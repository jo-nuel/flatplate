using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.Core.Data;

/// <summary>
/// Maps FlatPlate's recipes, prices, and weekly plan to SQLite tables.
/// </summary>
public sealed class FlatPlateDbContext : DbContext
{
    /// <summary>
    /// Creates a database context using options supplied by the application or tests.
    /// </summary>
    public FlatPlateDbContext(DbContextOptions<FlatPlateDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<PriceEntry> PriceEntries => Set<PriceEntry>();

    public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();

    /// <summary>
    /// Defines keys, relationships, and the shared ingredient table.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIngredients(modelBuilder);
        ConfigureRecipes(modelBuilder);
        ConfigurePrices(modelBuilder);
        ConfigurePlannedMeals(modelBuilder);
    }

    private static void ConfigureIngredients(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ingredient>()
            .HasDiscriminator<string>("IngredientKind")
            .HasValue<WeightIngredient>("Weight")
            .HasValue<VolumeIngredient>("Volume")
            .HasValue<CountIngredient>("Count");

        modelBuilder.Entity<Ingredient>()
            .HasIndex(ingredient => ingredient.Name)
            .IsUnique();
    }

    private static void ConfigureRecipes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeIngredient>()
            .HasKey(item => new { item.RecipeId, item.IngredientId });

        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(item => item.Recipe)
            .WithMany(recipe => recipe.Ingredients)
            .HasForeignKey(item => item.RecipeId);

        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(item => item.Ingredient)
            .WithMany(ingredient => ingredient.RecipeIngredients)
            .HasForeignKey(item => item.IngredientId);
    }

    private static void ConfigurePrices(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PriceEntry>()
            .HasKey(entry => new { entry.IngredientId, entry.StoreId });

        modelBuilder.Entity<PriceEntry>()
            .HasOne(entry => entry.Ingredient)
            .WithMany(ingredient => ingredient.Prices)
            .HasForeignKey(entry => entry.IngredientId);

        modelBuilder.Entity<PriceEntry>()
            .HasOne(entry => entry.Store)
            .WithMany(store => store.Prices)
            .HasForeignKey(entry => entry.StoreId);
    }

    private static void ConfigurePlannedMeals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlannedMeal>()
            .HasIndex(meal => new { meal.Date, meal.Slot })
            .IsUnique();

        modelBuilder.Entity<PlannedMeal>()
            .HasOne(meal => meal.Recipe)
            .WithMany(recipe => recipe.PlannedMeals)
            .HasForeignKey(meal => meal.RecipeId);
    }
}
