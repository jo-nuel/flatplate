using FlatPlate.Core.Models;

namespace FlatPlate.App.Services;

/// <summary>Explicit host read dependencies; no context creation, ownership or database path.</summary>
public sealed class RecipeLibraryDataSource
{
    public RecipeLibraryDataSource(Func<IReadOnlyList<Recipe>> loadRecipes, Func<IReadOnlyList<Ingredient>> loadIngredients)
    {
        LoadRecipes = loadRecipes ?? throw new ArgumentNullException(nameof(loadRecipes));
        LoadIngredients = loadIngredients ?? throw new ArgumentNullException(nameof(loadIngredients));
    }

    public RecipeLibraryDataSource(RecipeLibraryReadService reader, Func<IReadOnlyList<Ingredient>> loadIngredients)
        : this((reader ?? throw new ArgumentNullException(nameof(reader))).LoadAllRecipes, loadIngredients) { }

    public Func<IReadOnlyList<Recipe>> LoadRecipes { get; }
    public Func<IReadOnlyList<Ingredient>> LoadIngredients { get; }
}
