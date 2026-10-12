using System.Windows.Controls;
using FlatPlate.App.Services;

namespace FlatPlate.App.Views;

/// <summary>Displays pack prices with store filtering and CSV actions.</summary>
public partial class PriceBookView : UserControl
{
    public PriceBookView()
    {
        InitializeComponent();
    }

    /// <summary>Connects the host coordinator without creating a database context.</summary>
    public void Connect(PriceBookCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        DataContext = coordinator.ViewModel;
        coordinator.Load();
    }
}
