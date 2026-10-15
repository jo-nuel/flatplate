using Microsoft.ML.Data;
using Microsoft.ML;

namespace FlatPlate.App.Services;

// Defines how the application requests price predictions.
public interface IPriceEstimator
{
    float PredictPrice(
        string ingredient,
        string store,
        float packSize);
}

// Represents one training record from the price CSV.
public class PriceTrainingData
{
    [LoadColumn(0)]
    public string Ingredient { get; set; } = "";

    [LoadColumn(1)]
    public string Store { get; set; } = "";

    [LoadColumn(5)]
    public float PackSize { get; set; }

    [LoadColumn(4)]
    public float Price { get; set; }
}

// Represents the predicted price returned by ML.NET.
public class PricePrediction
{
    [ColumnName("Score")]
    public float PredictedPrice { get; set; }
}
// Trains an ML.NET regression model and predicts supermarket prices.
public sealed class MlNetPriceEstimator : IPriceEstimator
{
    private readonly MLContext _mlContext;
    private readonly ITransformer _model;

    public MlNetPriceEstimator(string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);

        if (!System.IO.File.Exists(csvPath))
        {
            throw new System.IO.FileNotFoundException(
                "Training CSV file was not found.", csvPath);
        }

        // Create the ML.NET environment
        _mlContext = new MLContext(seed: 42);

        // Load training data from CSV
        var data = _mlContext.Data.LoadFromTextFile<PriceTrainingData>(
            path: csvPath,
            hasHeader: true,
            separatorChar: ',');

        // Prepare features for the regression model
        var pipeline = _mlContext.Transforms.Categorical
            .OneHotEncoding("IngredientEncoded", nameof(PriceTrainingData.Ingredient))
            .Append(_mlContext.Transforms.Categorical
                .OneHotEncoding("StoreEncoded", nameof(PriceTrainingData.Store)))
            .Append(_mlContext.Transforms.Concatenate(
                "Features",
                "IngredientEncoded",
                "StoreEncoded",
                nameof(PriceTrainingData.PackSize)))
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(PriceTrainingData.Price),
                featureColumnName: "Features"));


        // Split the dataset into training (80%) and testing (20%)
        var split = _mlContext.Data.TrainTestSplit(
            data,
            testFraction: 0.2,
            seed: 42);

        // Train the model using only training data
        _model = pipeline.Fit(split.TrainSet);

        // Make predictions on the unseen test data
        var predictions = _model.Transform(split.TestSet);

        // Evaluate the regression model
        var metrics = _mlContext.Regression.Evaluate(
            predictions,
            labelColumnName: nameof(PriceTrainingData.Price),
            scoreColumnName: "Score");

        // Display evaluation results
        System.Diagnostics.Debug.WriteLine(
            $"ML.NET MAE: {metrics.MeanAbsoluteError:F4}");

        System.Diagnostics.Debug.WriteLine(
            $"ML.NET RMSE: {metrics.RootMeanSquaredError:F4}");

        System.Diagnostics.Debug.WriteLine(
            $"ML.NET R2: {metrics.RSquared:F4}");

    }
    // Predicts the price of one supermarket product pack.
    public float PredictPrice(
        string ingredient,
        string store,
        float packSize)
    {
        if (string.IsNullOrWhiteSpace(ingredient))
            throw new ArgumentException(
                "Ingredient name is required.", nameof(ingredient));

        if (string.IsNullOrWhiteSpace(store))
            throw new ArgumentException(
                "Store name is required.", nameof(store));

        if (!float.IsFinite(packSize) || packSize <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(packSize),
                "Pack size must be a positive finite number.");

        var input = new PriceTrainingData
        {
            Ingredient = ingredient,
            Store = store,
            PackSize = packSize
        };

        var predictionEngine =
            _mlContext.Model.CreatePredictionEngine<
                PriceTrainingData, PricePrediction>(_model);

        var result = predictionEngine.Predict(input);
        System.Diagnostics.Debug.WriteLine($"ML.NET Raw Prediction: {result.PredictedPrice:F4}");
        // A supermarket price cannot be negative
        return float.IsFinite(result.PredictedPrice)
            ? Math.Max(0f, result.PredictedPrice)
            : 0f;
    }
}
