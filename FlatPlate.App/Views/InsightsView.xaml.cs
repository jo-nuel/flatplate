using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;
using FlatPlate.App.Services;
using System.Globalization;

namespace FlatPlate.App.Views;

/// Holds the Insights tab until the teammate implementation is added.
public partial class InsightsView : UserControl
{
    // ML.NET price estimator
    private IPriceEstimator? _priceEstimator;
    public InsightsView()
    {
        InitializeComponent();
        PredictPriceButton.Click += PredictPriceButton_Click;
    }

    /// Displays supermarket costs as horizontal bar charts.
    public void UpdateStoreComparison(
        IEnumerable<MergedItem> items,
        IEnumerable<Store> stores,
        IPriceProvider priceProvider)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(priceProvider);

        // Clear the previous chart
        StoreComparisonChart.Children.Clear();

        var mergedItems = items.ToList();
        var storeList = stores.ToList();

        if (mergedItems.Count == 0 || storeList.Count == 0)
        {
            StoreComparisonChart.Children.Add(
                new TextBlock
                {
                    Text = "No shopping data available.",
                    Foreground = Brushes.Gray
                });
            return;
        }

        var results = new List<(string Name, decimal Cost)>();

        // Calculate the total cost for each supermarket
        foreach (var store in storeList)
        {
            decimal totalCost = 0m;
            bool missingPrice = false;

            foreach (var item in mergedItems)
            {
                var price = priceProvider.GetPrice(
                    item.Ingredient.Id, store.Id);

                if (price == null ||
                    price.PackSize <= 0 ||
                    price.Price < 0)
                {
                    missingPrice = true;
                    break;
                }

                // Calculate the number of whole packs
                var packsNeeded = Math.Ceiling(
                    item.BaseAmount / price.PackSize);

                totalCost += packsNeeded * price.Price;
            }

            // Only include stores with complete prices
            if (!missingPrice)
            {
                results.Add((store.Name, totalCost));
            }
        }

        if (results.Count == 0)
        {
            StoreComparisonChart.Children.Add(
                new TextBlock
                {
                    Text = "No store has complete price data.",
                    Foreground = Brushes.Gray
                });
            return;
        }

        // Order stores by total cost
        results = results.OrderBy(result => result.Cost).ToList();

        var maxCost = results.Max(result => result.Cost);

        foreach (var result in results)
        {
            // Store name and price
            var label = new TextBlock
            {
                Text = $"{result.Name}: ${result.Cost:F2}",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5)
            };

            StoreComparisonChart.Children.Add(label);

            // Create a horizontal bar
            var bar = new Border
            {
                Height = 20,
                Background = result.Equals(results[0])
                    ? Brushes.SeaGreen
                    : Brushes.SteelBlue,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = maxCost > 0
                    ? Math.Max(5, (double)(result.Cost / maxCost) * 400)
                    : 5
            };

