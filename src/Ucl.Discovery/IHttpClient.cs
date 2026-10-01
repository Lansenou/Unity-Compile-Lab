namespace Ucl.Discovery;

/// <summary>Port to the network, used only by <c>ucl fetch</c> to talk to package registries.</summary>
public interface IHttpClient
{
    /// <summary>Downloads <paramref name="uri"/>.</summary>
    /// <param name="uri">Absolute http or https address.</param>
    /// <returns>The response body.</returns>
    /// <exception cref="HttpRequestException">The request failed or the status code is not a success.</exception>
    /// <exception cref="TaskCanceledException">The request timed out.</exception>
    Task<byte[]> GetBytesAsync(Uri uri);
}
