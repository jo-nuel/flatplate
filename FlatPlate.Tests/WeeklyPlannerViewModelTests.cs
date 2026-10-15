using FlatPlate.App.Authentication;
using FlatPlate.App.Services;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Contracts.Authentication;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;

namespace FlatPlate.Tests;

/// <summary>
/// Checks the planner screen's week navigation, persistence, and budget summary.
/// </summary>
public sealed class WeeklyPlannerViewModelTests
{
    private const int StoreId = 1;

    private TestPlannedMealRepository _repository = null!;
    private TestPriceProvider _priceProvider = null!;
    private TestDialogService _dialogService = null!;
    private Recipe _recipe = null!;
    private Store _store = null!;

    /// <summary>
    /// Creates one recipe and supermarket for each planner test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _repository = new TestPlannedMealRepository();
        _priceProvider = new TestPriceProvider();
        _dialogService = new TestDialogService();
        _store = new Store { Id = StoreId, Name = "Woolworths" };
        _recipe = CreateRecipe();
    }

    /// <summary>
    /// Confirms that the planner opens on a complete Monday-to-Sunday week.
    /// </summary>
    [Test]
    public void Constructor_CreatesSevenDayWeekStartingMonday()
    {
        var viewModel = CreateViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Days, Has.Count.EqualTo(7));
            Assert.That(viewModel.Days[0].Date.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(viewModel.Days[^1].Date,
                Is.EqualTo(viewModel.Days[0].Date.AddDays(6)));
        });
    }

    /// <summary>
    /// Confirms that week navigation reloads the next seven-day period.
    /// </summary>
    [Test]
    public void NextWeekCommand_MovesPlannerForwardSevenDays()
    {
        var viewModel = CreateViewModel();
        var originalWeek = viewModel.WeekStartDate;

        viewModel.NextWeekCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.WeekStartDate,
                Is.EqualTo(originalWeek.AddDays(7)));
            Assert.That(viewModel.Days[0].Date,
                Is.EqualTo(DateOnly.FromDateTime(originalWeek.AddDays(7))));
        });
    }

    /// <summary>
    /// Confirms that backward navigation reloads the previous seven-day period.
    /// </summary>
    [Test]
    public void PreviousWeekCommand_MovesPlannerBackSevenDays()
    {
        var viewModel = CreateViewModel();
        var originalWeek = viewModel.WeekStartDate;

        viewModel.PreviousWeekCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.WeekStartDate,
                Is.EqualTo(originalWeek.AddDays(-7)));
            Assert.That(viewModel.Days[0].Date,
                Is.EqualTo(DateOnly.FromDateTime(originalWeek.AddDays(-7))));
        });
    }

    [Test]
    public void CurrentWeekCommand_ReturnsPlannerToCurrentMonday()
    {
        var viewModel = CreateViewModel();
        viewModel.NextWeekCommand.Execute(null);

        viewModel.CurrentWeekCommand.Execute(null);

        var today = DateTime.Today;
        var daysSinceMonday =
            (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var expectedMonday = today.Date.AddDays(-daysSinceMonday);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.WeekStartDate, Is.EqualTo(expectedMonday));
            Assert.That(viewModel.CurrentWeekCommand.CanExecute(null), Is.False);
        });
    }

    /// <summary>
    /// Confirms that selecting a recipe saves the meal and costs its ingredients.
    /// </summary>
    [Test]
    public void SelectRecipe_SavesMealAndUpdatesBudget()
    {
        _priceProvider.AddPrice(
            ingredientId: 1,
            storeId: StoreId,
            price: 6m,
            packSize: 500m);
        var viewModel = CreateViewModel();
        var dinner = viewModel.Days[0].MealSlots
            .Single(slot => slot.Slot == MealSlot.Dinner);

        dinner.SelectedRecipe = _recipe;

        Assert.Multiple(() =>
        {
            Assert.That(_repository.Items, Has.Count.EqualTo(1));
            Assert.That(_repository.Items[0].Recipe, Is.SameAs(_recipe));
            Assert.That(viewModel.BudgetResult.TotalCost, Is.EqualTo(6m));
            Assert.That(viewModel.BudgetResult.PercentageUsed, Is.EqualTo(4m));
            Assert.That(viewModel.PlannedMealCount, Is.EqualTo(1));
            Assert.That(viewModel.PlanningProgressText, Is.EqualTo("1 of 21 meals planned"));
            Assert.That(viewModel.SelectedStoreName, Is.EqualTo("Woolworths"));
            Assert.That(viewModel.BudgetRemaining, Is.EqualTo(144m));
            Assert.That(viewModel.DisplayPercentageUsed, Is.EqualTo(4m));
        });
    }

    [Test]
    public void HomeDashboard_TracksPlannerAndRaisesNavigationRequest()
    {
        _priceProvider.AddPrice(1, StoreId, 6m, 500m);
        var planner = CreateViewModel();
        using var session = new AuthenticationSession(TimeProvider.System);
        session.Start(new AuthenticationResponse(
            "test-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            new AuthenticatedUser(
                Guid.NewGuid(),
                "Jonathan",
                "jonathan@example.com",
                true)));
        using var dashboard = new HomeDashboardViewModel(session, planner);
        WorkspacePage? requestedPage = null;
        dashboard.NavigateRequested += page => requestedPage = page;

        planner.Days[0].MealSlots[0].SelectedRecipe = _recipe;
        dashboard.OpenPlannerCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.WelcomeMessage, Is.EqualTo("Welcome back, Jonathan"));
            Assert.That(dashboard.PlannedMealCount, Is.EqualTo(1));
            Assert.That(dashboard.PlanningPercentage, Is.EqualTo(4.8m));
            Assert.That(dashboard.EstimatedSpend, Is.EqualTo(6m));
            Assert.That(dashboard.NextStepTitle, Is.EqualTo("Keep building your week"));
            Assert.That(requestedPage, Is.EqualTo(WorkspacePage.WeeklyPlanner));
        });

        dashboard.OpenShoppingListCommand.Execute(null);
        Assert.That(requestedPage, Is.EqualTo(WorkspacePage.ShoppingList));

        dashboard.OpenInsightsCommand.Execute(null);
        Assert.That(requestedPage, Is.EqualTo(WorkspacePage.Insights));
    }

    /// <summary>
    /// Confirms that changing the budget refreshes the percentage and warning state.
    /// </summary>
    [Test]
    public void WeeklyBudget_Changed_RecalculatesOverBudgetState()
    {
        _priceProvider.AddPrice(
            ingredientId: 1,
            storeId: StoreId,
            price: 30m,
            packSize: 500m);
        var viewModel = CreateViewModel();
        viewModel.Days[0].MealSlots[0].SelectedRecipe = _recipe;

        viewModel.WeeklyBudget = 25m;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.BudgetResult.PercentageUsed, Is.EqualTo(120m));
            Assert.That(viewModel.BudgetResult.IsOverBudget, Is.True);
        });
    }

    /// <summary>
    /// Confirms that changing servings refreshes the number of packs required.
    /// </summary>
    [Test]
    public void Servings_Changed_RecalculatesBasketCost()
    {
        _priceProvider.AddPrice(
            ingredientId: 1,
            storeId: StoreId,
            price: 6m,
            packSize: 500m);
        var viewModel = CreateViewModel();
        var dinner = viewModel.Days[0].MealSlots
            .Single(slot => slot.Slot == MealSlot.Dinner);
        dinner.SelectedRecipe = _recipe;

        var reloadedDinner = viewModel.Days[0].MealSlots
            .Single(slot => slot.Slot == MealSlot.Dinner);
        reloadedDinner.Servings = 4;

        Assert.Multiple(() =>
        {
            Assert.That(_repository.Items.Single().Servings, Is.EqualTo(4));
            Assert.That(viewModel.BudgetResult.TotalCost, Is.EqualTo(12m));
        });
    }

    /// <summary>
    /// Confirms that choosing another supermarket uses its price book.
    /// </summary>
    [Test]
    public void SelectedStore_Changed_RecalculatesWithNewStorePrices()
    {
        var coles = new Store { Id = 2, Name = "Coles" };
        _priceProvider.AddPrice(1, StoreId, 6m, 500m);
        _priceProvider.AddPrice(1, coles.Id, 10m, 500m);
        var viewModel = CreateViewModel(new[] { _store, coles });
        viewModel.Days[0].MealSlots[0].SelectedRecipe = _recipe;
        Assert.That(viewModel.BudgetResult.TotalCost, Is.EqualTo(10m));

        viewModel.SelectedStore = _store;

        Assert.That(viewModel.BudgetResult.TotalCost, Is.EqualTo(6m));
    }

    /// <summary>
    /// Confirms that an unavailable ingredient price marks the estimate incomplete.
    /// </summary>
    [Test]
    public void SelectRecipe_MissingPrice_ReportsIncompleteEstimate()
    {
        var viewModel = CreateViewModel();

        viewModel.Days[0].MealSlots[0].SelectedRecipe = _recipe;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasMissingPrices, Is.True);
            Assert.That(viewModel.BudgetResult.MissingPriceItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.BudgetResult.TotalCost, Is.Zero);
        });
    }

    /// <summary>
    /// Confirms that approving Clear Week removes all planned meals.
    /// </summary>
    [Test]
    public void ClearWeekCommand_Confirmed_RemovesPlannedMeals()
    {
        var viewModel = CreateViewModel();
        viewModel.Days[0].MealSlots[0].SelectedRecipe = _recipe;

        viewModel.ClearWeekCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_dialogService.ConfirmCallCount, Is.EqualTo(1));
            Assert.That(_repository.Items, Is.Empty);
            Assert.That(viewModel.ClearWeekCommand.CanExecute(null), Is.False);
        });
    }

    /// <summary>
    /// Confirms that cancelling Clear Week preserves the current plan.
    /// </summary>
    [Test]
    public void ClearWeekCommand_Cancelled_PreservesPlannedMeals()
    {
        _dialogService.ConfirmResult = false;
        var viewModel = CreateViewModel();
        viewModel.Days[0].MealSlots[0].SelectedRecipe = _recipe;

        viewModel.ClearWeekCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_dialogService.ConfirmCallCount, Is.EqualTo(1));
            Assert.That(_repository.Items, Has.Count.EqualTo(1));
            Assert.That(viewModel.ClearWeekCommand.CanExecute(null), Is.True);
        });
    }

    private WeeklyPlannerViewModel CreateViewModel(
        IReadOnlyList<Store>? stores = null)
    {
        return new WeeklyPlannerViewModel(
            new WeeklyPlanService(_repository),
            new IngredientMerger(),
            new BudgetCalculator(_priceProvider),
            _dialogService,
            new[] { _recipe },
            stores ?? new[] { _store });
    }

    private static Recipe CreateRecipe()
    {
        var rice = new WeightIngredient { Id = 1, Name = "Rice" };
        var recipe = new Recipe
        {
            Id = 1,
            Name = "Rice bowl",
            BaseServings = 2,
        };
        recipe.Ingredients.Add(new RecipeIngredient
        {
            RecipeId = recipe.Id,
            IngredientId = rice.Id,
            Recipe = recipe,
            Ingredient = rice,
            Amount = 500m,
            Unit = MeasureUnit.Gram,
        });
        return recipe;
    }

    private sealed class TestDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; } = true;

        public int ConfirmCallCount { get; private set; }

        public bool Confirm(string message, string title)
        {
            ConfirmCallCount++;
            return ConfirmResult;
        }

        public void ShowError(string message, string title)
        {
            Assert.Fail($"Unexpected planner error: {title} - {message}");
        }
    }

    private sealed class TestPriceProvider : IPriceProvider
    {
        private readonly Dictionary<(int IngredientId, int StoreId), PriceEntry>
            _prices = new();

        public PriceEntry? GetPrice(int ingredientId, int storeId)
        {
            _prices.TryGetValue((ingredientId, storeId), out var price);
            return price;
        }

        public IReadOnlyList<PriceEntry> GetPricesForStore(int storeId)
        {
            return _prices.Values
                .Where(price => price.StoreId == storeId)
                .ToList();
        }

        public void AddPrice(
            int ingredientId,
            int storeId,
            decimal price,
            decimal packSize)
        {
            _prices[(ingredientId, storeId)] = new PriceEntry
            {
                IngredientId = ingredientId,
                StoreId = storeId,
                Price = price,
                PackSize = packSize,
            };
        }
    }

    private sealed class TestPlannedMealRepository : IRepository<PlannedMeal>
    {
        private int _nextId = 1;

        public List<PlannedMeal> Items { get; } = new();

        public IReadOnlyList<PlannedMeal> GetAll()
        {
            return Items.ToList();
        }

        public PlannedMeal? GetById(int id)
        {
            return Items.SingleOrDefault(meal => meal.Id == id);
        }

        public IReadOnlyList<PlannedMeal> Find(Func<PlannedMeal, bool> predicate)
        {
            return Items.Where(predicate).ToList();
        }

        public void Add(PlannedMeal entity)
        {
            entity.Id = _nextId++;
            Items.Add(entity);
        }

        public void Update(PlannedMeal entity)
        {
            if (!Items.Contains(entity))
            {
                throw new InvalidOperationException("Cannot update a missing meal.");
            }
        }

        public void Delete(PlannedMeal entity)
        {
            Items.Remove(entity);
        }
    }
}
