using Bookstore.Domain;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Data;

/// <summary>Starter catalog loaded the first time the database is created.</summary>
public static class SeedData
{
    public static void Seed(BookstoreDbContext db)
    {
        if (db.Authors.Any())
        {
            return;
        }

        db.Authors.AddRange(CreateAuthors());
        db.SaveChanges();
    }

    public static async Task SeedAsync(BookstoreDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Authors.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Authors.AddRange(CreateAuthors());
        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<Author> CreateAuthors() =>
    [
        new()
        {
            Name = "Maya Okafor",
            Books =
            [
                NewBook("The Quiet Compiler", "978-1-23456-001-0", 18.99m, 12, Genre.Fiction, new(2019, 4, 2)),
                NewBook("Rivers of Glass", "978-1-23456-002-7", 24.50m, 3, Genre.Fiction, new(2022, 9, 13)),
            ],
        },
        new()
        {
            Name = "Lars Henriksen",
            Books =
            [
                NewBook("A Short History of Bridges", "978-1-23456-003-4", 32.00m, 7, Genre.History, new(2015, 1, 20)),
                NewBook("Northern Lights Field Guide", "978-1-23456-004-1", 27.25m, 0, Genre.Science, new(2021, 11, 5)),
            ],
        },
        new()
        {
            Name = "Priya Raman",
            Books =
            [
                NewBook("Patterns in the Noise", "978-1-23456-005-8", 45.00m, 20, Genre.Technology, new(2020, 6, 30)),
                NewBook("Async All the Way Down", "978-1-23456-006-5", 39.99m, 15, Genre.Technology, new(2024, 3, 12)),
            ],
        },
        new()
        {
            Name = "Tomás Ortega",
            Books =
            [
                NewBook("The Lighthouse Cat", "978-1-23456-007-2", 12.99m, 30, Genre.Children, new(2018, 10, 1)),
                NewBook("Maps We Never Drew", "978-1-23456-008-9", 21.00m, 5, Genre.NonFiction, new(2023, 2, 14)),
            ],
        },
    ];

    private static Book NewBook(
        string title, string isbn, decimal price, int stock, Genre genre, DateOnly publishedOn) =>
        new()
        {
            Title = title,
            Isbn = isbn,
            Price = price,
            Stock = stock,
            Genre = genre,
            PublishedOn = publishedOn,
        };
}
