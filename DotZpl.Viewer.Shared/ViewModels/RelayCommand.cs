using System;
using System.Windows.Input;

namespace DotZpl.Viewer.Shared.ViewModels
{
    /// <summary>
    /// A basic <see cref="ICommand"/> that delegates to actions. The original WPF version wired
    /// <c>CanExecuteChanged</c> to <c>CommandManager.RequerySuggested</c>; that hook is WPF-only,
    /// so this cross-platform version exposes a manual <see cref="RaiseCanExecuteChanged"/> that
    /// VMs can call when their command predicates change.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute == null ? null : _ => canExecute()) { }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
