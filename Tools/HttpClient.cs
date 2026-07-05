using System.Net;
using System.Net.Sockets;

/// <summary>
/// Erstellt einen gehaerteten <see cref="HttpClient"/> fuer ausgehende Requests der Web-Tools.
/// </summary>
internal static class SafeHttpClientFactory
{
    public static HttpClient Create()
    {
        // ConnectCallback prueft die tatsaechlich aufgeloeste IP-Adresse beim Verbindungsaufbau
        // (statt nur den Hostnamen) und verhindert so SSRF inkl. DNS-Rebinding auf interne/private Ziele.
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                var address = addresses.FirstOrDefault(a => !IsPrivateOrReserved(a))
                    ?? throw new InvalidOperationException(
                        $"Host '{context.DnsEndPoint.Host}' loest ausschliesslich auf private/reservierte Adressen auf und wird aus Sicherheitsgruenden blockiert.");

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestHeaders =
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
            }
        };
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                169 when b[1] == 254 => true, // link-local, inkl. Cloud-Metadata 169.254.169.254
                172 when b[1] is >= 16 and <= 31 => true,
                192 when b[1] == 168 => true,
                _ => false,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return true;

            var b = address.GetAddressBytes();
            return (b[0] & 0xfe) == 0xfc; // fc00::/7, Unique Local Address
        }

        return true; // unbekannte Adressfamilie sicherheitshalber blockieren
    }
}
