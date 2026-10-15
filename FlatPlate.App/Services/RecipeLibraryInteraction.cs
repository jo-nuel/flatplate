using System.Windows;
using FlatPlate.App.ViewModels;
using FlatPlate.App.Views;
using Microsoft.Win32;

namespace FlatPlate.App.Services;

/// <summary>
/// Provides the Recipe Library's modal dialogs, file selection, and import confirmation prompts.
/// </summary>
public sealed class RecipeLibraryInteraction : IRecipeLibraryInteraction
{
    private readonly Func<Window?> _owner;
    public RecipeLibraryInteraction(Func<Window?> owner) { _owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
    public RecipeEditDraft? ShowRecipeDialog(RecipeEditDialogViewModel viewModel) => RecipeEditDialog.Prompt(_owner(), viewModel);

    public RecipeCatalogueImportFiles? ChooseRecipeImportFiles()
    {
        var recipes = new OpenFileDialog
        {
            Title = "Step 1 of 2: Select the recipe catalogue CSV",
            Filter = "CSV files (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = false,
            FileName = "FlatPlate_Recipes_Seed.csv",
        };
        if (!Show(recipes)) return null;
        var ingredients = new OpenFileDialog
        {
            Title = "Step 2 of 2: Select the recipe ingredient CSV",
            Filter = "CSV files (*.csv)|*.csv",
            CheckFileExists = true,
            Multiselect = false,
            FileName = "FlatPlate_RecipeIngredients_Seed.csv",
        };
        return Show(ingredients) ? new RecipeCatalogueImportFiles(recipes.FileName, ingredients.FileName) : null;
    }

    public bool ConfirmRecipeImport(PersonBRecipeCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        var message = $"Import a catalogue of {catalogue.Recipes.Count} recipes and {catalogue.Ingredients.Count} ingredient associations?\n\n"
            + "Only missing recipes and ingredients will be added. Existing recipes, planned meals and prices will be preserved. "
            + "A different existing recipe with the same name will block the entire import. No research prices will be imported.\n\n"
            + "Carrot and Potato reuse Carrots and Potatoes. Long grain rice and Tasty cheddar cheese remain separate ingredients.\n\n"
            + "Continue?";
        var owner = _owner();
        return (owner is null
            ? MessageBox.Show(message, "Import recipe catalogue", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            : MessageBox.Show(owner, message, "Import recipe catalogue", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)) == MessageBoxResult.Yes;
    }

    private bool Show(CommonDialog dialog)
    {
        var owner = _owner();
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
    }
}
