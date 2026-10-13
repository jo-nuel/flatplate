using System.Windows.Controls;
using FlatPlate.App.Services;

namespace FlatPlate.App.Views;

/// <summary>Displays recipes with search, cuisine filtering and ingredient details.</summary>
public partial class RecipeLibraryView : UserControl
{
    public RecipeLibraryView()
    {
        InitializeComponent();
    }

    /// <summary>Connects host-supplied reads and interactions without creating a database.</summary>
    public void Connect(RecipeLibraryCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        DataContext = coordinator.ViewModel;
        coordinator.Load();
    }
}
