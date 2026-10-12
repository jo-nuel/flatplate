using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Uses the host's existing context to read recipes with their ingredient relationships.</summary>
public sealed class RecipeLibraryReadService
{
    private readonly FlatPlateDbContext _context;

    public RecipeLibraryReadService(FlatPlateDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IReadOnlyList<Recipe> LoadAllRecipes() => _context.Recipes
        .AsNoTracking().Include(r => r.Ingredients).ThenInclude(line => line.Ingredient)
        .OrderBy(r => r.Name).ThenBy(r => r.Id).ToList();
}
