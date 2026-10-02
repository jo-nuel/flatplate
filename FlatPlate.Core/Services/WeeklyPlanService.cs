namespace FlatPlate.Core.Services;

/// <summary>
/// Owns weekly plan changes and notifies screens that depend on the plan.
/// </summary>
public sealed class WeeklyPlanService
{
    /// <summary>
    /// Occurs after a planned meal is added, changed, or removed.
    /// </summary>
    public event EventHandler? PlanChanged;

    private void RaisePlanChanged()
    {
        PlanChanged?.Invoke(this, EventArgs.Empty);
    }
}
