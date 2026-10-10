using FlatPlate.Core.Enums;
using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;

namespace FlatPlate.Core.Services;

/// <summary>
/// Owns weekly plan changes and notifies screens that depend on the plan.
/// </summary>
public sealed class WeeklyPlanService
{
    private const int MaximumServings = 20;
    private const int DaysInWeek = 7;

    private readonly IRepository<PlannedMeal> _plannedMeals;

    /// <summary>
    /// Creates a planner that saves meals through the supplied repository.
    /// </summary>
    public WeeklyPlanService(IRepository<PlannedMeal> plannedMeals)
    {
        ArgumentNullException.ThrowIfNull(plannedMeals);
        _plannedMeals = plannedMeals;
    }

    /// <summary>
    /// Occurs after a planned meal is added, changed, or removed.
    /// </summary>
    public event EventHandler? PlanChanged;

    /// <summary>
    /// Gets the planned meals from the supplied week start through seven days.
    /// </summary>
    public IReadOnlyList<PlannedMeal> GetWeek(DateOnly weekStart)
    {
        var weekEnd = weekStart.AddDays(DaysInWeek);

        return _plannedMeals
            .Find(meal => meal.Date >= weekStart && meal.Date < weekEnd)
            .OrderBy(meal => meal.Date)
            .ThenBy(meal => meal.Slot)
            .ToList();
    }

    /// <summary>
    /// Adds a meal to a planner slot or replaces the meal already in that slot.
    /// </summary>
    public PlannedMeal SetMeal(
        DateOnly date,
        MealSlot slot,
        Recipe recipe,
        int servings)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ValidateServings(servings);

        var plannedMeal = FindMeal(date, slot);

        if (plannedMeal is null)
        {
            plannedMeal = new PlannedMeal { Date = date, Slot = slot };
            ApplyMealDetails(plannedMeal, recipe, servings);
            _plannedMeals.Add(plannedMeal);
        }
        else
        {
            ApplyMealDetails(plannedMeal, recipe, servings);
            _plannedMeals.Update(plannedMeal);
        }

        RaisePlanChanged();
        return plannedMeal;
    }

    /// <summary>
    /// Removes one planned meal and reports whether the slot contained a meal.
    /// </summary>
    public bool ClearMeal(DateOnly date, MealSlot slot)
    {
        var plannedMeal = FindMeal(date, slot);

        if (plannedMeal is null)
        {
            return false;
        }

        _plannedMeals.Delete(plannedMeal);
        RaisePlanChanged();
        return true;
    }

    /// <summary>
    /// Removes every planned meal in a seven-day period.
    /// </summary>
    public int ClearWeek(DateOnly weekStart)
    {
        var plannedMeals = GetWeek(weekStart);

        foreach (var plannedMeal in plannedMeals)
        {
            _plannedMeals.Delete(plannedMeal);
        }

        if (plannedMeals.Count > 0)
        {
            RaisePlanChanged();
        }

        return plannedMeals.Count;
    }

    private static void ApplyMealDetails(
        PlannedMeal plannedMeal,
        Recipe recipe,
        int servings)
    {
        plannedMeal.RecipeId = recipe.Id;
        plannedMeal.Recipe = recipe;
        plannedMeal.Servings = servings;
    }

    private static void ValidateServings(int servings)
    {
        if (servings is < 1 or > MaximumServings)
        {
            throw new ArgumentOutOfRangeException(
                nameof(servings),
                servings,
                $"Servings must be from 1 to {MaximumServings}.");
        }
    }

    private PlannedMeal? FindMeal(DateOnly date, MealSlot slot)
    {
        return _plannedMeals
            .Find(meal => meal.Date == date && meal.Slot == slot)
            .SingleOrDefault();
    }

    private void RaisePlanChanged()
    {
        PlanChanged?.Invoke(this, EventArgs.Empty);
    }
}
