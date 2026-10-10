using System.Windows.Input;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Connects a WPF button or menu item to a view-model action.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    /// <summary>
    /// Creates a command for an action that does not need a parameter.
    /// </summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(
            _ => execute(),
            canExecute is null ? null : _ => canExecute())
    {
        ArgumentNullException.ThrowIfNull(execute);
    }

    /// <summary>
    /// Creates a command that receives its parameter from the WPF control.
    /// </summary>
    public RelayCommand(
        Action<object?> execute,
        Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// Occurs when WPF should check whether the command is available.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Reports whether the command can currently run.
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <summary>
    /// Runs the action associated with the command.
    /// </summary>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// Asks WPF to refresh the enabled state of controls using this command.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
