using System.Text.Json.Nodes;

namespace ReadDock;

public interface ITextModelClient
{
    Task<ApiResult> CompleteAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token);
}

public interface IImageModelClient
{
    Task<ApiResult> CompleteImageAsync(ModelRequestOptions options, string key, IReadOnlyList<ChatMessage> messages, CancellationToken token);
}

public interface IModelService : ITextModelClient, IImageModelClient, IDisposable { }

public sealed record ModelRequestOptions(string BaseUrl, string Model, TimeSpan Timeout, int MaxRetries = 0, int MaxOutputTokens = 4096)
{
    public static ModelRequestOptions From(AppConfig config, bool image = false) =>
        new(config.BaseUrl, image ? config.VisionModel : config.Model, TimeSpan.FromSeconds(config.RequestTimeoutSeconds), config.MaxRetries);
}

// Compatibility policy for the existing default provider; not part of the wire client.
internal static class ModelPresets
{
    internal const string BaseUrl = "https://api.deepseek.com";
    internal const string Model = "deepseek-flash";
    internal const string CredentialTarget = "ReadDock.DeepSeek.ApiKey.v1";
    internal static bool IsDeepSeek(Uri endpoint) => endpoint.Host.Equals("api.deepseek.com", StringComparison.OrdinalIgnoreCase);
    internal static void Apply(JsonObject payload, Uri endpoint)
    {
        if (IsDeepSeek(endpoint)) payload["thinking"] = new JsonObject { ["type"] = "disabled" };
    }
}

public static class ModelServiceExtensions
{
    public static Task<ApiResult> CompleteAsync(this ITextModelClient client, AppConfig config, string key, IReadOnlyList<ChatMessage> messages, bool json, CancellationToken token) =>
        client.CompleteAsync(ModelRequestOptions.From(config), key, messages, json, token);

    public static async Task<ImageTranslation> TranslateImageAsync(this IImageModelClient client, AppConfig config, string key, byte[] png, CancellationToken token)
    {
        if (png.Length == 0 || png.Length > 12 * 1024 * 1024) throw new InvalidOperationException("截图文件过大，请缩小框选区域后重试。");
        var result = await client.CompleteImageAsync(ModelRequestOptions.From(config, true), key, Prompts.Image(config, png), token);
        return Prompts.ParseImage(result);
    }
}
