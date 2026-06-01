using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotZpl.Viewer.Shared
{
    /// <summary>
    /// Default <see cref="IZplPrinterService"/>: streams the ZPL straight to the printer over a raw
    /// TCP socket, mirroring the BinaryKits viewer's PrintController. ZPL is plain ASCII, so the bytes
    /// go on the wire as-is with no driver, spooler, or rendering in between.
    /// </summary>
    public sealed class NetworkZplPrinterService : IZplPrinterService
    {
        public async Task SendAsync(string host, int port, string zpl, CancellationToken cancellationToken = default)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

            await using NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes(zpl);
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
