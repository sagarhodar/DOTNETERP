using System;
using System.Windows.Input;

namespace Ojaswat.ViewModels;

/// <summary>
/// Minimal ICommand implementation that delegates to Action delegates.
/// CanExecuteChanged re-uses WPF's CommandManager so bindings auto-refresh.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action        _execute;
    private readonly Func<bool>?   _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public event EventHandler? CanExecuteChanged
    {
        add    => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    /// <summary>Force WPF to re-query CanExecute on all commands.</summary>
    public static void Refresh() =>
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
}
