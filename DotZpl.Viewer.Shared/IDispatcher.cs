namespace DotZpl.Viewer.Shared
{
    /// <summary>
    /// Marshals work back to the UI thread. WPF implementation wraps <c>Application.Current.Dispatcher</c>;
    /// Avalonia implementation wraps <c>Avalonia.Threading.Dispatcher.UIThread</c>. Lets the shared
    /// ViewModel debounce text edits with a thread-pool timer without taking a platform dependency.
    /// </summary>
    public interface IDispatcher
    {
        void Post(Action action);
    }
}
