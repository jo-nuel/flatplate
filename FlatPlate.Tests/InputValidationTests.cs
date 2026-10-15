using FlatPlate.App.ViewModels;
using FlatPlate.App.Services;
using FlatPlate.Core.Enums;
using FlatPlate.Core.Models;

namespace FlatPlate.Tests;

/// <summary>
/// Verifies the user-editable recipe and price forms reject invalid drafts before persistence.
/// </summary>
public sealed class InputValidationTests
{
    private readonly WeightIngredient _rice = new()
    {
        Id = 1,
        Name = "Rice",
    };

    private readonly Store _coles = new()
    {
        Id = 1,
        Name = "Coles",
    };

    [TestCase("")]
    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("1,000")]
    [TestCase("not a number")]
    public void RecipeAmount_InvalidText_IsRejected(string text)
    {
        Assert.That(RecipeDraftValidation.ParseAmount(text), Is.Null);
    }

    [Test]
    public void RecipeDialog_InvalidFields_DoesNotCreateDraft()
    {
        var viewModel = RecipeEditDialogViewModel.ForAdd([_rice]);
        viewModel.NameText = "";
        viewModel.ServingsText = "0";
        viewModel.PrepMinutesText = "-1";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Result, Is.Null);
            Assert.That(viewModel.NameError, Is.Not.Empty);
            Assert.That(viewModel.ServingsError, Is.Not.Empty);
            Assert.That(viewModel.PrepMinutesError, Is.Not.Empty);
            Assert.That(viewModel.Lines.Single().Error, Is.Not.Empty);
        });
    }

    [Test]
    public void RecipeDialog_ValidFields_CreateDraft()
    {
        var viewModel = RecipeEditDialogViewModel.ForAdd([_rice]);
        var line = viewModel.Lines.Single();
        viewModel.NameText = "Rice bowl";
        viewModel.CuisineText = "Asian";
        viewModel.ServingsText = "2";
        viewModel.PrepMinutesText = "15";
        line.SelectedIngredient = _rice;
        line.AmountText = "500";
        line.SelectedUnit = MeasureUnit.Gram;

        viewModel.ConfirmCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Result, Is.Not.Null);
            Assert.That(viewModel.Result!.Name, Is.EqualTo("Rice bowl"));
            Assert.That(viewModel.Result.Ingredients, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void PriceDialog_InvalidAmounts_DoNotCreateDraft()
    {
        var viewModel = PriceEditDialogViewModel.ForAdd(
            [_rice],
            [_coles],
            []);
        viewModel.SelectedIngredient = _rice;
        viewModel.SelectedStore = _coles;
        viewModel.PriceText = "-2";
        viewModel.PackSizeText = "not a number";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Result, Is.Null);
            Assert.That(viewModel.PriceError, Is.Not.Empty);
            Assert.That(viewModel.PackSizeError, Is.Not.Empty);
        });
    }

    [Test]
    public void PriceDialog_DuplicateIngredientAndStore_IsRejected()
    {
        var viewModel = PriceEditDialogViewModel.ForAdd(
            [_rice],
            [_coles],
            [(_rice.Id, _coles.Id)]);
        viewModel.SelectedIngredient = _rice;
        viewModel.SelectedStore = _coles;
        viewModel.PriceText = "3.50";
        viewModel.PackSizeText = "500";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Result, Is.Null);
            Assert.That(viewModel.FormError, Does.Contain("already has a price"));
        });
    }

    [Test]
    public void PriceCsv_InvalidNumericAndEnumFields_ReturnsErrorsWithoutRecords()
    {
        var service = new PriceCsvService(["Rice"], ["Coles"]);
        var csv = PriceCsvService.Header + Environment.NewLine
            + "Rice,Coles,-1,0,kg,not-a-date,Unknown";

        var result = service.Parse(csv);

        Assert.Multiple(() =>
        {
            Assert.That(result.Records, Is.Empty);
            Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(5));
        });
    }

    [Test]
    public void RecipeCsv_NonPositiveIngredientAmount_IsRejected()
    {
        const string recipes =
            "RecipeKey,Name,Cuisine,PrepMinutes,BaseServings,ImagePath\r\n"
            + "rice-bowl,Rice bowl,Asian,15,2,";
        const string ingredients =
            "RecipeKey,IngredientName,Amount,MeasureUnit,SuggestedIngredientType,ValidationStatus\r\n"
            + "rice-bowl,Rice,-1,Gram,WeightIngredient,Reviewed";

        Assert.That(
            () => PersonBRecipeCatalogue.Parse(recipes, ingredients),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void HistoricalPriceCsv_InvalidStockFlag_IsRejected()
    {
        var csv = HistoricalPriceCsvReader.Header + Environment.NewLine
            + "Rice,Coles,3.50,500,g,2026-10-16,product-1,yes";

        Assert.That(
            () => new HistoricalPriceCsvReader().Parse(
                csv,
                ["Rice"],
                ["Coles"]),
            Throws.TypeOf<InvalidDataException>());
    }
}
