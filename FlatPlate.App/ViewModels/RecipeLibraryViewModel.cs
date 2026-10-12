using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Media;
using FlatPlate.App.Services;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

public sealed record RecipeCuisineOption(string Label, string? Value);

public sealed class RecipeLibraryViewModel : ViewModelBase
{
    private readonly ObservableCollection<RecipeLibraryItem> _recipes = new();
    private readonly ObservableCollection<RecipeEditDraft> _pendingDrafts = new();
    private readonly Func<string?, ImageSource?>? _loadImage;
    private string _searchText = "", _statusMessage = "Recipes have not been loaded. Saving is not available.";
    private bool _isError;
    private RecipeLibraryItem? _selectedRecipe;
    private RecipeCuisineOption _selectedCuisine = new("All cuisines", null);
    private IReadOnlyList<RecipeCuisineOption> _cuisineOptions;
    private RecipeLibraryCoordinator? _actions;

    public RecipeLibraryViewModel() : this(null) { }

    public RecipeLibraryViewModel(Func<string?, ImageSource?>? loadImage)
    {
        _loadImage = loadImage;
        _cuisineOptions = new[] { _selectedCuisine };
        Recipes = new ReadOnlyObservableCollection<RecipeLibraryItem>(_recipes);
        PendingDrafts = new ReadOnlyObservableCollection<RecipeEditDraft>(_pendingDrafts);
        VisibleRecipes = new ListCollectionView(_recipes) { Filter = MatchesFilter };
        VisibleRecipes.CollectionChanged += (_, _) =>
        {
            if (_selectedRecipe is not null && !VisibleRecipes.Contains(_selectedRecipe)) SelectedRecipe = null;
            OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyDescription));
        };
    }

    public ReadOnlyObservableCollection<RecipeLibraryItem> Recipes { get; }
    public ICollectionView VisibleRecipes { get; }
    public IReadOnlyList<RecipeCuisineOption> CuisineOptions => _cuisineOptions;
    public RecipeCuisineOption SelectedCuisine
    {
        get => _selectedCuisine;
        set { if (value is not null && _cuisineOptions.Contains(value) && SetProperty(ref _selectedCuisine, value)) VisibleRecipes.Refresh(); }
    }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value ?? "")) VisibleRecipes.Refresh(); } }
    public RecipeLibraryItem? SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (value is not null && !VisibleRecipes.Contains(value)) value = null;
            SetProperty(ref _selectedRecipe, value);
        }
    }
    public RecipeLibraryCoordinator? Actions => _actions;
    public ReadOnlyObservableCollection<RecipeEditDraft> PendingDrafts { get; }
    public bool HasPendingDrafts => _pendingDrafts.Count > 0;
    public string StatusMessage => _statusMessage;
    public bool IsError => _isError;
    public string EmptyTitle => _recipes.Count == 0 ? "No recipes yet" : "No recipes match";
    public string EmptyDescription => _recipes.Count == 0 ? "Load a catalogue to browse recipes. New recipes can be prepared as unsaved drafts." : "Change the search or cuisine filter to see more recipes.";

    public void LoadRecipes(IEnumerable<Recipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        // Build the entire snapshot first; a broken relationship must not erase valid prior data.
        var rows = recipes.Select(r =>
        {
            ArgumentNullException.ThrowIfNull(r);
            return new RecipeLibraryItem(r, _loadImage?.Invoke(r.ImagePath));
        })
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Id).ToArray();
        if (rows.GroupBy(r => r.Id).Any(g => g.Count() > 1)) throw new InvalidDataException("The recipe catalogue contains duplicate identities.");
        var selectionId = SelectedRecipe?.Id;
        var cuisineValue = _selectedCuisine.Value;
        _recipes.Clear(); foreach (var row in rows) _recipes.Add(row);
        _cuisineOptions = new[] { new RecipeCuisineOption("All cuisines", null) }.Concat(rows.Select(r => r.Cuisine)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .Select(c => new RecipeCuisineOption(string.IsNullOrWhiteSpace(c) ? "No cuisine specified" : c, c))).ToArray();
        OnPropertyChanged(nameof(CuisineOptions));
        _selectedCuisine = _cuisineOptions.FirstOrDefault(c => string.Equals(c.Value, cuisineValue, StringComparison.OrdinalIgnoreCase)) ?? _cuisineOptions[0];
        OnPropertyChanged(nameof(SelectedCuisine)); VisibleRecipes.Refresh();
        SelectedRecipe = rows.FirstOrDefault(r => r.Id == selectionId && VisibleRecipes.Contains(r));
        OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyDescription));
    }

    private bool MatchesFilter(object value) => value is RecipeLibraryItem recipe
        && recipe.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
        && (_selectedCuisine.Value is null || string.Equals(recipe.Cuisine, _selectedCuisine.Value, StringComparison.OrdinalIgnoreCase));

    internal void ConnectActions(RecipeLibraryCoordinator coordinator)
    {
        if (_actions is not null) throw new InvalidOperationException("The Recipe Library already has a coordinator.");
        _actions = coordinator; OnPropertyChanged(nameof(Actions));
    }
    internal void DisconnectActions(RecipeLibraryCoordinator coordinator)
    {
        if (ReferenceEquals(coordinator, _actions)) { _actions = null; OnPropertyChanged(nameof(Actions)); }
    }
    internal void RetainDraft(RecipeEditDraft draft) { _pendingDrafts.Add(draft); OnPropertyChanged(nameof(HasPendingDrafts)); }
    internal void SetStatus(string message, bool isError = false)
    {
        _statusMessage = message; _isError = isError;
        OnPropertyChanged(nameof(StatusMessage)); OnPropertyChanged(nameof(IsError));
    }
}
