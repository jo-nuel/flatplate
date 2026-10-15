using System;

namespace FlatPlate.Core.Models;

/// Stores the shopping status of an ingredient for a specific week.
public class ShoppingListRecord
{
    public DateOnly WeekStartDate { get; set; }
    public int IngredientId { get; set; }
    public bool IsPurchased { get; set; }
    public bool AlreadyHave { get; set; }
    public decimal? ActualPrice { get; set; }
}
