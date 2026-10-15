using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;
using FlatPlate.Core.Models;
using FlatPlate.Core.Interfaces;

namespace FlatPlate.App.Views;

// Displays the merged ingredients in the Shopping List.
public partial class ShoppingListView : UserControl
{
    private readonly Dictionary<DateOnly, Dictionary<int, ShoppingListItem>>
        _itemsByWeek = new();
    private Dictionary<int, ShoppingListItem> _shoppingItems = new();

    private string _storeComparisonMessage = "No comparison available.";
    private List<MergedItem> _currentMergedItems = new();
    private List<Store> _currentStores = new();
    private IPriceProvider? _currentPriceProvider;
    private Action<ShoppingListRecord>? _saveRecord;
    private Func<DateOnly, IEnumerable<ShoppingListRecord>>? _loadRecords;
    private Action<DateOnly, int>? _deleteRecord;

    public void ConfigurePersistence(
        Action<ShoppingListRecord> saveRecord,
        Func<DateOnly, IEnumerable<ShoppingListRecord>> loadRecords,
        Action<DateOnly, int> deleteRecord)
    {
        _saveRecord = saveRecord ??
            throw new ArgumentNullException(nameof(saveRecord));

        _loadRecords = loadRecords ??
            throw new ArgumentNullException(nameof(loadRecords));

        _deleteRecord = deleteRecord ??
            throw new ArgumentNullException(nameof(deleteRecord));
    }

    // Total amount actually paid for purchased ingredients
    public decimal ActualSpending =>
    _shoppingItems.Values
        .Where(item =>
            item.IsPurchased &&
            !item.AlreadyHave &&
            item.ActualPrice.HasValue &&
            item.ActualPrice.Value >= 0)
        .Sum(item => item.ActualPrice ?? 0m);

    public ShoppingListView()
    {
        InitializeComponent();

        CompareStoresButton.Click += CompareStoresButton_Click;
    }

    // Loads the merged ingredients into the shopping list table.
    public void LoadShoppingList(
        IEnumerable<MergedItem> items,
        DateOnly weekStart)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Get the shopping records for the selected week
        if (!_itemsByWeek.TryGetValue(weekStart, out var weekItems))
        {
            weekItems = new Dictionary<int, ShoppingListItem>();

            // Restore saved shopping records from SQLite
            if (_loadRecords != null)
            {
                var savedRecords = _loadRecords(weekStart);

                foreach (var record in savedRecords)
                {
                    weekItems[record.IngredientId] = new ShoppingListItem
                    {
                        IsPurchased = record.IsPurchased,
                        AlreadyHave = record.AlreadyHave,
                        ActualPrice = record.ActualPrice
                    };
                }
            }

            _itemsByWeek[weekStart] = weekItems;
        }
        // Use the selected week's shopping records
        _shoppingItems = weekItems;

        // Get the ingredients currently required by this week's plan
        var currentItems = items.ToList();

        var currentIngredientIds = currentItems
            .Select(item => item.Ingredient.Id)
            .ToHashSet();

        // Remove shopping records for ingredients no longer in the plan
        foreach (var ingredientId in _shoppingItems.Keys.ToList())
        {
            if (!currentIngredientIds.Contains(ingredientId))
            {
                // Delete the record from SQLite
                _deleteRecord?.Invoke(weekStart, ingredientId);

                // Remove it from the current week's memory
                _shoppingItems.Remove(ingredientId);
            }
        }

