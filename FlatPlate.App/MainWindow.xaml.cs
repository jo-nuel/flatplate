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
/// Provides the shared five-screen shell for the FlatPlate application.
/// </summary>
public partial class MainWindow : Window
{
    private readonly FlatPlateDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly AuthenticationSession _session;
    private readonly MainWindowViewModel _mainViewModel;

    /// <summary>
    /// Creates the application database and connects the planner screen.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

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

        var databaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlatPlate");
        Directory.CreateDirectory(databaseDirectory);

        var databasePath = Path.Combine(databaseDirectory, "flatplate.db");
        var databaseOptions = new DbContextOptionsBuilder<FlatPlateDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        _context = new FlatPlateDbContext(databaseOptions);
        _context.Database.EnsureCreated();
        IngredientSeedData.AddMissingIngredients(_context);
        RecipeSeedData.AddMissingRecipes(_context);
        StoreSeedData.AddMissingStores(_context);
        PriceSeedData.AddMissingPrices(_context);

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
    }

    /// <summary>
    /// Releases the application database when the main window closes.
    /// </summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _mainViewModel.Dispose();
        _session.Dispose();
        _httpClient.Dispose();
        _context.Dispose();
        base.OnClosed(eventArgs);
    }

    private static Uri ResolveApiBaseAddress()
    {
        const string defaultAddress = "http://localhost:7071/api/";
        var configuredAddress = Environment.GetEnvironmentVariable(
            "FLATPLATE_API_BASE_URL");
        var address = string.IsNullOrWhiteSpace(configuredAddress)
            ? defaultAddress
            : configuredAddress.Trim();

        if (!address.EndsWith('/'))
        {
            address += '/';
        }

        return Uri.TryCreate(address, UriKind.Absolute, out var baseAddress)
            ? baseAddress
            : throw new InvalidOperationException(
                "FLATPLATE_API_BASE_URL must be a valid absolute URL.");
    }
}
