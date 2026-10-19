using Bookstore.Domain;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Data;

public sealed class BookstoreDbContext(DbContextOptions<BookstoreDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<Order> Orders => Set<Order>();

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

            // Two orders that read the same stock level can't both save: the second gets a concurrency error.
            book.Property(b => b.Stock).IsConcurrencyToken();

            book.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId);
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.Property(o => o.CouponCode).HasMaxLength(20);
            order.Property(o => o.Subtotal).HasConversion<double>();
            order.Property(o => o.Discount).HasConversion<double>();
            order.Property(o => o.Tax).HasConversion<double>();
            order.Property(o => o.Total).HasConversion<double>();

            order.HasMany(o => o.Lines)
                .WithOne()
                .HasForeignKey(l => l.OrderId);
        });

        modelBuilder.Entity<OrderLine>(line =>
        {
            line.Property(l => l.UnitPrice).HasConversion<double>();

            line.HasOne(l => l.Book)
                .WithMany()
                .HasForeignKey(l => l.BookId);
        });
    }
}