        var shoppingItems = currentItems.Select(item =>
        {
            var ingredientId = item.Ingredient.Id;

            // Reuse the previous shopping status if available
            _shoppingItems.TryGetValue(ingredientId, out var previous);

            var shoppingItem = new ShoppingListItem
            {
                IngredientName = item.Ingredient.Name,
                RequiredAmount = item.BaseAmount,
                Aisle = item.Ingredient.Aisle.ToString(),

                Unit = item.Ingredient switch
                {
                    WeightIngredient => "g",
                    VolumeIngredient => "mL",
                    CountIngredient => "pieces",
                    _ => "Unknown"
                },

                IsPurchased = previous?.IsPurchased ?? false,
                AlreadyHave = previous?.AlreadyHave ?? false,
                ActualPrice = previous?.ActualPrice
            };

            _shoppingItems[ingredientId] = shoppingItem;

            shoppingItem.PropertyChanged += (_, args) =>
            {

                if (args.PropertyName == nameof(ShoppingListItem.IsPurchased) ||
                    args.PropertyName == nameof(ShoppingListItem.AlreadyHave) ||
                    args.PropertyName == nameof(ShoppingListItem.ActualPrice))
                {
                    RefreshActualSpending();

                    // Save valid shopping records to the database
                    if (shoppingItem.ActualPrice is null or >= 0m)
                    {
                        _saveRecord?.Invoke(new ShoppingListRecord
                        {
                            WeekStartDate = weekStart,
                            IngredientId = ingredientId,
                            IsPurchased = shoppingItem.IsPurchased,
                            AlreadyHave = shoppingItem.AlreadyHave,
                            ActualPrice = shoppingItem.ActualPrice
                        });
                    }
                }

                if (args.PropertyName == nameof(ShoppingListItem.IsPurchased) ||
                    args.PropertyName == nameof(ShoppingListItem.AlreadyHave))
                {
                    RefreshStoreComparison();
                }
            };

            return shoppingItem;
        }).ToList();


        // Create a collection view for grouping
        var collectionView =
            CollectionViewSource.GetDefaultView(shoppingItems);

        // Group shopping items by aisle
        collectionView.GroupDescriptions.Add(
            new PropertyGroupDescription("Aisle"));

        // Sort items by aisle and ingredient name
        collectionView.SortDescriptions.Add(
            new SortDescription("Aisle", ListSortDirection.Ascending));

        collectionView.SortDescriptions.Add(
            new SortDescription("IngredientName", ListSortDirection.Ascending));

