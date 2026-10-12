using System.Globalization;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Exceptions;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

public static class RecipeDraftValidation
{
    public static decimal? ParseAmount(string text)
    {
        text = text.Trim();
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) || value <= 0m) return null;
        return Normalise(text) == Normalise(value.ToString(CultureInfo.InvariantCulture)) ? value : null;
    }

    public static int? ParseInteger(string text, bool allowZero = false) =>
        int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
        && (allowZero ? value >= 0 : value > 0) ? value : null;

    public static IReadOnlyList<MeasureUnit> AllowedUnits(Ingredient? ingredient) =>
        Enum.GetValues<MeasureUnit>().Where(unit => CanConvert(ingredient, 1m, unit)).ToArray();

    public static bool CanConvert(Ingredient? ingredient, decimal amount, MeasureUnit unit)
    {
        if (ingredient is not (WeightIngredient or VolumeIngredient or CountIngredient) || !Enum.IsDefined(unit)) return false;
        try { return ingredient.ToBaseAmount(amount, unit) > 0m; }
        catch (Exception exception) when (exception is UnitConversionException or ArgumentException or OverflowException) { return false; }
    }

    private static string Normalise(string text)
    {
        var negative = text.StartsWith('-');
        if (text.StartsWith('+') || negative) text = text[1..];
        var parts = text.Split('.'); var integer = parts[0].TrimStart('0');
        if (integer.Length == 0) integer = "0";
        var fraction = parts.Length == 2 ? parts[1].TrimEnd('0') : string.Empty;
        var number = fraction.Length == 0 ? integer : integer + "." + fraction;
        return negative && number != "0" ? "-" + number : number;
    }
}
