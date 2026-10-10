namespace FlatPlate.App.Services;

/// <summary>
/// Lets view models request messages without depending directly on WPF windows.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Asks the user to confirm an action that changes planner data.
    /// </summary>
    bool Confirm(string message, string title);

    /// <summary>
    /// Shows a friendly error message to the user.
    /// </summary>
    void ShowError(string message, string title);
}
