using System.Windows;

namespace FlatPlate.App.Services;

/// <summary>
/// Displays confirmation and error messages with standard WPF dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public bool Confirm(string message, string title)
    {
        return MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public void ShowError(string message, string title)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
