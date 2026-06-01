using System;
using System.Windows;

using DotZpl.Viewer.Shared;

namespace DotZpl.Viewer
{
    /// <summary>WPF implementation of <see cref="IDispatcher"/>.</summary>
    internal sealed class WpfDispatcher : IDispatcher
    {
        public void Post(Action action) => Application.Current.Dispatcher.BeginInvoke(action);
    }
}
