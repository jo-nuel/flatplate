using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Services;

public interface IRecipeLibraryInteraction
{
    RecipeEditDraft? ShowRecipeDialog(RecipeEditDialogViewModel viewModel);

    // Older/read-only hosts need not provide importing; defaults never authorise a write.
    RecipeCatalogueImportFiles? ChooseRecipeImportFiles() => null;
    bool ConfirmRecipeImport(PersonBRecipeCatalogue catalogue) => false;
}

public sealed record RecipeCatalogueImportFiles(string RecipesPath, string IngredientsPath);
