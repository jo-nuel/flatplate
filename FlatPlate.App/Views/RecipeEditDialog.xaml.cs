using System.Windows;
using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Views;

/// <summary>
/// Hosts the validated recipe editor and returns a draft only after explicit confirmation.
/// </summary>
public partial class RecipeEditDialog : Window
{
    private readonly RecipeEditDialogViewModel _model;
    public RecipeEditDialog(RecipeEditDialogViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        InitializeComponent(); DataContext = model; model.Confirmed += OnConfirmed;
        Loaded += (_, _) => { NameTextBox.Focus(); NameTextBox.SelectAll(); };
        Closed += (_, _) => _model.Confirmed -= OnConfirmed;
    }

    public static RecipeEditDraft? Prompt(Window? owner, RecipeEditDialogViewModel model)
    {
        var window = new RecipeEditDialog(model) { Owner = owner };
        return window.ShowDialog() == true ? model.Result : null;
    }
    private void OnConfirmed(object? sender, EventArgs args) => DialogResult = true;
}
