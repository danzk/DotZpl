using System;

using DotZpl.Viewer.Shared;

namespace DotZpl.Viewer.Avalonia
{
    /// <summary>
    /// Avalonia implementation of <see cref="IDispatcher"/>. Avalonia.Threading also declares an
    /// <c>IDispatcher</c>, so this file fully qualifies <c>Dispatcher.UIThread</c> rather than
    /// importing the namespace.
    /// </summary>
    internal sealed class AvaloniaDispatcher : IDispatcher
    {
        public void Post(Action action) => global::Avalonia.Threading.Dispatcher.UIThread.Post(action);
    }
}
