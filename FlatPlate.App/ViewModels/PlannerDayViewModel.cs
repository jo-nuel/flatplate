using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Groups the three meal slots displayed beneath one planner day.
/// </summary>
public sealed class PlannerDayViewModel
{
    /// <summary>
    /// Creates breakfast, lunch, and dinner cells for one date.
    /// </summary>
    public PlannerDayViewModel(
        DateOnly date,
        IReadOnlyList<Recipe> availableRecipes)
    {
        ArgumentNullException.ThrowIfNull(availableRecipes);

        Date = date;
        MealSlots = new[]
        {
            new MealSlotViewModel(date, MealSlot.Breakfast, availableRecipes),
            new MealSlotViewModel(date, MealSlot.Lunch, availableRecipes),
            new MealSlotViewModel(date, MealSlot.Dinner, availableRecipes),
        };
    }

    public DateOnly Date { get; }

    public string DayLabel => Date.ToString("ddd");

    public string DateLabel => Date.ToString("d MMM");

    public IReadOnlyList<MealSlotViewModel> MealSlots { get; }
}
