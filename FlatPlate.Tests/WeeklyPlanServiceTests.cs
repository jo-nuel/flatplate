using FlatPlate.Core.Enums;
using FlatPlate.Core.Interfaces;
using FlatPlate.Core.Models;
using FlatPlate.Core.Services;

namespace FlatPlate.Tests;

/// <summary>
/// Checks that planner changes are stored and announced to dependent screens.
/// </summary>
public sealed class WeeklyPlanServiceTests
{
    private static readonly DateOnly WeekStart = new(2026, 10, 12);

    private TestPlannedMealRepository _repository = null!;
    private WeeklyPlanService _service = null!;

    /// <summary>
    /// Creates an empty in-memory planner repository for each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _repository = new TestPlannedMealRepository();
        _service = new WeeklyPlanService(_repository);
    }

    /// <summary>
    /// Confirms that setting a new meal stores it and raises the change event.
    /// </summary>
    [Test]
    public void SetMeal_EmptySlot_AddsMealAndRaisesPlanChanged()
    {
        var eventCount = 0;
        _service.PlanChanged += (_, _) => eventCount++;

        var result = _service.SetMeal(
            WeekStart,
            MealSlot.Dinner,
            CreateRecipe(1, "Stir fry"),
            2);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.Items, Has.Count.EqualTo(1));
            Assert.That(result.Servings, Is.EqualTo(2));
            Assert.That(eventCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Confirms that setting an occupied slot replaces rather than duplicates it.
    /// </summary>
    [Test]
    public void SetMeal_OccupiedSlot_UpdatesExistingMeal()
    {
        _service.SetMeal(
            WeekStart,
            MealSlot.Dinner,
            CreateRecipe(1, "Stir fry"),
            2);

        var result = _service.SetMeal(
            WeekStart,
            MealSlot.Dinner,
            CreateRecipe(2, "Pasta"),
            4);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.Items, Has.Count.EqualTo(1));
            Assert.That(result.Recipe.Name, Is.EqualTo("Pasta"));
            Assert.That(result.Servings, Is.EqualTo(4));
        });
    }

    /// <summary>
    /// Confirms that clearing an occupied slot removes it and raises the event.
    /// </summary>
    [Test]
    public void ClearMeal_OccupiedSlot_RemovesMealAndRaisesPlanChanged()
    {
        _service.SetMeal(
            WeekStart,
            MealSlot.Lunch,
            CreateRecipe(1, "Wraps"),
            2);
        var eventCount = 0;
        _service.PlanChanged += (_, _) => eventCount++;

        var result = _service.ClearMeal(WeekStart, MealSlot.Lunch);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(_repository.Items, Is.Empty);
            Assert.That(eventCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Confirms that clearing an empty slot does not announce a false change.
    /// </summary>
    [Test]
    public void ClearMeal_EmptySlot_ReturnsFalseWithoutRaisingPlanChanged()
    {
        var eventCount = 0;
        _service.PlanChanged += (_, _) => eventCount++;

        var result = _service.ClearMeal(WeekStart, MealSlot.Breakfast);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(eventCount, Is.Zero);
        });
    }

    /// <summary>
    /// Confirms that weekly retrieval excludes meals before and after the week.
    /// </summary>
    [Test]
    public void GetWeek_MealsAcrossDates_ReturnsOnlySelectedWeekInOrder()
    {
        _repository.Add(CreateMeal(WeekStart.AddDays(2), MealSlot.Dinner));
        _repository.Add(CreateMeal(WeekStart, MealSlot.Breakfast));
        _repository.Add(CreateMeal(WeekStart.AddDays(-1), MealSlot.Lunch));
        _repository.Add(CreateMeal(WeekStart.AddDays(7), MealSlot.Lunch));

        var result = _service.GetWeek(WeekStart);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Date, Is.EqualTo(WeekStart));
            Assert.That(result[1].Date, Is.EqualTo(WeekStart.AddDays(2)));
        });
    }

    /// <summary>
    /// Confirms that clearing a week removes all its meals and raises one event.
    /// </summary>
    [Test]
    public void ClearWeek_MultipleMeals_RemovesWeekAndRaisesPlanChangedOnce()
    {
        _repository.Add(CreateMeal(WeekStart, MealSlot.Breakfast));
        _repository.Add(CreateMeal(WeekStart.AddDays(3), MealSlot.Dinner));
        var eventCount = 0;
        _service.PlanChanged += (_, _) => eventCount++;

        var removedCount = _service.ClearWeek(WeekStart);

        Assert.Multiple(() =>
        {
            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(_repository.Items, Is.Empty);
            Assert.That(eventCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Confirms that servings outside the allowed planner range are rejected.
    /// </summary>
    [TestCase(0)]
    [TestCase(21)]
    public void SetMeal_InvalidServings_ThrowsArgumentOutOfRangeException(
        int servings)
    {
        Assert.That(
            () => _service.SetMeal(
                WeekStart,
                MealSlot.Dinner,
                CreateRecipe(1, "Stir fry"),
                servings),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static Recipe CreateRecipe(int id, string name)
    {
        return new Recipe { Id = id, Name = name, BaseServings = 2 };
    }

    private static PlannedMeal CreateMeal(DateOnly date, MealSlot slot)
    {
        var recipe = CreateRecipe(1, "Test recipe");
        return new PlannedMeal
        {
            Date = date,
            Slot = slot,
            RecipeId = recipe.Id,
            Recipe = recipe,
            Servings = 2,
        };
    }

    private sealed class TestPlannedMealRepository : IRepository<PlannedMeal>
    {
        private int _nextId = 1;

        public List<PlannedMeal> Items { get; } = new();

        public IReadOnlyList<PlannedMeal> GetAll()
        {
            return Items.ToList();
        }

        public PlannedMeal? GetById(int id)
        {
            return Items.SingleOrDefault(meal => meal.Id == id);
        }

        public IReadOnlyList<PlannedMeal> Find(Func<PlannedMeal, bool> predicate)
        {
            return Items.Where(predicate).ToList();
        }

        public void Add(PlannedMeal entity)
        {
            entity.Id = _nextId++;
            Items.Add(entity);
        }

        public void Update(PlannedMeal entity)
        {
            if (!Items.Contains(entity))
            {
                throw new InvalidOperationException("Cannot update a missing meal.");
            }
        }

        public void Delete(PlannedMeal entity)
        {
            Items.Remove(entity);
        }
    }
}
