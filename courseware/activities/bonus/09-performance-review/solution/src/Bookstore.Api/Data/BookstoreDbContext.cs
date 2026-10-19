using Bookstore.Domain;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Data;

public sealed class BookstoreDbContext(DbContextOptions<BookstoreDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

    public DbSet<Author> Authors => Set<Author>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(author =>
        {
            author.Property(a => a.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Book>(book =>
        {
            book.Property(b => b.Title).HasMaxLength(200);
            book.Property(b => b.Isbn).HasMaxLength(17);
            book.HasIndex(b => b.Isbn).IsUnique();

            // SQLite has no decimal type; store as REAL so prices can be sorted and compared in SQL.
            book.Property(b => b.Price).HasConversion<double>();
            book.Property(b => b.Genre).HasConversion<string>().HasMaxLength(20);

            book.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId);
        });
    }
}
