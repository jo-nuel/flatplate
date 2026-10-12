using FlatPlate.App.ViewModels;

namespace FlatPlate.App.Services;

public interface IRecipeLibraryInteraction
{
    RecipeEditDraft? ShowRecipeDialog(RecipeEditDialogViewModel viewModel);
}