        // Display the grouped collection
        ShoppingListGrid.ItemsSource = collectionView;
        RefreshActualSpending();
    }

    // Refresh the actual spending displayed on the screen
    private void RefreshActualSpending()
    {
        ActualSpendingText.Text = ActualSpending.ToString("C2");
    }

    // Refresh the store comparison when shopping status changes
    private void RefreshStoreComparison()
    {
        if (_currentPriceProvider == null)
            return;

        UpdateStoreComparison(
            _currentMergedItems,
            _currentStores,
            _currentPriceProvider);
    }

    // Display the cheapest store on the Shopping List screen
    public void DisplayCheapestStore(Store? store, decimal cost)
    {
        if (store == null)
        {
            CheapestStoreText.Text =
                "Cheapest Store: No complete price data";
            return;
        }

        CheapestStoreText.Text =
            $"Cheapest Store: {store.Name} - ${cost:F2}";
    }

    // Show supermarket comparison when the button is clicked
    private void CompareStoresButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            _storeComparisonMessage,
            "Store Comparison",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }


    public void UpdateStoreComparison(
        IEnumerable<MergedItem> items,
        IEnumerable<Store> stores,
        IPriceProvider priceProvider)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(priceProvider);
        _currentMergedItems = items.ToList();
        _currentStores = stores.ToList();
        _currentPriceProvider = priceProvider;

        var shoppingItems = items
            .Where(item =>
                !_shoppingItems.TryGetValue(item.Ingredient.Id, out var saved)
                || (!saved.AlreadyHave && !saved.IsPurchased))
            .ToList();

        if (shoppingItems.Count == 0)
        {
            _storeComparisonMessage =
                "There are no ingredients left to buy.";
                CheapestStoreText.Text = "Cheapest Store: Nothing left to buy";
            return;
        }

        var results = new List<(string Name, decimal Cost)>();
        var missingStores = new List<string>();

        foreach (var store in stores)
        {
            decimal totalCost = 0m;
            bool hasMissingPrice = false;

            foreach (var item in shoppingItems)
            {
                var price = priceProvider.GetPrice(
                    item.Ingredient.Id, store.Id);

                if (price == null ||
                    price.PackSize <= 0 ||
                    price.Price < 0)
                {
                    hasMissingPrice = true;
                    break;
                }

                var packsNeeded = Math.Ceiling(
                    item.BaseAmount / price.PackSize);

                totalCost += packsNeeded * price.Price;
            }

            if (hasMissingPrice)
                missingStores.Add(store.Name);
            else
                results.Add((store.Name, totalCost));
        }

        if (results.Count == 0)
        {
            _storeComparisonMessage =
                "No supermarket has complete price information.";
            CheapestStoreText.Text = "Cheapest Store: No complete price data";
            return;
        }

        results = results.OrderBy(result => result.Cost).ToList();
        // Update the cheapest store shown on the Shopping List
        var cheapest = results[0];
        CheapestStoreText.Text =
            $"Cheapest Store: {cheapest.Name} - ${cheapest.Cost:F2}";

        var lines = results.Select(result =>
            $"{result.Name}: ${result.Cost:F2}");

        _storeComparisonMessage =
            "Estimated Shopping Cost:\n\n" +
            string.Join("\n", lines) +
            $"\n\nCheapest Store: {results[0].Name}";

        if (missingStores.Count > 0)
        {
            _storeComparisonMessage +=
                "\n\nIncomplete price data:\n" +
                string.Join("\n", missingStores);
        }
    }


    // Compare the cost of the shopping list across stores
    public (Store? CheapestStore, decimal CheapestCost)
        FindCheapestStore(
            IEnumerable<MergedItem> items,
            IEnumerable<Store> stores,
            IPriceProvider priceProvider)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(priceProvider);

        var shoppingItems = items.ToList();

        Store? cheapestStore = null;
        decimal cheapestCost = decimal.MaxValue;

        foreach (var store in stores)
        {
            decimal totalCost = 0m;
            bool hasMissingPrice = false;

            foreach (var item in shoppingItems)
            {
                var price = priceProvider.GetPrice(
                    item.Ingredient.Id, store.Id);

                // Skip stores with missing prices
                if (price == null)
                {
                    hasMissingPrice = true;
                    break;
                }

                if (price.PackSize <= 0 || price.Price < 0)
                {
                    hasMissingPrice = true;
                    break;
                }

                // Calculate whole packs needed
                var packsNeeded = Math.Ceiling(
                    item.BaseAmount / price.PackSize);

                totalCost += packsNeeded * price.Price;
            }

            if (!hasMissingPrice && totalCost < cheapestCost)
            {
                cheapestCost = totalCost;
                cheapestStore = store;
            }
        }

        if (cheapestStore == null)
        {
            return (null, 0m);
        }

        return (cheapestStore, cheapestCost);
    }

    // Represents one row in the shopping list.
    private class ShoppingListItem :
        System.ComponentModel.INotifyPropertyChanged,
        System.ComponentModel.IDataErrorInfo
    {
        private bool _isPurchased;
        private bool _alreadyHave;
        private decimal? _actualPrice;

        public string IngredientName { get; set; } = "";

        public decimal RequiredAmount { get; set; }

        public string Unit { get; set; } = "";

        public string Aisle { get; set; } = "";

        public event System.ComponentModel.PropertyChangedEventHandler?
            PropertyChanged;

        public bool IsPurchased
        {
            get => _isPurchased;
            set
            {
                if (_isPurchased == value)
                    return;

                _isPurchased = value;

                // Update the other status first
                if (value && _alreadyHave)
                {
                    _alreadyHave = false;
                    OnPropertyChanged(nameof(AlreadyHave));
                }

                OnPropertyChanged(nameof(IsPurchased));
            }
        }

        public bool AlreadyHave
        {
            get => _alreadyHave;
            set
            {
                if (_alreadyHave == value)
                    return;

                _alreadyHave = value;

                // Update the other status first
                if (value && _isPurchased)
                {
                    _isPurchased = false;
                    OnPropertyChanged(nameof(IsPurchased));
                }

                OnPropertyChanged(nameof(AlreadyHave));
            }
        }

        public decimal? ActualPrice
        {
            get => _actualPrice;
            set
            {
                if (_actualPrice == value)
                    return;

                _actualPrice = value;
                OnPropertyChanged(nameof(ActualPrice));
            }
        }

        // Required by IDataErrorInfo
        public string Error => string.Empty;

        // Validate actual price
        public string this[string propertyName]
        {
            get
            {
                if (propertyName == nameof(ActualPrice) &&
                    ActualPrice.HasValue &&
                    ActualPrice.Value < 0)
                {
                    return "Actual price cannot be negative.";
                }

                return string.Empty;
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(
                this,
                new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
