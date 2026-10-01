namespace Ucl.Discovery;

/// <summary>
/// <see cref="IHttpClient"/> over <see cref="HttpClient"/>. The default handler honours the system proxy settings
/// (<c>HTTPS_PROXY</c>, <c>HTTP_PROXY</c>, <c>NO_PROXY</c>), which is what corporate networks need for registries.
/// </summary>
public sealed class SystemHttpClient : IHttpClient, IDisposable
{
    /// <summary>Per-request timeout: long enough for a large package on a slow link, short enough to not hang CI.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly HttpClient client = new() { Timeout = Timeout };

    /// <summary>Creates the client.</summary>
    public SystemHttpClient()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ucl");
    }

    /// <inheritdoc/>
    public async Task<byte[]> GetBytesAsync(Uri uri)
    {
        using var response = await client.GetAsync(uri).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() => client.Dispose();
}
