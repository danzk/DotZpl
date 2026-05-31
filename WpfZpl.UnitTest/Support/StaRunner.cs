using System;
using System.Threading;

namespace WpfZpl.UnitTest
{
    /// <summary>
    /// Runs a delegate on a dedicated STA thread. WPF rendering types
    /// (<c>RenderTargetBitmap</c>, <c>GlyphTypeface</c>) require an STA apartment.
    /// </summary>
    internal static class StaRunner
    {
        public static T Run<T>(Func<T> func)
        {
            T result = default!;
            Exception? captured = null;

            var thread = new Thread(() =>
            {
                try
                {
                    result = func();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            if (captured != null)
            {
                throw new InvalidOperationException("STA worker threw: " + captured.Message, captured);
            }

            return result;
        }
    }
}
