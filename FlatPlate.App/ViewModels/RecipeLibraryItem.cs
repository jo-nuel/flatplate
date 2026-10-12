using System.Globalization;
using System.IO;
using System.Windows.Media;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

public sealed record RecipeIngredientRow(int IngredientId, string IngredientName, decimal Amount, MeasureUnit Unit)
{
    public string AmountDisplay => Amount.ToString(CultureInfo.InvariantCulture);
    public string UnitDisplay => Enum.IsDefined(Unit) ? Unit.ToString() : "Unknown unit";
}

/// <summary>Immutable display snapshot. Editing it cannot mutate an EF entity or navigation collection.</summary>
public sealed class RecipeLibraryItem
{
    public RecipeLibraryItem(Recipe recipe, ImageSource? image = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.Id <= 0 || string.IsNullOrWhiteSpace(recipe.Name))
            throw new InvalidDataException("A recipe is missing its name or identity. The previous catalogue is unchanged.");
        Id = recipe.Id; Name = recipe.Name; Cuisine = recipe.Cuisine; PrepMinutes = recipe.PrepMinutes;
        BaseServings = recipe.BaseServings; ImagePath = recipe.ImagePath; Image = image;
        Ingredients = recipe.Ingredients.Select(line =>
        {
            if (line.Ingredient is null || line.IngredientId <= 0 || line.Ingredient.Id != line.IngredientId || string.IsNullOrWhiteSpace(line.Ingredient.Name))
                throw new InvalidDataException("Recipe ingredients could not be read completely. The previous catalogue is unchanged.");
            return new RecipeIngredientRow(line.IngredientId, line.Ingredient.Name, line.Amount, line.Unit);
        }).OrderBy(line => line.IngredientName, StringComparer.CurrentCultureIgnoreCase).ThenBy(line => line.IngredientId).ToList().AsReadOnly();
    }

    public int Id { get; }
    public string Name { get; }
    public string Cuisine { get; }
    public string CuisineDisplay => string.IsNullOrWhiteSpace(Cuisine) ? "No cuisine specified" : Cuisine;
    public int PrepMinutes { get; }
    public int BaseServings { get; }
    public string? ImagePath { get; }
    public ImageSource? Image { get; }
    public bool HasImage => Image is not null;
    public IReadOnlyList<RecipeIngredientRow> Ingredients { get; }
    public string QuantityNotice => $"Amounts are totals for {BaseServings} base servings, not per portion.";
}
