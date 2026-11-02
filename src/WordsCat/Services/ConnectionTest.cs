using System.Net;

namespace ReadDock;

internal enum ConnectionTestKind { Success, MissingKey, SaveKeyFirst, InvalidKey, QuotaOrRateLimit, NetworkOrTimeout, InvalidResponse, InvalidConfiguration, Unavailable, Cancelled, Offline }

internal sealed record ConnectionTestResult(ConnectionTestKind Kind)
{
    internal string MessageKey => Kind switch
    {
        ConnectionTestKind.Success => "连接成功。文本服务已响应；此测试不验证视觉识别。",
        ConnectionTestKind.MissingKey => "尚未配置此服务的 API Key。请填写密钥并保存设置。",
        ConnectionTestKind.SaveKeyFirst => "请先保存新密钥或服务地址，再测试已保存的凭据。",
        ConnectionTestKind.InvalidKey => "401：API Key 无效或已失效，请更新密钥。",
        ConnectionTestKind.QuotaOrRateLimit => "402/429：额度不足或请求过于频繁，请检查服务商额度后重试。",
        ConnectionTestKind.NetworkOrTimeout => "网络连接失败或请求超时，请检查网络和 Base URL。",
        ConnectionTestKind.InvalidResponse => "返回格式异常，接口可能不兼容 OpenAI chat completions。",
        ConnectionTestKind.InvalidConfiguration => "请填写有效的 HTTPS Base URL 和模型名。",
        ConnectionTestKind.Cancelled => "连接测试已取消。",
        ConnectionTestKind.Offline => "离线预览不读取密钥，也不请求真实服务。",
        _ => "服务暂时不可用，请检查服务地址或稍后重试。"
    };
}

internal interface IConnectionTester
{
    Task<ConnectionTestResult> TestAsync(AppConfig config, ICredentialVault credentials, CancellationToken token);
}

internal sealed class ConnectionTester(Func<IModelService>? factory = null) : IConnectionTester
{
    public async Task<ConnectionTestResult> TestAsync(AppConfig config, ICredentialVault credentials, CancellationToken token)
    {
        if (CredentialScope.Origin(config.BaseUrl).Length == 0 || string.IsNullOrWhiteSpace(config.Model)) return new(ConnectionTestKind.InvalidConfiguration);
        try
        {
            if (!credentials.HasKey(config.BaseUrl)) return new(ConnectionTestKind.MissingKey);
            string key = credentials.Read(config.BaseUrl);
            if (string.IsNullOrWhiteSpace(key)) return new(ConnectionTestKind.MissingKey);
            using IModelService client = factory?.Invoke() ?? new OpenAiCompatibleClient();
            var options = new ModelRequestOptions(config.BaseUrl, config.Model, TimeSpan.FromSeconds(Math.Clamp(config.RequestTimeoutSeconds, 1, 15)), 0, 8);
            await client.CompleteAsync(options, key, [new ChatMessage("user", "Reply OK.")], false, token);
            return new(ConnectionTestKind.Success);
        }
        catch (ModelRequestException ex)
        {
            return new(ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ConnectionTestKind.InvalidKey,
                HttpStatusCode.PaymentRequired or HttpStatusCode.TooManyRequests => ConnectionTestKind.QuotaOrRateLimit,
                null => ConnectionTestKind.NetworkOrTimeout,
                HttpStatusCode.BadRequest or HttpStatusCode.NotFound => ConnectionTestKind.InvalidConfiguration,
                _ => ConnectionTestKind.Unavailable
            });
        }
        catch (TimeoutException) { return new(ConnectionTestKind.NetworkOrTimeout); }
        catch (OperationCanceledException) { return new(token.IsCancellationRequested ? ConnectionTestKind.Cancelled : ConnectionTestKind.NetworkOrTimeout); }
        catch (InvalidDataException) { return new(ConnectionTestKind.InvalidResponse); }
        catch { return new(ConnectionTestKind.Unavailable); } // Never surface provider payloads, headers or exception details.
    }
}
