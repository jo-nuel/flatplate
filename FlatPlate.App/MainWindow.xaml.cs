using FlatPlate.App.Authentication;
using FlatPlate.App.Data;
using FlatPlate.App.Services;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace FlatPlate.App;

/// <summary>
/// Composes the authenticated application shell and its feature screens.
/// </summary>
public partial class MainWindow : Window
{
    private readonly FlatPlateDbContext _context;
    private readonly bool _ownsContext;
    private HttpClient? _httpClient;
    private AuthenticationSession? _session;
    private HomeDashboardViewModel? _homeViewModel;
    private MainWindowViewModel? _mainViewModel;
    private PriceBookCoordinator? _priceCoordinator;
    private RecipeLibraryCoordinator? _recipeCoordinator;

    /// <summary>
    /// Creates the production application with its local SQLite database.
    /// </summary>
    public MainWindow()
        : this(CreateDefaultContext(), ownsContext: true, initializeSeedData: true)
    {
    }

    /// <summary>
    /// Composes the application using a supplied context so tests can use an
    /// explicitly opened in-memory database.
    /// </summary>
    public MainWindow(
        FlatPlateDbContext context,
        bool ownsContext = false,
        bool initializeSeedData = false,
        IPriceBookInteraction? priceInteraction = null,
        IRecipeLibraryInteraction? recipeInteraction = null,
        Func<DateTime>? utcSaveTime = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _ownsContext = ownsContext;
        try
        {
            InitializeComponent();
            ComposeAuthentication();
            if (initializeSeedData)
            {
                _context.Database.EnsureCreated();

                if (_context.Database.IsSqlite())
                {
                    _context.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS "ShoppingListRecords" (
                        "WeekStartDate" TEXT NOT NULL,
                        "IngredientId" INTEGER NOT NULL,
                        "IsPurchased" INTEGER NOT NULL,
                        "AlreadyHave" INTEGER NOT NULL,
                        "ActualPrice" TEXT NULL,
                        CONSTRAINT "PK_ShoppingListRecords"
                        PRIMARY KEY ("WeekStartDate", "IngredientId")
                        );
                    """);
                }

                IngredientSeedData.AddMissingIngredients(_context);
                RecipeSeedData.AddMissingRecipes(_context);
                StoreSeedData.AddMissingStores(_context);
                PriceSeedData.AddMissingPrices(_context);
            }

            ComposeFeatures(
                priceInteraction,
                recipeInteraction,
                utcSaveTime ?? (() => DateTime.UtcNow));
        }
        catch
        {
            DisposeResources();
            throw;
        }
    }

    private static FlatPlateDbContext CreateDefaultContext()
    {
        var databaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlatPlate");
        Directory.CreateDirectory(databaseDirectory);

        var databasePath = Path.Combine(databaseDirectory, "flatplate.db");
        var databaseOptions = new DbContextOptionsBuilder<FlatPlateDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new FlatPlateDbContext(databaseOptions);
    }

    private void ComposeAuthentication()
    {
        _session = new AuthenticationSession(TimeProvider.System);
        _httpClient = new HttpClient
        {
            BaseAddress = ResolveApiBaseAddress(),
            Timeout = TimeSpan.FromSeconds(15)
        };
        var authenticationClient = new AuthenticationApiClient(_httpClient);
        AuthenticationView.DataContext = new AuthenticationViewModel(
            authenticationClient,
            _session);
        _mainViewModel = new MainWindowViewModel(_session);
        DataContext = _mainViewModel;
    }

    private void ComposeFeatures(
        IPriceBookInteraction? priceInteraction,
        IRecipeLibraryInteraction? recipeInteraction,
        Func<DateTime> utcSaveTime)
    {
        var planRepository = new Repository<PlannedMeal>(_context);
        var planService = new WeeklyPlanService(planRepository);
        var ingredientMerger = new IngredientMerger();
        var priceProvider = new DatabasePriceProvider(_context);
        var budgetCalculator = new BudgetCalculator(priceProvider);
        var trainingCsvPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Seed",
            "price-seed.csv");

        var recipes = _context.Recipes
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(item => item.Ingredient)
            .OrderBy(recipe => recipe.Name)
            .ToList();
        var stores = _context.Stores
            .OrderBy(store => store.Name)
            .ToList();

        try
        {
            IPriceEstimator priceEstimator =
                new MlNetPriceEstimator(trainingCsvPath);

            var ingredients = _context.Ingredients
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToList();

            Insights.ConnectPriceEstimator(
                priceEstimator,
                ingredients,
                stores);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"ML.NET initialization failed: {ex.Message}");
        }

        var plannerViewModel = new WeeklyPlannerViewModel(
            planService,
            ingredientMerger,
            budgetCalculator,
            new DialogService(),
            recipes,
            stores);

        WeeklyPlanner.DataContext = plannerViewModel;
        _homeViewModel = new HomeDashboardViewModel(_session!, plannerViewModel);
        _homeViewModel.NavigateRequested += NavigateToPage;
        Home.DataContext = _homeViewModel;

        var recipesById = recipes.ToDictionary(
            recipe => recipe.Id);
        var reportedMissingRecipeIds = new HashSet<int>();

        void RefreshShoppingList()
        {
            var weekStart = DateOnly.FromDateTime(
                plannerViewModel.WeekStartDate);

            var plannedMeals = planService.GetWeek(weekStart);

            var mealsWithRecipes = new List<PlannedMeal>();
            foreach (var meal in plannedMeals)
            {
                if (!recipesById.TryGetValue(meal.RecipeId, out var recipe))
                {
                    if (reportedMissingRecipeIds.Add(meal.RecipeId))
                    {
                        MessageBox.Show(
                            "A planned meal refers to a recipe that is no longer available. " +
                            "That meal has been excluded from Shopping List and Insights.",
                            "Unavailable Recipe",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    continue;
                }

                mealsWithRecipes.Add(new PlannedMeal
                {
                    Date = meal.Date,
                    Slot = meal.Slot,
                    RecipeId = meal.RecipeId,
                    Recipe = recipe,
                    Servings = meal.Servings
                });
            }

            var mergedItems = ingredientMerger.Merge(
                mealsWithRecipes);

            // Display the shopping list for the selected week
            ShoppingList.LoadShoppingList(mergedItems, weekStart);

            // Prepare detailed store comparison for the button
            ShoppingList.UpdateStoreComparison(
                mergedItems,
                stores,
                priceProvider);

            // Update Insights store comparison chart
            Insights.UpdateStoreComparison(
                mergedItems,
                stores,
                priceProvider);

            // Update Insights using the selected supermarket
            if (plannerViewModel.SelectedStore != null)
            {
                var selectedStoreId = plannerViewModel.SelectedStore.Id;

                // Update potential waste score
                Insights.UpdateWasteScore(
                    mergedItems,
                    selectedStoreId,
                    priceProvider);

                // Update estimated spending by cuisine
                Insights.UpdateCuisineSpending(
                    mealsWithRecipes,
                    selectedStoreId,
                    priceProvider);
            }
        }

        void TryRefreshFeatures()
        {
            try
            {
                RefreshShoppingList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Shopping and insights refresh failed: {ex.Message}");
                MessageBox.Show(
                    "Shopping List and Insights could not be refreshed. " +
                    "Please try again.",
                    "Refresh Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Refresh when meals or servings change.
        planService.PlanChanged += (_, _) =>
        {
            TryRefreshFeatures();
        };

        // Refresh when the user changes the selected week.
        plannerViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName ==
                    nameof(WeeklyPlannerViewModel.WeekStartDate) ||
                args.PropertyName ==
                    nameof(WeeklyPlannerViewModel.SelectedStore))
            {
                TryRefreshFeatures();
            }
        };

        void SaveShoppingRecord(ShoppingListRecord record)
        {
            ShoppingListRecord? existing = null;

            try
            {
                existing = _context.ShoppingListRecords.Find(
                    record.WeekStartDate,
                    record.IngredientId);

                if (existing is null)
                {
                    _context.ShoppingListRecords.Add(record);
                }
                else
                {
                    existing.IsPurchased = record.IsPurchased;
                    existing.AlreadyHave = record.AlreadyHave;
                    existing.ActualPrice = record.ActualPrice;
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Shopping record save failed: {ex.Message}");

                var failedEntry = _context.Entry(existing ?? record);
                if (existing is null)
                {
                    failedEntry.State = EntityState.Detached;
                }
                else
                {
                    failedEntry.CurrentValues.SetValues(failedEntry.OriginalValues);
                    failedEntry.State = EntityState.Unchanged;
                }

                MessageBox.Show(
                    "Failed to save the shopping record. " +
                    "The latest change has not been saved.",
                    "Shopping List Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        IEnumerable<ShoppingListRecord> LoadShoppingRecords(DateOnly weekStart)
        {
            try
            {
                return _context.ShoppingListRecords
                    .AsNoTracking()
                    .Where(record => record.WeekStartDate == weekStart)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Shopping records could not be loaded: {ex.Message}");
                MessageBox.Show(
                    "Saved shopping progress could not be loaded. " +
                    "The current list will start without saved progress.",
                    "Shopping List Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return Array.Empty<ShoppingListRecord>();
            }
        }

        void DeleteShoppingRecord(DateOnly weekStart, int ingredientId)
        {
            ShoppingListRecord? existing = null;
            try
            {
                existing = _context.ShoppingListRecords.Find(
                    weekStart,
                    ingredientId);

                if (existing is null)
                {
                    return;
                }

                _context.ShoppingListRecords.Remove(existing);
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Shopping record delete failed: {ex.Message}");

                if (existing is not null)
                {
                    _context.Entry(existing).State = EntityState.Unchanged;
                }

                MessageBox.Show(
                    "The shopping record could not be removed. " +
                    "Your saved progress has been kept.",
                    "Shopping List Delete Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        ShoppingList.ConfigurePersistence(
            SaveShoppingRecord,
            LoadShoppingRecords,
            DeleteShoppingRecord);

        // Display the current week's shopping list at startup.
        TryRefreshFeatures();

        var priceReader = new PriceBookReadService(_context);
        _priceCoordinator = new PriceBookCoordinator(
            new PriceBookViewModel(),
            new PriceBookDataSource(
                priceReader,
                () => _context.Ingredients
                    .AsNoTracking()
                    .OrderBy(ingredient => ingredient.Name)
                    .ToList(),
                () => _context.Stores
                    .AsNoTracking()
                    .OrderBy(store => store.Name)
                    .ToList()),
            priceInteraction ?? new PriceBookInteraction(() => this),
            writer: new PriceBookWriteService(_context, utcSaveTime));
        PriceBook.Connect(_priceCoordinator);

        var recipeReader = new RecipeLibraryReadService(_context);
        var imageReader = new RecipeImageService(AppContext.BaseDirectory);
        _recipeCoordinator = new RecipeLibraryCoordinator(
            new RecipeLibraryViewModel(imageReader.Load),
            new RecipeLibraryDataSource(
                recipeReader,
                () => _context.Ingredients
                    .AsNoTracking()
                    .OrderBy(ingredient => ingredient.Name)
                    .ToList()),
            recipeInteraction ?? new RecipeLibraryInteraction(() => this),
            writer: new RecipeLibraryWriteService(_context));
        RecipeLibrary.Connect(_recipeCoordinator);
    }

    /// <summary>
    /// Releases application services and the owned database context.
    /// </summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        DisposeResources();
        base.OnClosed(eventArgs);
    }

    private void DisposeResources()
    {
        if (_homeViewModel is not null)
        {
            _homeViewModel.NavigateRequested -= NavigateToPage;
            _homeViewModel.Dispose();
        }

        _priceCoordinator?.Dispose();
        _recipeCoordinator?.Dispose();
        _mainViewModel?.Dispose();
        _session?.Dispose();
        _httpClient?.Dispose();

        if (_ownsContext)
        {
            _context.Dispose();
        }
    }

    private static Uri ResolveApiBaseAddress()
        => AuthenticationApiEndpoint.Resolve(
            Environment.GetEnvironmentVariable(
                AuthenticationApiEndpoint.EnvironmentVariableName));

    private void NavigateToPage(WorkspacePage page)
    {
        WorkspaceTabs.SelectedIndex = page switch
        {
            WorkspacePage.Home => 0,
            WorkspacePage.RecipeLibrary => 1,
            WorkspacePage.PriceBook => 2,
            WorkspacePage.WeeklyPlanner => 3,
            _ => WorkspaceTabs.SelectedIndex,
        };
    }
}
