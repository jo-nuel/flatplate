using FlatPlate.Core.Enums;

namespace FlatPlate.App.ViewModels;

public enum RecipeEditMode { Add, Edit }

public sealed record RecipeIngredientDraft(int IngredientId, decimal Amount, MeasureUnit Unit);

/// <summary>Confirmed unsaved values. New recipes have no allocated ID; Edit retains its original ID.</summary>
public sealed record RecipeEditDraft(RecipeEditMode Mode, int? RecipeId, string Name, string Cuisine,
    int PrepMinutes, int BaseServings, string? ImagePath, IReadOnlyList<RecipeIngredientDraft> Ingredients)
{
    public string Status => "Not saved";
    public string Summary => $"{Name} · {BaseServings} servings · {Ingredients.Count} ingredients · Not saved";
}
