using FlatPlate.App.Data;
using FlatPlate.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that the starter catalogue creates a complete, repeat-safe planner database.
/// </summary>
public sealed class SeedDataTests
{
    private SqliteConnection _connection = null!;
    private FlatPlateDbContext _context = null!;

    /// <summary>
    /// Creates a fresh in-memory SQLite database for each seed test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FlatPlateDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new FlatPlateDbContext(options);
        _context.Database.EnsureCreated();
    }

    /// <summary>
    /// Releases the isolated database after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Confirms that the starter data supplies the agreed planner catalogue.
    /// </summary>
    [Test]
    public void AddMissingSeedData_EmptyDatabase_CreatesStarterCatalogue()
    {
        SeedAll();

        Assert.Multiple(() =>
        {
            Assert.That(_context.Ingredients.Count(), Is.EqualTo(35));
            Assert.That(_context.Recipes.Count(), Is.EqualTo(8));
            Assert.That(_context.RecipeIngredients.Count(), Is.EqualTo(53));
            Assert.That(_context.Stores.Count(), Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Confirms that running startup seeding again does not create duplicates.
    /// </summary>
    [Test]
    public void AddMissingSeedData_CalledTwice_DoesNotDuplicateRecords()
    {
        SeedAll();
        SeedAll();

        Assert.Multiple(() =>
        {
            Assert.That(_context.Ingredients.Count(), Is.EqualTo(35));
            Assert.That(_context.Recipes.Count(), Is.EqualTo(8));
            Assert.That(_context.RecipeIngredients.Count(), Is.EqualTo(53));
            Assert.That(_context.Stores.Count(), Is.EqualTo(2));
        });
    }

    /// <summary>
    /// Confirms that every starter recipe can contribute to a shopping list.
    /// </summary>
    [Test]
    public void AddMissingRecipes_StarterRecipes_AllHaveIngredients()
    {
        SeedAll();

        var recipes = _context.Recipes
            .Include(recipe => recipe.Ingredients)
            .ToList();

        Assert.That(recipes, Has.All.Matches<FlatPlate.Core.Models.Recipe>(
            recipe => recipe.Ingredients.Count > 0));
    }

    /// <summary>
    /// Confirms that the planner offers the two supermarkets selected by the team.
    /// </summary>
    [Test]
    public void AddMissingStores_EmptyDatabase_CreatesAgreedSupermarkets()
    {
        StoreSeedData.AddMissingStores(_context);

        var storeNames = _context.Stores
            .OrderBy(store => store.Name)
            .Select(store => store.Name)
            .ToList();

        Assert.That(storeNames, Is.EqualTo(new[] { "Coles", "Woolworths" }));
    }

    private void SeedAll()
    {
        IngredientSeedData.AddMissingIngredients(_context);
        RecipeSeedData.AddMissingRecipes(_context);
        StoreSeedData.AddMissingStores(_context);
    }
}
