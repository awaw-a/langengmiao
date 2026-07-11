using System.Net.Http.Headers;
using System.Text.Json;

namespace Lanmian;

public sealed class Sb6657Client : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public string BaseUrl { get; set; }

    public Sb6657Client(string baseUrl)
    {
        BaseUrl = NormalizeBaseUrl(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(12);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Lanmian/0.1");
    }

    public async Task<Meme> FetchRandomMemeAsync(CancellationToken cancellationToken = default)
    {
        var url = $"{NormalizeBaseUrl(BaseUrl)}/machine/getRandOne";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("code", out var code) || code.GetInt32() != 200)
        {
            var message = root.TryGetProperty("msg", out var msg) ? msg.GetString() : "sb6657 返回失败";
            throw new InvalidOperationException(message ?? "sb6657 返回失败");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("接口没有返回烂梗数据");
        }

        var text = ReadString(data, "barrage");
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("接口返回了空烂梗");

        return new Meme(
            ReadString(data, "id"),
            text.Trim(),
            ReadString(data, "tags"),
            ReadString(data, "cnt"),
            ReadString(data, "submitTime"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.ToString() : string.Empty;
    }

    private static string NormalizeBaseUrl(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized) ? "https://hguofichp.cn:10086" : normalized;
    }

    public void Dispose() => _httpClient.Dispose();
}

