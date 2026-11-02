using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReadDock;

public class ModelRequestException(System.Net.HttpStatusCode? statusCode, string message) : InvalidOperationException(message)
{
    public System.Net.HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class OpenAiCompatibleClient : IModelService
{
    private readonly HttpClient http;
    public OpenAiCompatibleClient(HttpMessageHandler? handler = null)
    {
        http = handler == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler);
        http.Timeout = Timeout.InfiniteTimeSpan;
    }
    public Task<ApiResult> CompleteAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token)
    {
        if (messages.Any(m => m.ImageDataUrl != null)) throw new InvalidDataException("文本对话不能包含图片，请使用图片输入能力。");
        return SendAsync(options, key, messages, json, token);
    }
    public Task<ApiResult> CompleteImageAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, CancellationToken token)
    {
        if (!messages.Any(m => m.ImageDataUrl != null)) throw new InvalidDataException("图片输入请求必须包含图片。");
        return SendAsync(options, key, messages, true, token);
    }
    private async Task<ApiResult> SendAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token)
    {
        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(10) || options.MaxRetries is < 0 or > 5 || options.MaxOutputTokens is < 1 or > 4096)
            throw new InvalidOperationException("模型请求超时或重试参数无效。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(options.Timeout);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                try { return await SendOnceAsync(options, key, messages, json, deadline.Token); }
                catch (TransientModelException) when (attempt < options.MaxRetries)
                { await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(2000, 100 * (1 << attempt))), deadline.Token); }
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("请求超时，请检查网络后手动重试。"); }
    }
    private sealed class TransientModelException(HttpStatusCode? statusCode, string message) : ModelRequestException(statusCode, message) { }
    private async Task<ApiResult> SendOnceAsync(ModelRequestOptions config, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken requestToken)
    {
        SecretRedactor.Register(key);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("请先在「设置」中填写 DeepSeek API Key。");
        if (key.Trim().Any(char.IsWhiteSpace) || key.Any(char.IsControl)) throw new InvalidOperationException("API Key 中包含空格或换行，请重新粘贴完整的 Key。");
        if (!Uri.TryCreate(config.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
            throw new InvalidOperationException("API 地址必须是有效的 HTTPS 基础地址，不能带查询参数、密码或片段。");
        if (string.IsNullOrWhiteSpace(config.Model)) throw new InvalidOperationException("请填写模型名称。");
        var payload = new JsonObject
        {
            ["model"] = config.Model.Trim(),
            ["messages"] = new JsonArray(messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = MessageContent(m, ModelPresets.IsDeepSeek(endpoint)) }).ToArray()),
            ["stream"] = false,
            ["max_tokens"] = config.MaxOutputTokens
        };
        ModelPresets.Apply(payload, endpoint);
        if (json) payload["response_format"] = new JsonObject { ["type"] = "json_object" };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Trim());
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
            if (!response.IsSuccessStatusCode)
            {
                var reason = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "API Key 无效或已失效，请在设置中检查。",
                    HttpStatusCode.PaymentRequired when ModelPresets.IsDeepSeek(endpoint) => "DeepSeek 余额不足，请在官方平台检查余额。",
                    HttpStatusCode.TooManyRequests => "请求过于频繁或额度受限，请稍后手动重试。",
                    HttpStatusCode.NotFound => "API 地址或模型不存在，请检查设置。",
                    HttpStatusCode.BadRequest => "请求参数不被接受，请检查模型名称和 API 地址。",
                    _ => "服务暂时不可用，请稍后手动重试。"
                };
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                    throw new TransientModelException(response.StatusCode, $"{UiText.T(reason)} (HTTP {(int)response.StatusCode})");
                throw new ModelRequestException(response.StatusCode, $"{UiText.T(reason)} (HTTP {(int)response.StatusCode})");
            }
            // Parse under the caller's cancellation token; never log prompts, keys or response bodies.
            using var stream = await response.Content.ReadAsStreamAsync(requestToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: requestToken);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) throw new InvalidDataException("模型未返回结果，请手动重试。");
            var choice = choices[0];
            if (choice.ValueKind != JsonValueKind.Object || !choice.TryGetProperty("finish_reason", out var finish) || finish.ValueKind != JsonValueKind.String || finish.GetString() != "stop")
                throw new InvalidDataException("模型结果未完整结束，请缩小选区或减少输入后重试。");
            if (!choice.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("API 返回格式异常，请检查接口地址或稍后重试。");
            var text = content.GetString();
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("模型返回了空内容，请手动重试。");
            int input = 0, output = 0;
            if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
            {
                if (u.TryGetProperty("prompt_tokens", out var i) && i.ValueKind == JsonValueKind.Number) i.TryGetInt32(out input);
                if (u.TryGetProperty("completion_tokens", out var o) && o.ValueKind == JsonValueKind.Number) o.TryGetInt32(out output);
            }
            return new ApiResult(SecretRedactor.Redact(text), input, output);
        }
        catch (HttpRequestException) { throw new TransientModelException(null, "无法连接模型服务，请检查网络和 API 地址。"); }
        catch (JsonException) { throw new InvalidDataException("API 返回格式异常，请检查接口地址或稍后重试。"); }
        catch (Exception ex) when (SecretRedactor.Redact(ex.ToString()) != ex.ToString())
        { throw new InvalidOperationException("模型请求失败，敏感诊断信息已隐藏。"); }
    }
    static JsonNode MessageContent(ChatMessage message, bool deepSeek)
    {
        if (message.ImageDataUrl == null) return JsonValue.Create(message.Content)!;
        if (message.Role != "user" || !message.ImageDataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal)) throw new InvalidDataException("图片只能作为用户消息中的 PNG 图片发送。");
        return new JsonArray(
            new JsonObject { ["type"] = "text", ["text"] = message.Content },
            new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = message.ImageDataUrl, ["detail"] = deepSeek ? "original" : "auto" } });
    }
    public void Dispose() => http.Dispose();
}

