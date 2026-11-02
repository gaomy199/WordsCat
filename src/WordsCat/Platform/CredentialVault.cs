namespace ReadDock;

internal interface ICredentialVault
{
    bool HasKey(string baseUrl);
    string Read(string baseUrl);
    void Save(string value, string baseUrl);
}

internal sealed class WindowsCredentialVault : ICredentialVault
{
    public bool HasKey(string baseUrl) => CredentialStore.HasSavedKey(baseUrl);
    public string Read(string baseUrl) => CredentialStore.ReadFor(baseUrl);
    public void Save(string value, string baseUrl) => CredentialStore.SaveFor(value, baseUrl);
}

internal sealed class DisabledCredentialVault : ICredentialVault
{
    public bool HasKey(string baseUrl) => false;
    public string Read(string baseUrl) => "";
    public void Save(string value, string baseUrl) => throw new InvalidOperationException("离线预览不保存密钥。");
}

internal static class CredentialScope
{
    internal const string Prefix = "WordsCat API origin: ";
    internal static bool Matches(string? comment, string url) => comment?.StartsWith(Prefix, StringComparison.Ordinal) != true || Same(comment[Prefix.Length..], url);
    internal static string Origin(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri.GetLeftPart(UriPartial.Authority) : "";
    internal static bool Same(string first, string second) => Origin(first).Length > 0 && string.Equals(Origin(first), Origin(second), StringComparison.OrdinalIgnoreCase);
}
