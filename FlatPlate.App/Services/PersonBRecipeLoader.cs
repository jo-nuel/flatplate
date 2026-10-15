using System.IO;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>
/// Adds Person B's catalogue transactionally without modifying any existing recipe or price.
/// Startup activation is deliberately separate from this service.
/// </summary>
public sealed class PersonBRecipeLoader
{
    private readonly FlatPlateDbContext _context;

    public PersonBRecipeLoader(FlatPlateDbContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Person B approved these two aliases on 12 October 2026. Long grain rice and
    /// Tasty cheddar cheese retain their own names and do not inherit generic prices.
    /// All other matches ignore case only; IDs are always assigned by EF.
    /// </summary>
    public static string CanonicalName(string preparedName) => preparedName switch
    {
        "Carrot" => "Carrots",
        "Potato" => "Potatoes",
        _ => preparedName,
    };

    public PersonBRecipeLoadResult Load(PersonBRecipeCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        return AppWriteTransaction.Execute(_context, () =>
        {
            var storedIngredients = _context.Ingredients.AsNoTracking().ToList();
            var storedRecipes = _context.Recipes.AsNoTracking().Include(r => r.Ingredients).ThenInclude(i => i.Ingredient).ToList();
            if (storedIngredients.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)
                || storedRecipes.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                throw new InvalidDataException("Existing catalogue names are ambiguous; resolve duplicates before loading.");
            var byName = storedIngredients.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
            var recipesByName = storedRecipes.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
            var resolved = new Dictionary<string, Ingredient>(StringComparer.OrdinalIgnoreCase);
            var newIngredients = new List<Ingredient>();

            // Validate the entire graph before adding even one entity to the context.
            foreach (var group in catalogue.Ingredients.GroupBy(i => i.IngredientName, StringComparer.OrdinalIgnoreCase))
            {
                var definition = group.First();
                var name = CanonicalName(definition.IngredientName);
                if (!byName.TryGetValue(name, out var ingredient))
                {
                    if (definition.IngredientName is "Carrot" or "Potato")
                        throw new InvalidDataException($"Approved existing ingredient {name} is missing; do not create a replacement.");
                    ingredient = PersonBRecipeCatalogue.CreateIngredient(name, definition.SuggestedIngredientType);
                    byName.Add(name, ingredient);
                    newIngredients.Add(ingredient);
                }
                if (ingredient.GetType().Name != definition.SuggestedIngredientType)
                    throw new InvalidDataException($"Ingredient {ingredient.Name} has an incompatible existing type.");
                foreach (var line in group) _ = ingredient.ToBaseAmount(line.Amount, line.Unit);
                resolved.Add(definition.IngredientName, ingredient);
            }

            var newRecipes = new List<Recipe>();
            foreach (var definition in catalogue.Recipes)
            {
                var lines = catalogue.Ingredients.Where(i => i.RecipeKey == definition.RecipeKey).ToArray();
                if (lines.Select(i => resolved[i.IngredientName].Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != lines.Length)
                    throw new InvalidDataException($"Approved mappings produce duplicate ingredients in {definition.Name}.");
                if (recipesByName.TryGetValue(definition.Name, out var existing))
                {
                    if (!Matches(existing, definition, lines, resolved))
                        throw new InvalidDataException($"Existing recipe {existing.Name} differs from this batch. Nothing was overwritten; review the collision.");
                    continue;
                }
                var recipe = new Recipe
                {
                    Name = definition.Name,
                    Cuisine = definition.Cuisine,
                    PrepMinutes = definition.PrepMinutes,
                    BaseServings = definition.BaseServings,
                    ImagePath = definition.ImagePath,
                };
                foreach (var line in lines)
                {
                    var ingredient = resolved[line.IngredientName];
                    var association = new RecipeIngredient { Amount = line.Amount, Unit = line.Unit };
                    if (ingredient.Id == 0) association.Ingredient = ingredient;
                    else association.IngredientId = ingredient.Id;
                    recipe.Ingredients.Add(association);
                }
                newRecipes.Add(recipe);
            }
            _context.Ingredients.AddRange(newIngredients);
            _context.Recipes.AddRange(newRecipes);
            return new PersonBRecipeLoadResult(newIngredients.Count, newRecipes.Count,
                newRecipes.Sum(r => r.Ingredients.Count));
        });
    }

    private static bool Matches(Recipe existing, PersonBRecipeDefinition definition,
        IReadOnlyList<PersonBRecipeIngredientDefinition> lines, IReadOnlyDictionary<string, Ingredient> resolved) =>
        existing.Name == definition.Name && existing.Cuisine == definition.Cuisine
        && existing.PrepMinutes == definition.PrepMinutes && existing.BaseServings == definition.BaseServings
        && existing.ImagePath == definition.ImagePath && existing.Ingredients.Count == lines.Count
        && lines.All(line => existing.Ingredients.Any(i => i.IngredientId == resolved[line.IngredientName].Id
            && i.Amount == line.Amount && i.Unit == line.Unit));
}

public sealed record PersonBRecipeLoadResult(int IngredientsAdded, int RecipesAdded, int AssociationsAdded);
