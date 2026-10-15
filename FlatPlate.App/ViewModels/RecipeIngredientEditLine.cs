using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Represents one editable ingredient row and restricts units to those supported by its ingredient subtype.
/// </summary>
public sealed class RecipeIngredientEditLine : ViewModelBase
{
    private Ingredient? _selectedIngredient;
    private MeasureUnit? _selectedUnit;
    private string _amountText = string.Empty;
    private string _error = string.Empty;

    public RecipeIngredientEditLine(IReadOnlyList<Ingredient> ingredients)
    {
        IngredientOptions = ingredients;
    }

    public IReadOnlyList<Ingredient> IngredientOptions { get; }
    public IReadOnlyList<MeasureUnit> UnitOptions => RecipeDraftValidation.AllowedUnits(SelectedIngredient);
    public Ingredient? SelectedIngredient
    {
        get => _selectedIngredient;
        set
        {
            if (!SetProperty(ref _selectedIngredient, value)) return;
            OnPropertyChanged(nameof(UnitOptions));
            if (_selectedUnit is not null && !UnitOptions.Contains(_selectedUnit.Value)) SelectedUnit = null;
        }
    }
    public MeasureUnit? SelectedUnit { get => _selectedUnit; set => SetProperty(ref _selectedUnit, value); }
    public string AmountText { get => _amountText; set => SetProperty(ref _amountText, value ?? string.Empty); }
    public string Error => _error;
    internal void SetError(string error) => SetProperty(ref _error, error, nameof(Error));

    /// <summary>
    /// Returns the first actionable validation message for this row, or null when it is valid.
    /// </summary>
    internal string? Validate(bool duplicate)
    {
        if (SelectedIngredient is null || !IngredientOptions.Contains(SelectedIngredient)) return "Choose an ingredient from the supplied catalogue.";
        if (duplicate) return "Duplicate ingredient. Keep one line and adjust its quantity explicitly; no automatic merge is performed.";
        var amount = RecipeDraftValidation.ParseAmount(AmountText);
        if (amount is null) return "Enter a positive, exact decimal amount without separators or excess precision.";
        if (SelectedUnit is null || !RecipeDraftValidation.CanConvert(SelectedIngredient, amount.Value, SelectedUnit.Value))
            return "Choose a compatible unit. The amount must be convertible using this ingredient's existing unit rules.";
        return null;
    }
}
