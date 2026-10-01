namespace Ucl.Discovery.Tests;

/// <summary>Serves fixed responses by URL and records every request; unknown URLs fail like a 404.</summary>
public sealed class FakeHttpClient : IHttpClient
{
    public Dictionary<string, byte[]> Responses { get; } = new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    public Task<byte[]> GetBytesAsync(Uri uri)
    {
        Requests.Add(uri.ToString());
        return Responses.TryGetValue(uri.ToString(), out var body)
            ? Task.FromResult(body)
            : Task.FromException<byte[]>(new HttpRequestException($"Response status code does not indicate success: 404 (Not Found) for {uri}"));
    }

    /// <summary>Publishes a package: its registry document and its tarball, with a correct SHA-1.</summary>
    public FakeHttpClient Publish(string registry, string name, string version, byte[] tgz, string? shasum = "")
    {
        var tarball = $"{registry}/{name}/-/{name}-{version}.tgz";
        Responses[$"{registry}/{name}"] = Tgz.Document(name, version, tarball, shasum == string.Empty ? Tgz.Sha1(tgz) : shasum);
        Responses[tarball] = tgz;
        return this;
    }
}
