using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Ucl.Integration.Tests;

/// <summary>An npm-protocol package registry on <c>http://127.0.0.1:&lt;free port&gt;</c>, serving packages built in memory.</summary>
public sealed class LoopbackRegistry : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Dictionary<string, byte[]> responses = new(StringComparer.Ordinal);
    private readonly Task loop;

    /// <summary>Starts listening.</summary>
    public LoopbackRegistry()
    {
        Url = $"http://127.0.0.1:{FreePort()}";
        listener.Prefixes.Add(Url + "/");
        listener.Start();
        loop = Task.Run(ServeAsync);
    }

    /// <summary>Registry URL without a trailing slash.</summary>
    public string Url { get; }

    /// <summary>Publishes one package version whose tarball holds <c>package/package.json</c> plus <paramref name="files"/>.</summary>
    public LoopbackRegistry Publish(string name, string version, string dependencies = "", params (string Path, string Content)[] files)
    {
        var tgz = Tarball([("package/package.json", $"{{ \"name\": \"{name}\", \"version\": \"{version}\", \"dependencies\": {{ {dependencies} }} }}"),
            .. files.Select(f => ($"package/{f.Path}", f.Content))]);
        var tarball = $"{Url}/{name}/-/{name}-{version}.tgz";
        var sha = Convert.ToHexStringLower(SHA1.HashData(tgz));
        lock (responses)
        {
            responses[$"/{name}"] = Encoding.UTF8.GetBytes(
                $"{{ \"name\": \"{name}\", \"versions\": {{ \"{version}\": {{ \"dist\": {{ \"tarball\": \"{tarball}\", \"shasum\": \"{sha}\" }} }} }} }}");
            responses[$"/{name}/-/{name}-{version}.tgz"] = tgz;
        }

        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        listener.Stop();
        listener.Close();
        try
        {
            loop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // The accept loop ends with an exception when the listener stops; that is the expected shutdown.
        }
    }

    private static byte[] Tarball(IEnumerable<(string Name, string Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax))
        {
            foreach (var (name, content) in entries)
            {
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)) });
            }
        }

        return stream.ToArray();
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task ServeAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            byte[]? body;
            lock (responses)
            {
                body = responses.GetValueOrDefault(context.Request.Url!.AbsolutePath);
            }

            context.Response.StatusCode = body is null ? 404 : 200;
            if (body is not null)
            {
                await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
            }

            context.Response.Close();
        }
    }
}
