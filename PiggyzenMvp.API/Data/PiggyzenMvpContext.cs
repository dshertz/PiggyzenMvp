using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Data
{
    public class PiggyzenMvpContext : DbContext
    {
        public PiggyzenMvpContext(DbContextOptions<PiggyzenMvpContext> options)
            : base(options) { }

        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CategorizationRule> CategorizationRules { get; set; }
        public DbSet<CategorizationUsage> CategorizationUsages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Category: self reference
            modelBuilder
                .Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany()
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Transaction ↔ CategorizationUsage (1:1)
            modelBuilder
                .Entity<Transaction>()
                .HasOne(t => t.CategorizationUsage)
                .WithOne(u => u.Transaction)
                .HasForeignKey<CategorizationUsage>(u => u.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            // En usage per transaction (unik FK)
            modelBuilder.Entity<CategorizationUsage>().HasIndex(u => u.TransactionId).IsUnique();

            // Usage → History (N:1) + koppla till navigationen Usages
            modelBuilder
                .Entity<CategorizationUsage>()
                .HasOne(u => u.CategorizationRule)
                .WithMany(h => h.Usages)
                .HasForeignKey(u => u.CategorizationRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            // History → Category
            modelBuilder
                .Entity<CategorizationRule>()
                .HasOne(h => h.Category)
                .WithMany()
                .HasForeignKey(h => h.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index & kolumndefinitioner (prestanda/hygien)
            modelBuilder
                .Entity<CategorizationRule>()
                .Property(h => h.Description)
                .HasMaxLength(512);
            modelBuilder
                .Entity<CategorizationRule>()
                .Property(h => h.NormalizedDescription)
                .HasMaxLength(256);
            modelBuilder.Entity<CategorizationRule>().HasIndex(h => h.NormalizedDescription);
            modelBuilder
                .Entity<CategorizationRule>()
                .HasIndex(h => new { h.NormalizedDescription, h.IsPositive });
            // Beloppsgränser används inte just nu – lämnas kommenterade för framtida stöd.
            // modelBuilder
            //     .Entity<CategorizationRule>()
            //     .Property(h => h.MinAmount)
            //     .HasColumnType("decimal(18,2)");
            // modelBuilder
            //     .Entity<CategorizationRule>()
            //     .Property(h => h.MaxAmount)
            //     .HasColumnType("decimal(18,2)");
            modelBuilder
                .Entity<CategorizationUsage>()
                .Property(u => u.Amount)
                .HasColumnType("decimal(18,2)");

            // Seeds
            modelBuilder
                .Entity<Category>()
                .HasData(
                    new Category
                    {
                        Id = 1,
                        Name = "Income",
                        IsSystemCategory = true,
                    },
                    new Category
                    {
                        Id = 2,
                        Name = "Housing",
                        IsSystemCategory = true,
                    },
                    new Category
                    {
                        Id = 3,
                        Name = "Vehicle",
                        IsSystemCategory = true,
                    },
                    new Category
                    {
                        Id = 4,
                        Name = "Fixed Expenses",
                        IsSystemCategory = true,
                    },
                    new Category
                    {
                        Id = 5,
                        Name = "Variable Expenses",
                        IsSystemCategory = true,
                    },
                    new Category
                    {
                        Id = 6,
                        Name = "Transfers",
                        IsSystemCategory = true,
                    }
                );
        }
    }
}
