using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Endpoints;

public static class AuthorEndpoints
{
    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder app)
    {
        var authors = app.MapGroup("/api/authors").WithTags("Authors");

        authors.MapGet("/", GetAuthorsAsync);
        authors.MapGet("/{id:int}/books", GetAuthorBooksAsync);

        return app;
    }

    private static async Task<Ok<List<AuthorDto>>> GetAuthorsAsync(
        BookstoreDbContext db, CancellationToken cancellationToken)
    {
        var authors = await db.Authors
            .OrderBy(a => a.Name)
            .Select(a => new AuthorDto(a.Id, a.Name, a.Books.Count))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(authors);
    }

    private static async Task<Results<Ok<List<BookDto>>, NotFound>> GetAuthorBooksAsync(
        int id, BookstoreDbContext db, CancellationToken cancellationToken)
    {
        var author = await db.Authors.Include(a => a.Books).AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (author is null)
        {
            return TypedResults.NotFound();
        }

        var books = author.Books
            .OrderBy(b => b.PublishedOn)
            .Select(b => b.ToDto() with { AuthorName = author.Name })
            .ToList();

        return TypedResults.Ok(books);
    }
}
