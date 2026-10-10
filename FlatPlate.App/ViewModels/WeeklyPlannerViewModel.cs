using FlatPlate.App.Services;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Coordinates the seven-day planner and saves changes made in its meal cells.
/// </summary>
public sealed class WeeklyPlannerViewModel : ViewModelBase
{
    private const int DaysInWeek = 7;

    private readonly WeeklyPlanService _planService;
    private readonly IDialogService _dialogService;

    private DateTime _weekStartDate;
    private IReadOnlyList<PlannerDayViewModel> _days = Array.Empty<PlannerDayViewModel>();
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Creates the planner from the available recipes and saved weekly plan.
    /// </summary>
    public WeeklyPlannerViewModel(
        WeeklyPlanService planService,
        IDialogService dialogService,
        IReadOnlyList<Recipe> availableRecipes)
    {
        ArgumentNullException.ThrowIfNull(planService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(availableRecipes);

        _planService = planService;
        _dialogService = dialogService;
        AvailableRecipes = availableRecipes
            .OrderBy(recipe => recipe.Name)
            .ToList();
        _weekStartDate = GetMonday(DateTime.Today);

        ClearWeekCommand = new RelayCommand(
            ClearWeek,
            () => Days.SelectMany(day => day.MealSlots)
                .Any(slot => slot.SelectedRecipe is not null));

        _planService.PlanChanged += OnPlanChanged;
        LoadWeek();
    }

    public IReadOnlyList<Recipe> AvailableRecipes { get; }

    public RelayCommand ClearWeekCommand { get; }

    public DateTime WeekStartDate
    {
        get => _weekStartDate;
        set
        {
            var monday = GetMonday(value);

            if (SetProperty(ref _weekStartDate, monday))
            {
                LoadWeek();
            }
        }
    }

    public IReadOnlyList<PlannerDayViewModel> Days
    {
        get => _days;
        private set
        {
            if (SetProperty(ref _days, value))
            {
                ClearWeekCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private static DateTime GetMonday(DateTime date)
    {
        var daysSinceMonday =
            (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }

    private void LoadWeek()
    {
        try
        {
            var weekStart = DateOnly.FromDateTime(WeekStartDate);
            var savedMeals = _planService.GetWeek(weekStart);
            Days = CreateDays(weekStart, savedMeals);
            StatusMessage = AvailableRecipes.Count == 0
                ? "No recipes are available yet. Add recipes before planning meals."
                : string.Empty;
        }
        catch (Exception exception)
        {
            ShowPlannerError(exception);
        }
    }

    private void OnMealChanged(object? sender, EventArgs eventArgs)
    {
        if (sender is not MealSlotViewModel mealSlot)
        {
            return;
        }

        try
        {
            SaveMeal(mealSlot);
            StatusMessage = string.Empty;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            _dialogService.ShowError(exception.Message, "Invalid servings");
            LoadWeek();
        }
        catch (Exception exception)
        {
            ShowPlannerError(exception);
        }
    }

    private void ClearWeek()
    {
        if (!_dialogService.Confirm(
                "Remove every meal from this week?",
                "Clear week"))
        {
            return;
        }

        try
        {
            _planService.ClearWeek(DateOnly.FromDateTime(WeekStartDate));
        }
        catch (Exception exception)
        {
            ShowPlannerError(exception);
        }
    }

    private void OnPlanChanged(object? sender, EventArgs eventArgs)
    {
        LoadWeek();
    }

    private IReadOnlyList<PlannerDayViewModel> CreateDays(
        DateOnly weekStart,
        IReadOnlyList<PlannedMeal> savedMeals)
    {
        var days = new List<PlannerDayViewModel>();

        for (var dayOffset = 0; dayOffset < DaysInWeek; dayOffset++)
        {
            var date = weekStart.AddDays(dayOffset);
            var day = new PlannerDayViewModel(date, AvailableRecipes);

            foreach (var mealSlot in day.MealSlots)
            {
                mealSlot.LoadMeal(savedMeals.SingleOrDefault(meal =>
                    meal.Date == date && meal.Slot == mealSlot.Slot));
                mealSlot.MealChanged += OnMealChanged;
            }

            days.Add(day);
        }

        return days;
    }

    private void SaveMeal(MealSlotViewModel mealSlot)
    {
        if (mealSlot.SelectedRecipe is null)
        {
            _planService.ClearMeal(mealSlot.Date, mealSlot.Slot);
            return;
        }

        _planService.SetMeal(
            mealSlot.Date,
            mealSlot.Slot,
            mealSlot.SelectedRecipe,
            mealSlot.Servings);
    }

    private void ShowPlannerError(Exception exception)
    {
        StatusMessage = "The weekly plan could not be updated.";
        _dialogService.ShowError(
            $"The weekly plan could not be updated. {exception.Message}",
            "Planner error");
    }
}
