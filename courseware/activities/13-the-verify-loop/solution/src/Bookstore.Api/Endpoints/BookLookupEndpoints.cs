using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Endpoints;

public static class BookLookupEndpoints
{
    public static IEndpointRouteBuilder MapBookLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/books/by-isbn/{isbn}", GetBookByIsbnAsync).WithTags("Books");
        return app;
    }

    // GET /api/books/by-isbn/9781234560010 accepts the ISBN with or without hyphens.
    private static async Task<Results<Ok<BookDto>, NotFound, ValidationProblem>> GetBookByIsbnAsync(
        string isbn, BookstoreDbContext db, CancellationToken cancellationToken)
    {
        if (!Isbn.IsValid(isbn))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["isbn"] = ["isbn must be a valid ISBN-13."],
            });
        }

        // Stored ISBNs may or may not contain hyphens, so compare normalized forms. The catalog is small
        // enough to compare in memory; a large one would store a normalized column instead.
        var wanted = Isbn.Normalize(isbn);
        var books = await db.Books.Include(b => b.Author).AsNoTracking().ToListAsync(cancellationToken);
        var book = books.FirstOrDefault(b => Isbn.Normalize(b.Isbn) == wanted);

        return book is null ? TypedResults.NotFound() : TypedResults.Ok(book.ToDto());
    }
}
