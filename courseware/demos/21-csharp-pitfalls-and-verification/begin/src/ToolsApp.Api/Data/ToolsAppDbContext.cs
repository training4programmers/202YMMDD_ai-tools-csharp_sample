using Microsoft.EntityFrameworkCore;
using ToolsApp.Domain;

namespace ToolsApp.Api.Data;

public sealed class ToolsAppDbContext(DbContextOptions<ToolsAppDbContext> options) : DbContext(options)
{
    public DbSet<Color> Colors => Set<Color>();

    public DbSet<Palette> Palettes => Set<Palette>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Palette>(palette =>
        {
            palette.Property(p => p.Name).HasMaxLength(60);
            palette.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<Color>(color =>
        {
            color.Property(c => c.Name).HasMaxLength(60);
            color.Property(c => c.Hex).HasMaxLength(7);

            color.HasOne(c => c.Palette)
                .WithMany(p => p.Colors)
                .HasForeignKey(c => c.PaletteId);
        });
    }
}
