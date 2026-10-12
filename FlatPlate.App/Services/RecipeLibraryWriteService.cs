using System.IO;
using FlatPlate.App.ViewModels;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FlatPlate.App.Services;

/// <summary>Transactional recipe aggregate writes; no deletion or ID reassignment.</summary>
public sealed class RecipeLibraryWriteService
{
    internal FlatPlateDbContext Context { get; }
    public RecipeLibraryWriteService(FlatPlateDbContext context) => Context = context ?? throw new ArgumentNullException(nameof(context));

    public int Save(RecipeEditDraft draft, RecipeLibraryItem? expectedOriginal = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (!Enum.IsDefined(draft.Mode) || string.IsNullOrWhiteSpace(draft.Name) || draft.Cuisine is null
            || draft.BaseServings <= 0 || draft.PrepMinutes < 0 || draft.Ingredients is null || draft.Ingredients.Count == 0
            || draft.Ingredients.Any(i => i is null || i.IngredientId <= 0 || i.Amount <= 0m)
            || draft.Ingredients.GroupBy(i => i.IngredientId).Any(g => g.Count() != 1))
            throw new InvalidDataException("The recipe draft is invalid or has duplicate ingredients. Correct it before saving.");
        if (draft.Mode == RecipeEditMode.Add && (draft.RecipeId is not null || expectedOriginal is not null))
            throw new InvalidDataException("A new recipe must not provide an existing recipe ID.");
        if (draft.Mode == RecipeEditMode.Edit && (draft.RecipeId is null || expectedOriginal is null || draft.RecipeId != expectedOriginal.Id))
            throw new InvalidDataException("The existing recipe identity must be preserved.");

        var saved = AppWriteTransaction.Execute(Context, () =>
        {
            var ids = draft.Ingredients.Select(i => i.IngredientId).ToArray();
            var ingredients = Context.Ingredients.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionary(i => i.Id);
            if (ingredients.Count != ids.Length || draft.Ingredients.Any(i => !RecipeDraftValidation.CanConvert(ingredients[i.IngredientId], i.Amount, i.Unit)))
                throw new InvalidDataException("An ingredient or measurement is no longer valid. Refresh the catalogue.");
            Recipe entity;
            if (draft.Mode == RecipeEditMode.Add)
            {
                entity = new Recipe();
                ApplyMetadata(entity, draft);
                foreach (var line in draft.Ingredients)
                    entity.Ingredients.Add(new RecipeIngredient { IngredientId = line.IngredientId, Amount = line.Amount, Unit = line.Unit });
                Context.Recipes.Add(entity);
            }
            else
            {
                var current = Context.Recipes.AsNoTracking().Include(r => r.Ingredients).ThenInclude(i => i.Ingredient)
                    .SingleOrDefault(r => r.Id == draft.RecipeId);
                if (current is null) throw new InvalidDataException("This recipe no longer exists. Refresh before editing.");
                var actual = new RecipeLibraryItem(current);
                if (!Matches(actual, expectedOriginal!)) throw new InvalidDataException("This recipe changed since the dialog opened. Refresh and review it.");
                if (MatchesDraft(actual, draft)) throw new InvalidDataException("Change a recipe value before saving.");
                entity = Context.Recipes.Single(r => r.Id == draft.RecipeId);
                Context.Entry(entity).Reload();
                var oldLines = Context.RecipeIngredients.Where(i => i.RecipeId == entity.Id).ToList();
                foreach (var line in oldLines) Context.Entry(line).Reload();
                ApplyMetadata(entity, draft);
                foreach (var old in oldLines)
                {
                    var replacement = draft.Ingredients.SingleOrDefault(i => i.IngredientId == old.IngredientId);
                    if (replacement is null) Context.RecipeIngredients.Remove(old);
                    else { old.Amount = replacement.Amount; old.Unit = replacement.Unit; }
                }
                foreach (var line in draft.Ingredients.Where(i => oldLines.All(old => old.IngredientId != i.IngredientId)))
                    Context.RecipeIngredients.Add(new RecipeIngredient { RecipeId = entity.Id, IngredientId = line.IngredientId, Amount = line.Amount, Unit = line.Unit });
            }
            return entity;
        });
        return saved.Id;
    }

    private static void ApplyMetadata(Recipe entity, RecipeEditDraft draft)
    {
        entity.Name = draft.Name; entity.Cuisine = draft.Cuisine; entity.PrepMinutes = draft.PrepMinutes;
        entity.BaseServings = draft.BaseServings; entity.ImagePath = draft.ImagePath;
    }
    private static bool Matches(RecipeLibraryItem actual, RecipeLibraryItem expected) => actual.Id == expected.Id
        && actual.Name == expected.Name && actual.Cuisine == expected.Cuisine && actual.PrepMinutes == expected.PrepMinutes
        && actual.BaseServings == expected.BaseServings && actual.ImagePath == expected.ImagePath
        && actual.Ingredients.OrderBy(i => i.IngredientId).SequenceEqual(expected.Ingredients.OrderBy(i => i.IngredientId));
    private static bool MatchesDraft(RecipeLibraryItem actual, RecipeEditDraft draft) => actual.Name == draft.Name
        && actual.Cuisine == draft.Cuisine && actual.PrepMinutes == draft.PrepMinutes && actual.BaseServings == draft.BaseServings && actual.ImagePath == draft.ImagePath
        && actual.Ingredients.OrderBy(i => i.IngredientId).Select(i => new RecipeIngredientDraft(i.IngredientId, i.Amount, i.Unit))
            .SequenceEqual(draft.Ingredients.OrderBy(i => i.IngredientId));
}
