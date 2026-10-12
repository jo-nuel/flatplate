using System.Windows;
using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Views;

/// <summary>Lets the user add or edit one pack price and returns the validated values as a draft.</summary>
public partial class PriceEditDialog : Window
{
    private readonly PriceEditDialogViewModel _viewModel;

    public PriceEditDialog(PriceEditDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Confirmed += OnConfirmed;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Shows the dialog and returns the confirmed draft, or null when the user cancels.
    /// Nothing is saved by this method.
    /// </summary>
    public static PriceEditDraft? Prompt(Window? owner, PriceEditDialogViewModel viewModel)
    {
        var dialog = new PriceEditDialog(viewModel) { Owner = owner };
        return dialog.ShowDialog() == true ? viewModel.Result : null;
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.IsEditMode)
        {
            PriceTextBox.Focus();
            PriceTextBox.SelectAll();
        }
        else
        {
            IngredientComboBox.Focus();
        }
    }

    private void OnConfirmed(object? sender, EventArgs eventArgs)
    {
        DialogResult = true;
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _viewModel.Confirmed -= OnConfirmed;
    }
}