            StoreComparisonChart.Children.Add(bar);
        }

        // Show the cheapest store
        StoreComparisonChart.Children.Add(
            new TextBlock
            {
                Text = $"Cheapest Store: {results[0].Name}",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.SeaGreen,
                Margin = new Thickness(0, 15, 0, 0)
            });
    }

    /// Calculates the average potential surplus from whole-pack purchases.
    public void UpdateWasteScore(
        IEnumerable<MergedItem> items,
        int storeId,
        IPriceProvider priceProvider)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(priceProvider);

        var surplusRatios = new List<decimal>();

        foreach (var item in items)
        {
            var price = priceProvider.GetPrice(
                item.Ingredient.Id, storeId);

            // Skip ingredients without valid pack information
            if (price == null || price.PackSize <= 0 ||
                item.BaseAmount <= 0)
            {
                continue;
            }

            // Calculate the total quantity of whole packs
            var packsNeeded = Math.Ceiling(
                item.BaseAmount / price.PackSize);

            var purchasedAmount = packsNeeded * price.PackSize;

            // Calculate the potential surplus ratio
            var surplusRatio =
                (purchasedAmount - item.BaseAmount) / purchasedAmount;

            surplusRatios.Add(surplusRatio);
        }

        if (surplusRatios.Count == 0)
        {
            WasteScoreText.Text = "Not calculated";
            return;
        }

        var wasteScore = surplusRatios.Average() * 100m;

        WasteScoreText.Text = $"{wasteScore:F1}%";
    }

    /// Displays estimated ingredient spending by cuisine.
    /// Costs are calculated using recipe servings and store prices.
    public void UpdateCuisineSpending(
        IEnumerable<PlannedMeal> meals,
        int storeId,
        IPriceProvider priceProvider)
    {
        ArgumentNullException.ThrowIfNull(meals);
        ArgumentNullException.ThrowIfNull(priceProvider);

        CuisineSpendingChart.Children.Clear();

        var cuisineCosts = new Dictionary<string, decimal>();

        foreach (var meal in meals)
        {
            var recipe = meal.Recipe;

            if (recipe.BaseServings <= 0 || meal.Servings <= 0)
                continue;

            var cuisine = string.IsNullOrWhiteSpace(recipe.Cuisine)
                ? "Other"
                : recipe.Cuisine.Trim();

            var servingScale =
                (decimal)meal.Servings / recipe.BaseServings;

            decimal mealCost = 0m;
            bool missingPrice = false;

            foreach (var recipeIngredient in recipe.Ingredients)
            {
                var ingredient = recipeIngredient.Ingredient;

                // Convert the required quantity to its base unit
                var baseAmount = ingredient.ToBaseAmount(
                    recipeIngredient.Amount,
                    recipeIngredient.Unit) * servingScale;

                // Get the selected supermarket's price
                var price = priceProvider.GetPrice(
                    ingredient.Id, storeId);

                if (price == null ||
                    price.PackSize <= 0 ||
                    price.Price < 0)
                {
                    missingPrice = true;
                    break;
                }

                // Estimate the cost of the quantity used,
                // rather than charging a full pack to every recipe.
                mealCost += baseAmount / price.PackSize * price.Price;
            }

            // Do not display an incomplete meal cost
            if (missingPrice)
                continue;

            if (!cuisineCosts.ContainsKey(cuisine))
                cuisineCosts[cuisine] = 0m;

            cuisineCosts[cuisine] += mealCost;
        }

        if (cuisineCosts.Count == 0)
        {
            CuisineSpendingChart.Children.Add(new TextBlock
            {
                Text = "No cuisine spending data available.",
                Foreground = Brushes.Gray
            });
            return;
        }

        var results = cuisineCosts
            .OrderByDescending(item => item.Value)
            .ToList();

        var maxCost = results.Max(item => item.Value);

        foreach (var result in results)
        {
            // Display cuisine name and estimated cost
            CuisineSpendingChart.Children.Add(new TextBlock
            {
                Text = $"{result.Key}: ${result.Value:F2}",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 5)
            });

            // Draw a horizontal bar for each cuisine
            CuisineSpendingChart.Children.Add(new Border
            {
                Height = 20,
                Background = Brushes.SteelBlue,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = maxCost > 0
                    ? Math.Max(5, (double)(result.Value / maxCost) * 400)
                    : 5
            });
        }
    }
    /// Connects the ML.NET estimator and loads available ingredients and stores.
    public void ConnectPriceEstimator(
        IPriceEstimator estimator,
        IEnumerable<Ingredient> ingredients,
        IEnumerable<Store> stores)
    {
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(stores);

        _priceEstimator = estimator;

        // Load ingredient names
        IngredientComboBox.ItemsSource = ingredients
            .Select(ingredient => ingredient.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        // Load supermarket names
        StoreComboBox.ItemsSource = stores
            .Select(store => store.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        // Select the first available option
        IngredientComboBox.SelectedIndex =
            IngredientComboBox.Items.Count > 0 ? 0 : -1;

        StoreComboBox.SelectedIndex =
            StoreComboBox.Items.Count > 0 ? 0 : -1;
    }

    /// Predicts a product price using the trained ML.NET model.
    private void PredictPriceButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (_priceEstimator == null)
        {
            PredictedPriceText.Text =
                "Price estimator is not available.";
            return;
        }

        var ingredient = IngredientComboBox.SelectedItem as string;
        var store = StoreComboBox.SelectedItem as string;

        // Validate selected ingredient and store
        if (string.IsNullOrWhiteSpace(ingredient) ||
            string.IsNullOrWhiteSpace(store))
        {
            PredictedPriceText.Text =
                "Please select an ingredient and a store.";
            return;
        }

        // Validate pack size
        if (!float.TryParse(
                PackSizeTextBox.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var packSize) ||
            !float.IsFinite(packSize) ||
            packSize <= 0)
        {
            PredictedPriceText.Text =
                "Please enter a valid positive pack size.";
            return;
        }

        try
        {
            // Request a prediction through the interface
            var predictedPrice = _priceEstimator.PredictPrice(
                ingredient,
                store,
                packSize);

            PredictedPriceText.Text =
                $"Predicted Price: ${predictedPrice:F2}";
        }
        catch (Exception ex)
        {
            PredictedPriceText.Text =
                $"Prediction failed: {ex.Message}";
        }
    }
}
