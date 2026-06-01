using System.Threading;
using System.Threading.Tasks;

namespace DotZpl.Viewer.Shared
{
    /// <summary>
    /// Sends raw ZPL to a network label printer. Zebra (and compatible) printers accept ZPL on a
    /// raw TCP socket (the JetDirect / RAW protocol, port 9100 by convention) — the same approach the
    /// BinaryKits viewer's print endpoint uses. The transport is platform-agnostic, so both viewers
    /// share one implementation; the interface exists so tests can substitute a fake (no real socket).
    /// </summary>
    public interface IZplPrinterService
    {
        /// <summary>
        /// Open a raw TCP connection to <paramref name="host"/>:<paramref name="port"/> and write the
        /// ZPL. Completes when the data has been flushed; throws on connection/IO failure.
        /// </summary>
        Task SendAsync(string host, int port, string zpl, CancellationToken cancellationToken = default);
    }
}
