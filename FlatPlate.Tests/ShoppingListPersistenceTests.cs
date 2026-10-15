using System;
using System.Linq;
using FlatPlate.Core.Data;
using FlatPlate.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace FlatPlate.Tests;

public class ShoppingListPersistenceTests
{
    [Test]
    public void ShoppingRecords_CanBeSavedAndRestored()
    {
        // Create a temporary SQLite database for testing
        using var connection =
            new SqliteConnection("Data Source=:memory:");

        connection.Open();

        var options = new DbContextOptionsBuilder<FlatPlateDbContext>()
            .UseSqlite(connection)
            .Options;

        var week1 = new DateOnly(2026, 10, 12);
        var week2 = week1.AddDays(7);

        // Save shopping records
        using (var context = new FlatPlateDbContext(options))
        {
            context.Database.EnsureCreated();

            context.ShoppingListRecords.AddRange(
                new ShoppingListRecord
                {
                    WeekStartDate = week1,
                    IngredientId = 1,
                    IsPurchased = true,
                    ActualPrice = 4.50m
                },
                new ShoppingListRecord
                {
                    WeekStartDate = week2,
                    IngredientId = 1,
                    AlreadyHave = true
                });

            context.SaveChanges();
        }

        // Read the records using a new DbContext
        using (var context = new FlatPlateDbContext(options))
        {
            var firstWeek = context.ShoppingListRecords
                .Single(record =>
                    record.WeekStartDate == week1 &&
                    record.IngredientId == 1);

            var secondWeek = context.ShoppingListRecords
                .Single(record =>
                    record.WeekStartDate == week2 &&
                    record.IngredientId == 1);

            Assert.That(firstWeek.IsPurchased, Is.True);
            Assert.That(firstWeek.ActualPrice, Is.EqualTo(4.50m));

            Assert.That(secondWeek.IsPurchased, Is.False);
            Assert.That(secondWeek.AlreadyHave, Is.True);
        }
    }
}
