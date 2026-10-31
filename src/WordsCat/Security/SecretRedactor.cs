using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ReadDock
{
    // Shared by runtime sinks and the offline PowerShell guard. No credential or
    // environment access is permitted here. Only the caller's in-memory value
    // may be registered, and it is never persisted or included in diagnostics.
    public static class SecretRedactor
    {
        public const string Marker = "[REDACTED]";
        private static readonly object Gate = new object();
        private static readonly List<string> Known = new List<string>();
        private static readonly Regex KeyPattern = new Regex(@"(?i)(?<![\w])sk-[a-z0-9][a-z0-9_-]{3,}");
        private static readonly Regex ProviderPattern = new Regex(@"(?i)(?<![\w])(?:github_pat_[a-z0-9_]{20,}|gh[pousr]_[a-z0-9]{20,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)");
        private static readonly Regex LiteralPattern = new Regex(@"(?i)(?<![\w])(?:api_?key|token|secret|password)\b\s*(?:\\?[""'])?\s*\]?\s*[:=]\s*(?:@|\$)?\\?[""'](?<value>[^""'\r\n]+?)(?:\\?[""'])");
        private static readonly Regex HeaderPattern = new Regex(@"(?i)\bAuthorization\b(?:\\?[""'])?\s*[:=]\s*(?:\\?[""'])?\s*(?:Bearer|Basic)[ \t]+(?<value>[^""'\s\x00-\x20\\,;}<>]+)");

        public static void Register(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < 4) return;
            lock (Gate) { if (!Known.Contains(value.Trim())) Known.Add(value.Trim()); }
        }
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            string[] values;
            lock (Gate) { values = Known.ToArray(); }
            foreach (string value in values)
            {
                text = text.Replace(value, Marker);
                text = text.Replace(Uri.EscapeDataString(value), Marker);
            }
            text = HeaderPattern.Replace(text, Marker);
            text = LiteralPattern.Replace(text, delegate(Match m) {
                Group value = m.Groups["value"];
                return m.Value.Substring(0, value.Index - m.Index) + Marker + m.Value.Substring(value.Index - m.Index + value.Length);
            });
            return ProviderPattern.Replace(KeyPattern.Replace(text, Marker), Marker);
        }
        public static List<SecretFinding> Find(string text)
        {
            var result = new List<SecretFinding>();
            AddMatches(result, KeyPattern, text, "key-prefix", false);
            AddMatches(result, ProviderPattern, text, "provider-credential", false);
            AddMatches(result, LiteralPattern, text, "credential-literal", true);
            AddMatches(result, HeaderPattern, text, "authorization-header", true);
            return result;
        }
        public static List<SecretFinding> FindBinary(string text)
        {
            var result = new List<SecretFinding>();
            if (text.IndexOf("sk-", StringComparison.OrdinalIgnoreCase) >= 0) AddMatches(result, KeyPattern, text, "key-prefix", false);
            if (text.IndexOf("gh", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("PRIVATE KEY", StringComparison.Ordinal) >= 0) AddMatches(result, ProviderPattern, text, "provider-credential", false);
            if (text.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) >= 0) AddMatches(result, HeaderPattern, text, "authorization-header", true);
            return result;
        }
        private static void AddMatches(List<SecretFinding> result, Regex pattern, string text, string kind, bool valueGroup)
        {
            foreach (Match match in pattern.Matches(text))
            {
                string value = valueGroup ? match.Groups["value"].Value : match.Value;
                // Exact, explicit fake values only; no line-wide suppressions.
                if (value == "test-api-key-not-real" || value == Marker) continue;
                result.Add(new SecretFinding { Index = match.Index, Type = kind });
            }
        }
    }
    public sealed class SecretFinding
    {
        public SecretFinding() { Type = string.Empty; }
        public int Index { get; set; }
        public string Type { get; set; }
    }
}
