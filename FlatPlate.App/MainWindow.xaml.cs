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
}
