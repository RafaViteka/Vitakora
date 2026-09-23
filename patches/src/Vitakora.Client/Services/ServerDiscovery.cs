using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace Vitakora.Client.Services;

public static class ServerDiscovery
{
    private const int DiscoveryPort = 50505;
    private const string Request = "VITAKORA_DISCOVER_V1";
    private const string ResponsePrefix = "VITAKORA_SERVER_V1|";

    public static string ConfigFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Vitakora", "server.txt");

    public static async Task<string?> FindAsync(CancellationToken cancellationToken = default)
    {
        var saved = ReadSaved();
        if (saved is not null && await IsVitakoraAsync(saved, cancellationToken))
            return saved;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (await IsVitakoraAsync("http://localhost:5000", cancellationToken))
            {
                Save("http://localhost:5000");
                return "http://localhost:5000";
            }

            await Task.Delay(500, cancellationToken);
        }

        try
        {
            using var udp = new UdpClient(0) { EnableBroadcast = true };
            var payload = Encoding.UTF8.GetBytes(Request);
            await udp.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));

            while (!timeout.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(timeout.Token);
                    var text = Encoding.UTF8.GetString(result.Buffer);
                    if (!text.StartsWith(ResponsePrefix, StringComparison.Ordinal)) continue;

                    var url = text[ResponsePrefix.Length..].Trim().TrimEnd('/');
                    if (Uri.TryCreate(url, UriKind.Absolute, out _) &&
                        await IsVitakoraAsync(url, timeout.Token))
                    {
                        Save(url);
                        return url;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    public static async Task<bool> IsVitakoraAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.GetAsync(baseUrl.TrimEnd('/') + "/health", cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Contains("\"product\":\"Vitakora\"", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void Save(string url)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
            File.WriteAllText(ConfigFile, url.TrimEnd('/'));
        }
        catch { }
    }

    private static string? ReadSaved()
    {
        try
        {
            if (!File.Exists(ConfigFile)) return null;
            var value = File.ReadAllText(ConfigFile).Trim().TrimEnd('/');
            return Uri.TryCreate(value, UriKind.Absolute, out _) ? value : null;
        }
        catch
        {
            return null;
        }
    }
}
