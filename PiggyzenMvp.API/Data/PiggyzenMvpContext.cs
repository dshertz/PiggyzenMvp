using Microsoft.EntityFrameworkCore;
using PiggyzenMvp.API.Models;

namespace PiggyzenMvp.API.Data
{
    public class PiggyzenMvpContext : DbContext
    {
        public PiggyzenMvpContext(DbContextOptions<PiggyzenMvpContext> options)
            : base(options) { }

        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<CategoryGroup> CategoryGroups { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CategorizationRule> CategorizationRules { get; set; }
        public DbSet<CategorizationUsage> CategorizationUsages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Category groups
            modelBuilder
                .Entity<CategoryGroup>()
                .HasIndex(g => g.Key)
                .IsUnique();
            modelBuilder.Entity<CategoryGroup>().Property(g => g.DisplayName).HasMaxLength(128);
            modelBuilder.Entity<CategoryGroup>().Property(g => g.Id).ValueGeneratedNever();

            // Categories
            modelBuilder
                .Entity<Category>()
                .HasOne(c => c.Group)
                .WithMany(g => g.Categories)
                .HasForeignKey(c => c.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder
                .Entity<Category>()
                .HasIndex(c => new { c.GroupId, c.Key })
                .IsUnique();
            modelBuilder.Entity<Category>().Property(c => c.SystemDisplayName).HasMaxLength(128);
            modelBuilder.Entity<Category>().Property(c => c.CustomDisplayName).HasMaxLength(128);
            modelBuilder.Entity<Category>().Property(c => c.Key).HasMaxLength(128);

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
            // Seeds handled outside OnModelCreating to preserve user overrides
        }
    }
}
