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

    /// <summary>
    /// Creates the application database and connects the planner screen.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

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
        RecipeSeedData.AddMissingRecipes(_context);

        var planRepository = new Repository<PlannedMeal>(_context);
        var planService = new WeeklyPlanService(planRepository);
        var recipes = _context.Recipes
            .OrderBy(recipe => recipe.Name)
            .ToList();

        WeeklyPlanner.DataContext = new WeeklyPlannerViewModel(
            planService,
            new DialogService(),
            recipes);
    }

    /// <summary>
    /// Releases the application database when the main window closes.
    /// </summary>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _context.Dispose();
        base.OnClosed(eventArgs);
    }
}
