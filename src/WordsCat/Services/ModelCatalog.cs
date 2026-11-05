using System.Net.Http.Headers;
using System.Text.Json;

namespace ReadDock;

internal static class ModelCatalog
{
    internal static async Task<string[]> FetchAsync(string baseUrl, ICredentialVault credentials, CancellationToken token)
    {
        baseUrl = ModelPresets.NormalizeBaseUrl(baseUrl);
        if (CredentialScope.Origin(baseUrl).Length == 0) throw new InvalidOperationException("请输入有效的 HTTPS 基础地址。");
        string key = credentials.Read(baseUrl);
        SecretRedactor.Register(key);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("请先输入此服务的 API Key，再获取模型。");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl + "/"), "models"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        try
        {
            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "无法获取模型：密钥无效或没有访问权限。",
                    System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.MethodNotAllowed => "此服务不支持模型列表，请手动填写模型 ID。",
                    _ => "无法获取模型，请检查服务地址、额度或稍后重试。"
                });
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("模型列表格式异常，请手动填写模型 ID。");
            var models = data.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                .Select(item => item.GetProperty("id").GetString()!)
                .Where(id => id.Length is > 0 and <= 200 && !id.Any(char.IsControl) && SecretRedactor.Redact(id) == id)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).Take(500).ToArray();
            if (models.Length == 0) throw new InvalidDataException("未返回可选模型，请检查权限或手动填写模型 ID。");
            return models;
        }
        catch (JsonException) { throw new InvalidDataException("模型列表格式异常，请手动填写模型 ID。"); }
        catch (HttpRequestException) { throw new InvalidOperationException("无法获取模型，请检查网络和 API 地址。"); }
        catch (TaskCanceledException) when (!token.IsCancellationRequested) { throw new InvalidOperationException("获取模型超时，请稍后重试或手动填写模型 ID。"); }
    }
}
