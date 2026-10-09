using FlatPlate.Core.Enums;
using FlatPlate.Core.Exceptions;
using FlatPlate.Core.Models;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that each ingredient type converts recipe units to its base unit.
/// </summary>
public sealed class UnitConverterTests
{
    /// <summary>
    /// Confirms that kilograms are converted to grams for weight ingredients.
    /// </summary>
    [Test]
    public void WeightIngredient_Kilograms_ConvertsToGrams()
    {
        Ingredient ingredient = new WeightIngredient { Name = "Rice" };

        var result = ingredient.ToBaseAmount(2m, MeasureUnit.Kilogram);

        Assert.That(result, Is.EqualTo(2000m));
    }

    /// <summary>
    /// Confirms the Australian kitchen measurements used by volume ingredients.
    /// </summary>
    [TestCase(2, MeasureUnit.Litre, 2000)]
    [TestCase(2, MeasureUnit.Cup, 500)]
    [TestCase(2, MeasureUnit.Tablespoon, 40)]
    [TestCase(2, MeasureUnit.Teaspoon, 10)]
    public void VolumeIngredient_SupportedUnit_ConvertsToMillilitres(
        int amount,
        MeasureUnit unit,
        int expected)
    {
        Ingredient ingredient = new VolumeIngredient { Name = "Milk" };

        var result = ingredient.ToBaseAmount(amount, unit);

        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>
    /// Confirms that density allows a volume of a dry ingredient to become grams.
    /// </summary>
    [Test]
    public void WeightIngredient_CupWithDensity_ConvertsToGrams()
    {
        Ingredient ingredient = new WeightIngredient
        {
            Name = "Rice",
            DensityGramsPerMl = 0.8m,
        };

        var result = ingredient.ToBaseAmount(1m, MeasureUnit.Cup);

        Assert.That(result, Is.EqualTo(200m));
    }

    /// <summary>
    /// Confirms that count ingredients retain measurements expressed as pieces.
    /// </summary>
    [Test]
    public void CountIngredient_Pieces_ReturnsSameAmount()
    {
        Ingredient ingredient = new CountIngredient { Name = "Eggs" };

        var result = ingredient.ToBaseAmount(6m, MeasureUnit.Piece);

        Assert.That(result, Is.EqualTo(6m));
    }

    /// <summary>
    /// Confirms that a count ingredient rejects a weight measurement.
    /// </summary>
    [Test]
    public void CountIngredient_Grams_ThrowsUnitConversionException()
    {
        Ingredient ingredient = new CountIngredient { Name = "Eggs" };

        Assert.That(
            () => ingredient.ToBaseAmount(100m, MeasureUnit.Gram),
            Throws.TypeOf<UnitConversionException>());
    }

    /// <summary>
    /// Confirms that converting volume to weight requires an ingredient density.
    /// </summary>
    [Test]
    public void WeightIngredient_CupWithoutDensity_ThrowsUnitConversionException()
    {
        Ingredient ingredient = new WeightIngredient { Name = "Rice" };

        Assert.That(
            () => ingredient.ToBaseAmount(1m, MeasureUnit.Cup),
            Throws.TypeOf<UnitConversionException>());
    }

    /// <summary>
    /// Confirms that ingredient amounts must always be greater than zero.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    public void Ingredient_NonPositiveAmount_ThrowsArgumentOutOfRangeException(
        int amount)
    {
        Ingredient ingredient = new WeightIngredient { Name = "Rice" };

        Assert.That(
            () => ingredient.ToBaseAmount(amount, MeasureUnit.Gram),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}
