using System.Windows;
using FlatPlate.App.ViewModels;
using FlatPlate.App.Views;

namespace FlatPlate.App.Services;

public sealed class RecipeLibraryInteraction : IRecipeLibraryInteraction
{
    private readonly Func<Window?> _owner;
    public RecipeLibraryInteraction(Func<Window?> owner) { _owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
    public RecipeEditDraft? ShowRecipeDialog(RecipeEditDialogViewModel viewModel) => RecipeEditDialog.Prompt(_owner(), viewModel);
}
