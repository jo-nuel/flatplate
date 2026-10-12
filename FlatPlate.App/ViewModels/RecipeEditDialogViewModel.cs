using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using FlatPlate.Core.Models;

namespace FlatPlate.App.ViewModels;

public sealed class RecipeEditDialogViewModel : ViewModelBase
{
    private readonly RecipeLibraryItem? _original;
    private bool _confirmAttempted;
    private string _nameText = "", _cuisineText = "", _servingsText = "", _prepMinutesText = "0", _imagePathText = "";
    private RecipeIngredientEditLine? _selectedLine;

    private RecipeEditDialogViewModel(IReadOnlyList<Ingredient> ingredients, RecipeLibraryItem? original)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        if (ingredients.Any(i => i is null || i.Id <= 0 || string.IsNullOrWhiteSpace(i.Name)) || ingredients.GroupBy(i => i.Id).Any(g => g.Count() > 1))
            throw new ArgumentException("Ingredient options must have existing unique IDs and names.", nameof(ingredients));
        IngredientOptions = ingredients.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        _original = original;
        AddLineCommand = new RelayCommand(() =>
        {
            var line = new RecipeIngredientEditLine(IngredientOptions); Lines.Add(line); SelectedLine = line;
        });
        RemoveLineCommand = new RelayCommand(() => { if (SelectedLine is not null) Lines.Remove(SelectedLine); SelectedLine = null; }, () => SelectedLine is not null && Lines.Contains(SelectedLine));
        ConfirmCommand = new RelayCommand(Confirm);
        Lines.CollectionChanged += (_, args) =>
        {
            if (args.OldItems is not null) foreach (RecipeIngredientEditLine line in args.OldItems) line.PropertyChanged -= OnLineChanged;
            if (args.NewItems is not null) foreach (RecipeIngredientEditLine line in args.NewItems) line.PropertyChanged += OnLineChanged;
            Changed();
        };
        if (original is null) AddLineCommand.Execute(null);
        else
        {
            _nameText = original.Name; _cuisineText = original.Cuisine;
            _servingsText = original.BaseServings.ToString(CultureInfo.InvariantCulture);
            _prepMinutesText = original.PrepMinutes.ToString(CultureInfo.InvariantCulture); _imagePathText = original.ImagePath ?? "";
            foreach (var source in original.Ingredients)
            {
                var line = new RecipeIngredientEditLine(IngredientOptions)
                {
                    SelectedIngredient = IngredientOptions.SingleOrDefault(i => i.Id == source.IngredientId),
                    AmountText = source.Amount.ToString(CultureInfo.InvariantCulture), SelectedUnit = source.Unit,
                };
                Lines.Add(line);
            }
        }
    }

    public static RecipeEditDialogViewModel ForAdd(IReadOnlyList<Ingredient> ingredients) => new(ingredients, null);
    public static RecipeEditDialogViewModel ForEdit(RecipeLibraryItem recipe, IReadOnlyList<Ingredient> ingredients) => new(ingredients, recipe ?? throw new ArgumentNullException(nameof(recipe)));
    public IReadOnlyList<Ingredient> IngredientOptions { get; }
    public ObservableCollection<RecipeIngredientEditLine> Lines { get; } = new();
    public RecipeEditMode Mode => _original is null ? RecipeEditMode.Add : RecipeEditMode.Edit;
    public int? RecipeId => _original?.Id;
    public string Title => Mode == RecipeEditMode.Add ? "Add Recipe Draft" : "Edit Recipe Draft";
    public string Notice => "Amounts are totals for Base Servings. Confirm prepares an unsaved draft; Cancel leaves the recipe unchanged.";
    public string NameText { get => _nameText; set { if (SetProperty(ref _nameText, value ?? "")) Changed(); } }
    public string CuisineText { get => _cuisineText; set { if (SetProperty(ref _cuisineText, value ?? "")) Changed(); } }
    public string ServingsText { get => _servingsText; set { if (SetProperty(ref _servingsText, value ?? "")) Changed(); } }
    public string PrepMinutesText { get => _prepMinutesText; set { if (SetProperty(ref _prepMinutesText, value ?? "")) Changed(); } }
    public string ImagePathText { get => _imagePathText; set { if (SetProperty(ref _imagePathText, value ?? "")) Changed(); } }
    public RecipeIngredientEditLine? SelectedLine
    {
        get => _selectedLine;
        set { if (SetProperty(ref _selectedLine, value)) RemoveLineCommand.RaiseCanExecuteChanged(); }
    }
    public RelayCommand AddLineCommand { get; }
    public RelayCommand RemoveLineCommand { get; }
    public RelayCommand ConfirmCommand { get; }
    public RecipeEditDraft? Result { get; private set; }
    public event EventHandler? Confirmed;
    public string NameError => _confirmAttempted && string.IsNullOrWhiteSpace(NameText) ? "Enter a recipe name." : "";
    public string ServingsError => _confirmAttempted && RecipeDraftValidation.ParseInteger(ServingsText) is null ? "Enter a positive whole number of servings." : "";
    public string PrepMinutesError => _confirmAttempted && RecipeDraftValidation.ParseInteger(PrepMinutesText, true) is null ? "Enter a whole number of minutes, zero or greater." : "";
    public string FormError => !_confirmAttempted ? "" : Lines.Count == 0 ? "Add at least one ingredient line." : IsUnchanged() ? "Change a recipe value before confirming." : "";
    public bool IsValid => !string.IsNullOrWhiteSpace(NameText) && RecipeDraftValidation.ParseInteger(ServingsText) is not null
        && RecipeDraftValidation.ParseInteger(PrepMinutesText, true) is not null && Lines.Count > 0
        && Lines.All(line => line.Validate(IsDuplicate(line)) is null) && !IsUnchanged();

    private bool IsDuplicate(RecipeIngredientEditLine line) => line.SelectedIngredient is not null && Lines.Count(l => l.SelectedIngredient?.Id == line.SelectedIngredient.Id) > 1;
    private string? DraftImagePath => ImagePathText.Length == 0 ? (_original?.ImagePath == "" ? "" : null) : ImagePathText;
    private bool IsUnchanged()
    {
        if (_original is null || Lines.Any(line => line.Validate(IsDuplicate(line)) is not null)) return false;
        var original = _original.Ingredients.OrderBy(line => line.IngredientId).Select(line => new RecipeIngredientDraft(line.IngredientId, line.Amount, line.Unit));
        return NameText == _original.Name && CuisineText == _original.Cuisine
            && RecipeDraftValidation.ParseInteger(ServingsText) == _original.BaseServings && RecipeDraftValidation.ParseInteger(PrepMinutesText, true) == _original.PrepMinutes
            && DraftImagePath == _original.ImagePath && BuildLines().OrderBy(line => line.IngredientId).SequenceEqual(original);
    }
    private IReadOnlyList<RecipeIngredientDraft> BuildLines() => Lines.Select(line => new RecipeIngredientDraft(line.SelectedIngredient!.Id,
        RecipeDraftValidation.ParseAmount(line.AmountText)!.Value, line.SelectedUnit!.Value)).ToList().AsReadOnly();

    private void Confirm()
    {
        _confirmAttempted = true; Changed();
        if (!IsValid) return;
        Result = new RecipeEditDraft(Mode, RecipeId, NameText, CuisineText, RecipeDraftValidation.ParseInteger(PrepMinutesText, true)!.Value,
            RecipeDraftValidation.ParseInteger(ServingsText)!.Value, DraftImagePath, BuildLines());
        OnPropertyChanged(nameof(Result)); Confirmed?.Invoke(this, EventArgs.Empty);
    }
    private void OnLineChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(RecipeIngredientEditLine.SelectedIngredient) or nameof(RecipeIngredientEditLine.SelectedUnit) or nameof(RecipeIngredientEditLine.AmountText)) Changed();
    }
    private void Changed()
    {
        foreach (var line in Lines) line.SetError(_confirmAttempted ? line.Validate(IsDuplicate(line)) ?? "" : "");
        OnPropertyChanged(nameof(NameError)); OnPropertyChanged(nameof(ServingsError)); OnPropertyChanged(nameof(PrepMinutesError));
        OnPropertyChanged(nameof(FormError)); OnPropertyChanged(nameof(IsValid)); RemoveLineCommand.RaiseCanExecuteChanged();
    }
}
