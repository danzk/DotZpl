using System;

namespace DotZpl
{
    /// <summary>
    /// WPF-only extension giving <c>DrawingContext.PushTransform(Matrix)</c> the IDisposable-scope
    /// shape Avalonia's instance method already has, so call sites use a uniform
    /// <c>using (dc.PushTransform(matrix))</c> on both backends. On the Avalonia TFM this file is not
    /// compiled and the native <c>DrawingContext.PushTransform(Matrix)</c> binds instead.
    /// </summary>
    internal static class WpfDrawingContextExtensions
    {
        public static IDisposable PushTransform(this DrawingContext dc, Matrix m)
        {
            dc.PushTransform(new MatrixTransform(m));
            return new PopGuard(dc);
        }

        private sealed class PopGuard : IDisposable
        {
            private readonly DrawingContext _dc;
            public PopGuard(DrawingContext dc) => _dc = dc;
            public void Dispose() => _dc.Pop();
        }
    }
}
