using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Represents one breakfast, lunch, or dinner cell in the weekly planner.
/// </summary>
public sealed class MealSlotViewModel : ViewModelBase
{
    private const int DefaultServings = 2;

    private Recipe? _selectedRecipe;
    private int _servings = DefaultServings;

    /// <summary>
    /// Creates a planner cell for one date and meal slot.
    /// </summary>
    public MealSlotViewModel(
        DateOnly date,
        MealSlot slot,
        IReadOnlyList<Recipe> availableRecipes)
    {
        ArgumentNullException.ThrowIfNull(availableRecipes);

        Date = date;
        Slot = slot;
        AvailableRecipes = availableRecipes;
        ClearCommand = new RelayCommand(
            Clear,
            () => SelectedRecipe is not null);
    }

    /// <summary>
    /// Occurs when the selected recipe or serving count changes.
    /// </summary>
    public event EventHandler? MealChanged;

    public DateOnly Date { get; }

    public MealSlot Slot { get; }

    public string SlotLabel => Slot.ToString();

    public IReadOnlyList<Recipe> AvailableRecipes { get; }

    public RelayCommand ClearCommand { get; }

    public Recipe? SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (SetProperty(ref _selectedRecipe, value))
            {
                ClearCommand.RaiseCanExecuteChanged();
                RaiseMealChanged();
            }
        }
    }

    public int Servings
    {
        get => _servings;
        set
        {
            if (SetProperty(ref _servings, value))
            {
                RaiseMealChanged();
            }
        }
    }

    /// <summary>
    /// Loads a saved meal without treating the initial values as user changes.
    /// </summary>
    public void LoadMeal(PlannedMeal? plannedMeal)
    {
        _selectedRecipe = plannedMeal?.Recipe;
        _servings = plannedMeal?.Servings ?? DefaultServings;

        OnPropertyChanged(nameof(SelectedRecipe));
        OnPropertyChanged(nameof(Servings));
        ClearCommand.RaiseCanExecuteChanged();
    }

    private void Clear()
    {
        SelectedRecipe = null;
    }

    private void RaiseMealChanged()
    {
        MealChanged?.Invoke(this, EventArgs.Empty);
    }
}
