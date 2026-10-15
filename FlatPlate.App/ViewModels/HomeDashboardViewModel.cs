using System.ComponentModel;
using System.Windows.Input;
using FlatPlate.App.Authentication;

namespace FlatPlate.App.ViewModels;

/// <summary>
/// Presents the signed-in user's current planning progress and shortcuts.
/// </summary>
public sealed class HomeDashboardViewModel : ViewModelBase, IDisposable
{
    private readonly IAuthenticationSession _session;
    private readonly WeeklyPlannerViewModel _planner;

    public HomeDashboardViewModel(
        IAuthenticationSession session,
        WeeklyPlannerViewModel planner)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));

        OpenPlannerCommand = new RelayCommand(
            () => NavigateRequested?.Invoke(WorkspacePage.WeeklyPlanner));
        OpenRecipesCommand = new RelayCommand(
            () => NavigateRequested?.Invoke(WorkspacePage.RecipeLibrary));
        OpenPriceBookCommand = new RelayCommand(
            () => NavigateRequested?.Invoke(WorkspacePage.PriceBook));
        OpenShoppingListCommand = new RelayCommand(
            () => NavigateRequested?.Invoke(WorkspacePage.ShoppingList));
        OpenInsightsCommand = new RelayCommand(
            () => NavigateRequested?.Invoke(WorkspacePage.Insights));

        _session.SessionChanged += OnSessionChanged;
        _planner.PropertyChanged += OnPlannerChanged;
    }

    public event Action<WorkspacePage>? NavigateRequested;

    public string WelcomeMessage =>
        string.IsNullOrWhiteSpace(_session.CurrentUser?.DisplayName)
            ? "Welcome to FlatPlate"
            : $"Welcome back, {_session.CurrentUser.DisplayName}";

    public string WeekRangeLabel => _planner.WeekRangeLabel;

    public int PlannedMealCount => _planner.PlannedMealCount;

    public string PlanningProgressText => _planner.PlanningProgressText;

    public decimal PlanningPercentage => decimal.Round(
        PlannedMealCount * 100m / WeeklyPlannerViewModel.TotalMealSlots,
        1);

    public decimal EstimatedSpend => _planner.BudgetResult.TotalCost;

    public string BudgetVarianceText => _planner.BudgetVarianceText;

    public bool IsOverBudget => _planner.BudgetResult.IsOverBudget;

    public string SelectedStoreName => _planner.SelectedStoreName;

    public int MissingPriceCount => _planner.MissingPriceCount;

    public string NextStepTitle => PlannedMealCount switch
    {
        0 => "Start planning your week",
        WeeklyPlannerViewModel.TotalMealSlots => "Your week is fully planned",
        _ => "Keep building your week",
    };

    public string NextStepDescription => PlannedMealCount switch
    {
        0 => "Choose recipes for breakfast, lunch, and dinner to see a live grocery estimate.",
        WeeklyPlannerViewModel.TotalMealSlots =>
            "Review the estimate, then use your merged shopping list when you are ready.",
        _ => $"You have {WeeklyPlannerViewModel.TotalMealSlots - PlannedMealCount} meal slots left to fill.",
    };

    public string PlannerActionLabel => PlannedMealCount == 0
        ? "Plan my week"
        : "Continue planning";

    public ICommand OpenPlannerCommand { get; }

    public ICommand OpenRecipesCommand { get; }

    public ICommand OpenPriceBookCommand { get; }

    public ICommand OpenShoppingListCommand { get; }

    public ICommand OpenInsightsCommand { get; }

    public void Dispose()
    {
        _session.SessionChanged -= OnSessionChanged;
        _planner.PropertyChanged -= OnPlannerChanged;
    }

    private void OnSessionChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(WelcomeMessage));
    }

    private void OnPlannerChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        OnPropertyChanged(nameof(WeekRangeLabel));
        OnPropertyChanged(nameof(PlannedMealCount));
        OnPropertyChanged(nameof(PlanningProgressText));
        OnPropertyChanged(nameof(PlanningPercentage));
        OnPropertyChanged(nameof(EstimatedSpend));
        OnPropertyChanged(nameof(BudgetVarianceText));
        OnPropertyChanged(nameof(IsOverBudget));
        OnPropertyChanged(nameof(SelectedStoreName));
        OnPropertyChanged(nameof(MissingPriceCount));
        OnPropertyChanged(nameof(NextStepTitle));
        OnPropertyChanged(nameof(NextStepDescription));
        OnPropertyChanged(nameof(PlannerActionLabel));
    }
}
