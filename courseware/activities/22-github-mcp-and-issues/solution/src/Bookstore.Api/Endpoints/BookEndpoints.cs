using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Endpoints;

public static class BookEndpoints
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/api/books").WithTags("Books");

        books.MapGet("/", GetBooksAsync);
        books.MapGet("/{id:int}", GetBookAsync).WithName("GetBook");
        books.MapPost("/", CreateBookAsync);
        books.MapPut("/{id:int}", UpdateBookAsync);
        books.MapDelete("/{id:int}", DeleteBookAsync);

        return app;
    }

    // GET /api/books?genre=Fiction&authorId=1
    private static async Task<Ok<List<BookDto>>> GetBooksAsync(
        BookstoreDbContext db, TimeProvider timeProvider, Genre? genre, int? authorId, CancellationToken cancellationToken)
    {
        var query = db.Books.Include(b => b.Author).AsNoTracking();

        if (genre is not null)
        {
            query = query.Where(b => b.Genre == genre);
        }

        if (authorId is not null)
        {
            query = query.Where(b => b.AuthorId == authorId);
        }

        var books = await query.OrderBy(b => b.Title).ToListAsync(cancellationToken);
        var today = timeProvider.UtcToday();
        return TypedResults.Ok(books.Select(b => b.ToDto(today)).ToList());
    }

    private static async Task<Results<Ok<BookDto>, NotFound>> GetBookAsync(
        int id, BookstoreDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var book = await db.Books.Include(b => b.Author).AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        return book is null ? TypedResults.NotFound() : TypedResults.Ok(book.ToDto(timeProvider.UtcToday()));
    }

    private static async Task<Results<CreatedAtRoute<BookDto>, ValidationProblem>> CreateBookAsync(
        BookRequest request, BookstoreDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var today = timeProvider.UtcToday();
        if (await ValidateAsync(request, db, existingId: null, today, cancellationToken) is { } problem)
        {
            return problem;
        }

        var book = new Book
        {
            Title = request.Title,
            Isbn = request.Isbn,
            Price = request.Price,
            Stock = request.Stock,
            Genre = request.Genre,
            PublishedOn = request.PublishedOn,
            AuthorId = request.AuthorId,
        };

        db.Books.Add(book);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(book).Reference(b => b.Author).LoadAsync(cancellationToken);

        return TypedResults.CreatedAtRoute(book.ToDto(today), "GetBook", new { id = book.Id });
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem>> UpdateBookAsync(
        int id, BookRequest request, BookstoreDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var book = await db.Books.FindAsync([id], cancellationToken);
        if (book is null)
        {
            return TypedResults.NotFound();
        }

        if (await ValidateAsync(request, db, existingId: id, timeProvider.UtcToday(), cancellationToken) is { } problem)
        {
            return problem;
        }

        book.Title = request.Title;
        book.Isbn = request.Isbn;
        book.Price = request.Price;
        book.Stock = request.Stock;
        book.Genre = request.Genre;
        book.PublishedOn = request.PublishedOn;
        book.AuthorId = request.AuthorId;

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound>> DeleteBookAsync(
        int id, BookstoreDbContext db, CancellationToken cancellationToken)
    {
        var deleted = await db.Books.Where(b => b.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    // Rules that need the database, the clock, or domain logic, beyond the attribute validation on
    // BookRequest. Create and update share this method so a rule can't be enforced in only one of them.
    private static async Task<ValidationProblem?> ValidateAsync(
        BookRequest request, BookstoreDbContext db, int? existingId, DateOnly today, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (!Isbn.IsValid(request.Isbn))
        {
            errors[nameof(request.Isbn)] = ["Isbn must be a valid ISBN-13."];
        }
        else if (await db.Books.AnyAsync(b => b.Isbn == request.Isbn && b.Id != existingId, cancellationToken))
        {
            errors[nameof(request.Isbn)] = ["A book with this ISBN already exists."];
        }

        if (!await db.Authors.AnyAsync(a => a.Id == request.AuthorId, cancellationToken))
        {
            errors[nameof(request.AuthorId)] = [$"Author {request.AuthorId} does not exist."];
        }

        if (request.PublishedOn > today && request.Stock > 0)
        {
            errors[nameof(request.Stock)] =
                ["A pre-order (published after today) can't have stock. Set Stock to 0."];
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }
}
