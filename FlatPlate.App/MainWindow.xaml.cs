using FlatPlate.App.Data;
using FlatPlate.App.Services;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Windows;
using System.Linq;

namespace FlatPlate.App;

// Provides the shared five-screen shell for the FlatPlate application.
public partial class MainWindow : Window
{
    private readonly FlatPlateDbContext _context;
    private readonly bool _ownsContext;
    private PriceBookCoordinator? _priceCoordinator;
    private RecipeLibraryCoordinator? _recipeCoordinator;

    /// <summary>
    /// Creates the application database and connects the planner and Person B screens.
    /// </summary>
    public MainWindow() : this(CreateDefaultContext(), ownsContext: true, initializeSeedData: true)
    {
    }

    // Composes the real application using one supplied context. Tests use an explicitly
    // opened in-memory context; only the parameterless startup resolves the production path.
    public MainWindow(FlatPlateDbContext context, bool ownsContext = false, bool initializeSeedData = false,
        IPriceBookInteraction? priceInteraction = null, IRecipeLibraryInteraction? recipeInteraction = null,
        Func<DateTime>? utcSaveTime = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _ownsContext = ownsContext;
        try
        {
            InitializeComponent();
            if (initializeSeedData)
            {
                _context.Database.EnsureCreated();

                // Create the Shopping List table if it does not exist
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

                System.Diagnostics.Debug.WriteLine("ShoppingListRecords table is ready.");
                }

                IngredientSeedData.AddMissingIngredients(_context);
                RecipeSeedData.AddMissingRecipes(_context);
                StoreSeedData.AddMissingStores(_context);
                PriceSeedData.AddMissingPrices(_context);
            }

            Compose(priceInteraction, recipeInteraction, utcSaveTime ?? (() => DateTime.UtcNow));
        }
        catch
        {
            _priceCoordinator?.Dispose(); _recipeCoordinator?.Dispose();
            if (_ownsContext) _context.Dispose();
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

    private void Compose(IPriceBookInteraction? priceInteraction, IRecipeLibraryInteraction? recipeInteraction, Func<DateTime> utcSaveTime)
    {
        var planRepository = new Repository<PlannedMeal>(_context);
        var planService = new WeeklyPlanService(planRepository);
        var ingredientMerger = new IngredientMerger();
        var priceProvider = new DatabasePriceProvider(_context);
        var budgetCalculator = new BudgetCalculator(priceProvider);
        // Train the ML.NET price estimator using the CSV
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
            // Train the ML.NET price estimator
            IPriceEstimator priceEstimator =
                new MlNetPriceEstimator(trainingCsvPath);

            // Load ingredients from the existing database
            var ingredients = _context.Ingredients
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToList();

            // Connect the trained model to Insights
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
        var recipesById = recipes.ToDictionary(
           recipe => recipe.Id);

        void RefreshShoppingList()
        {
            var weekStart = DateOnly.FromDateTime(
                plannerViewModel.WeekStartDate);

            var plannedMeals = planService.GetWeek(weekStart);

            // Attach fully loaded recipes before merging.
            var mealsWithRecipes = plannedMeals
                .Select(meal => new PlannedMeal
                {
                    Date = meal.Date,
                    Slot = meal.Slot,
                    RecipeId = meal.RecipeId,
                    Recipe = recipesById[meal.RecipeId],
                    Servings = meal.Servings
                })
                .ToList();

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

        // Refresh when meals or servings change.
        planService.PlanChanged += (_, _) =>
        {
            RefreshShoppingList();
        };

        // Refresh when the user changes the selected week.
        plannerViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName ==
                    nameof(WeeklyPlannerViewModel.WeekStartDate) ||
                args.PropertyName ==
                    nameof(WeeklyPlannerViewModel.SelectedStore))
            {
                RefreshShoppingList();
            }
        };

        ShoppingList.ConfigurePersistence(record =>
        {
            // Find an existing record for the same week and ingredient
            var existing = _context.ShoppingListRecords.Find(
                record.WeekStartDate,
                record.IngredientId);

            if (existing == null)
            {
                // Insert a new record
                _context.ShoppingListRecords.Add(record);
            }
            else
            {
                // Update the existing record
                existing.IsPurchased = record.IsPurchased;
                existing.AlreadyHave = record.AlreadyHave;
                existing.ActualPrice = record.ActualPrice;
            }

            try
            {
                // Save shopping record to SQLite
                _context.SaveChanges();

                System.Diagnostics.Debug.WriteLine(
                    $"Shopping record saved: Ingredient {record.IngredientId}");
            }
            catch (Exception ex)
            {
                // Log the error
                System.Diagnostics.Debug.WriteLine(
                    $"Shopping record save failed: {ex.Message}");

                // Remove the failed changes from EF Core tracking
                _context.Entry(existing ?? record).State =
                    EntityState.Detached;

                // Inform the user
                MessageBox.Show(
                    "Failed to save the shopping record. " +
                    "The latest change has not been saved.",
                    "Shopping List Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }


        },
        weekStart => _context.ShoppingListRecords
            .AsNoTracking()
            .Where(record => record.WeekStartDate == weekStart)
            .ToList(),

        // Delete shopping records that are no longer needed
        (weekStart, ingredientId) =>
        {
            var existing = _context.ShoppingListRecords.Find(
                weekStart,
                ingredientId);

            if (existing != null)
            {
                _context.ShoppingListRecords.Remove(existing);

                try
                {
                    // Save shopping changes to SQLite
                    _context.SaveChanges();

                    System.Diagnostics.Debug.WriteLine(
                        $"Shopping record saved: Ingredient {ingredientId}");
                }
                catch (Exception ex)
                {
                    // Log the database error
                    System.Diagnostics.Debug.WriteLine(
                        $"Shopping record save failed: {ex.Message}");

                    // Inform the user that the change was not saved
                    MessageBox.Show(
                        "Unable to save the shopping record. " +
                        "Your latest change may be lost after restarting the app.",
                        "Shopping List Save Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        });

        // Display the current week's shopping list at startup.
        RefreshShoppingList();

        var priceReader = new PriceBookReadService(_context);
        _priceCoordinator = new PriceBookCoordinator(new PriceBookViewModel(),
            new PriceBookDataSource(priceReader,
                () => _context.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToList(),
                () => _context.Stores.AsNoTracking().OrderBy(s => s.Name).ToList()),
            priceInteraction ?? new PriceBookInteraction(() => this),
            writer: new PriceBookWriteService(_context, utcSaveTime));
        PriceBook.Connect(_priceCoordinator);

        var recipeReader = new RecipeLibraryReadService(_context);
        var imageReader = new RecipeImageService(AppContext.BaseDirectory);
        _recipeCoordinator = new RecipeLibraryCoordinator(new RecipeLibraryViewModel(imageReader.Load),
            new RecipeLibraryDataSource(recipeReader,
                () => _context.Ingredients.AsNoTracking().OrderBy(i => i.Name).ToList()),
            recipeInteraction ?? new RecipeLibraryInteraction(() => this),
            writer: new RecipeLibraryWriteService(_context));
        RecipeLibrary.Connect(_recipeCoordinator);
    }

    // Releases the application database when the main window closes.
    protected override void OnClosed(EventArgs eventArgs)
    {
        _priceCoordinator?.Dispose();
        _recipeCoordinator?.Dispose();
        if (_ownsContext) _context.Dispose();
        base.OnClosed(eventArgs);
    }
}
