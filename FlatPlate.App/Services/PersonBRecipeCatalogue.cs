using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace FlatPlate.App.Services;

/// <summary>A validated, immutable recipe preparation batch; research prices are never read.</summary>
public sealed class PersonBRecipeCatalogue
{
    private PersonBRecipeCatalogue(IReadOnlyList<PersonBRecipeDefinition> recipes,
        IReadOnlyList<PersonBRecipeIngredientDefinition> ingredients)
    {
        Recipes = recipes;
        Ingredients = ingredients;
    }

    public IReadOnlyList<PersonBRecipeDefinition> Recipes { get; }
    public IReadOnlyList<PersonBRecipeIngredientDefinition> Ingredients { get; }

    /// <summary>Reads the two approved CSV schemas without changing names, amounts or validation flags.</summary>
    public static PersonBRecipeCatalogue Parse(string recipesCsv, string ingredientsCsv)
    {
        var recipes = ReadRows(recipesCsv, "RecipeKey,Name,Cuisine,PrepMinutes,BaseServings,ImagePath")
            .Select(row => new PersonBRecipeDefinition(Required(row[0]), Required(row[1]), Required(row[2]),
                Integer(row[3], allowZero: true), Integer(row[4], allowZero: false),
                row[5].Length == 0 ? null : row[5])).ToArray();
        if (recipes.Length == 0 || recipes.GroupBy(r => r.RecipeKey, StringComparer.Ordinal).Any(g => g.Count() != 1)
            || recipes.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() != 1))
            throw new InvalidDataException("Recipe keys and names must be nonempty and unique.");

        var ingredients = ReadRows(ingredientsCsv,
                "RecipeKey,IngredientName,Amount,MeasureUnit,SuggestedIngredientType,ValidationStatus")
            .Select(row => new PersonBRecipeIngredientDefinition(Required(row[0]), Required(row[1]),
                PositiveDecimal(row[2]), Unit(row[3]), Required(row[4]), Required(row[5]))).ToArray();
        var keys = recipes.Select(r => r.RecipeKey).ToHashSet(StringComparer.Ordinal);
        foreach (var line in ingredients)
        {
            if (!keys.Contains(line.RecipeKey)) throw new InvalidDataException("An ingredient refers to an unknown RecipeKey.");
            _ = CreateIngredient(line.IngredientName, line.SuggestedIngredientType).ToBaseAmount(line.Amount, line.Unit);
        }
        if (recipes.Any(r => !ingredients.Any(i => i.RecipeKey == r.RecipeKey))
            || ingredients.GroupBy(i => (i.RecipeKey, Name: i.IngredientName.ToUpperInvariant())).Any(g => g.Count() != 1)
            || ingredients.GroupBy(i => i.IngredientName, StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Select(i => i.SuggestedIngredientType).Distinct(StringComparer.Ordinal).Count() != 1))
            throw new InvalidDataException("Recipes need ingredients; duplicate names or conflicting ingredient types are not allowed.");

        return new PersonBRecipeCatalogue(Array.AsReadOnly(recipes), Array.AsReadOnly(ingredients));
    }

    internal static Ingredient CreateIngredient(string name, string type) => type switch
    {
        nameof(WeightIngredient) => new WeightIngredient { Name = name, Aisle = Aisle.Other },
        nameof(VolumeIngredient) => new VolumeIngredient { Name = name, Aisle = Aisle.Other },
        nameof(CountIngredient) => new CountIngredient { Name = name, Aisle = Aisle.Other },
        _ => throw new InvalidDataException($"Unsupported ingredient type: {type}."),
    };

    private static List<string[]> ReadRows(string csv, string header)
    {
        ArgumentNullException.ThrowIfNull(csv);
        using var reader = new StringReader(csv.TrimStart('\uFEFF'));
        using var parser = new TextFieldParser(reader) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(",");
        try
        {
            var columns = header.Split(',');
            if (parser.EndOfData || !parser.ReadFields()!.SequenceEqual(columns, StringComparer.Ordinal))
                throw new InvalidDataException($"Expected the exact header: {header}.");
            var rows = new List<string[]>();
            while (!parser.EndOfData)
            {
                var line = parser.LineNumber;
                var fields = parser.ReadFields()!;
                if (fields.Length != columns.Length) throw new InvalidDataException($"CSV line {line} has an incorrect field count.");
                rows.Add(fields);
            }
            return rows;
        }
        catch (MalformedLineException error)
        {
            throw new InvalidDataException($"Malformed recipe CSV at line {error.LineNumber}.", error);
        }
    }

    private static string Required(string text) => string.IsNullOrWhiteSpace(text) || text != text.Trim()
        ? throw new InvalidDataException("Required values must be nonempty and have no surrounding whitespace.") : text;

    private static int Integer(string text, bool allowZero) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && (allowZero ? value >= 0 : value > 0)
            ? value : throw new InvalidDataException("Servings must be a positive integer; prep minutes must be a nonnegative integer.");

    private static MeasureUnit Unit(string text) => Enum.GetNames<MeasureUnit>().Contains(text, StringComparer.Ordinal)
        && Enum.TryParse<MeasureUnit>(text, out var unit) ? unit : throw new InvalidDataException($"Unsupported MeasureUnit: {text}.");

    private static decimal PositiveDecimal(string text)
    {
        if (!Regex.IsMatch(text, @"^[0-9]+(?:\.[0-9]+)?$", RegexOptions.CultureInvariant)
            || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0 || Normalise(text) != Normalise(amount.ToString(CultureInfo.InvariantCulture)))
            throw new InvalidDataException("Ingredient amounts must be positive, exactly representable invariant decimals.");
        return amount;
    }

    private static string Normalise(string text)
    {
        var parts = text.Split('.');
        var integer = parts[0].TrimStart('0');
        var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : "";
        return (integer.Length == 0 ? "0" : integer) + (fraction.Length == 0 ? "" : "." + fraction);
    }
}

public sealed record PersonBRecipeDefinition(string RecipeKey, string Name, string Cuisine,
    int PrepMinutes, int BaseServings, string? ImagePath);

public sealed record PersonBRecipeIngredientDefinition(string RecipeKey, string IngredientName,
    decimal Amount, MeasureUnit Unit, string SuggestedIngredientType, string ValidationStatus);
