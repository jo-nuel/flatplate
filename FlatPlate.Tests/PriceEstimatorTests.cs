using System;
using System.IO;
using FlatPlate.App.Services;

namespace FlatPlate.Tests;

// Tests the ML.NET supermarket price estimator.
public class PriceEstimatorTests
{
    private MlNetPriceEstimator _estimator = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        // Locate the existing training CSV
        var csvPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "Seed",
            "price-seed.csv");

        Assert.That(File.Exists(csvPath), Is.True,
            $"Training CSV was not found: {csvPath}");

        // Train once for all tests
        _estimator = new MlNetPriceEstimator(csvPath);
    }

    [Test]
    public void PredictPrice_ValidInput_ReturnsNonNegativePrice()
    {
        var result = _estimator.PredictPrice(
            "Rice", "Coles", 1000f);

        Assert.That(float.IsFinite(result), Is.True);
        Assert.That(result, Is.GreaterThanOrEqualTo(0f));
    }

    [TestCase(0f)]
    [TestCase(-5f)]
    public void PredictPrice_InvalidPackSize_ThrowsException(float packSize)
    {
        Assert.That(
            () => _estimator.PredictPrice("Rice", "Coles", packSize),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void PredictPrice_EmptyIngredient_ThrowsException()
    {
        Assert.That(
            () => _estimator.PredictPrice("", "Coles", 1000f),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void PredictPrice_EmptyStore_ThrowsException()
    {
        Assert.That(
            () => _estimator.PredictPrice("Rice", "", 1000f),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_MissingCsv_ThrowsFileNotFoundException()
    {
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "flatplate-nonexistent-price-training.csv");

        Assert.That(
            () => new MlNetPriceEstimator(missingPath),
            Throws.TypeOf<FileNotFoundException>());
    }
}
