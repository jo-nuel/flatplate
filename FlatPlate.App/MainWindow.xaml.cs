using FlatPlate.App.Data;
using FlatPlate.App.Services;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Windows;

namespace FlatPlate.App;

/// <summary>
/// Provides the shared five-screen shell for the FlatPlate application.
/// </summary>
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

    /// <summary>
    /// Composes the real application using one supplied context. Tests use an explicitly
    /// opened in-memory context; only the parameterless startup resolves the production path.
    /// </summary>
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
        var budgetCalculator = new BudgetCalculator(
            new DatabasePriceProvider(_context));
        var recipes = _context.Recipes
            .Include(recipe => recipe.Ingredients)
            .ThenInclude(item => item.Ingredient)
            .OrderBy(recipe => recipe.Name)
            .ToList();
        var stores = _context.Stores
            .OrderBy(store => store.Name)
            .ToList();

        WeeklyPlanner.DataContext = new WeeklyPlannerViewModel(
            planService,
            ingredientMerger,
            budgetCalculator,
            new DialogService(),
            recipes,
            stores);

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

    /// <summary>
    /// Releases the application database when the main window closes.
    /// </summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _priceCoordinator?.Dispose();
        _recipeCoordinator?.Dispose();
        if (_ownsContext) _context.Dispose();
        base.OnClosed(eventArgs);
    }
}
