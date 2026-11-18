using JQuery_ECommerceAss.Models;
using Microsoft.EntityFrameworkCore;

namespace JQuery_ECommerceAss.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<Admin> Admins { get; set; }
        public DbSet<Seller> Sellers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<TransactionHis> TransactionHis { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<UserPromotion> UserPromotions { get; set; }
        public DbSet<CheckoutAttempt> CheckoutAttempts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Cart>().Property(c => c.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Cart>().Property(c => c.Quantity).HasPrecision(18, 2);
            modelBuilder.Entity<Product>().Property(p => p.UnitPrice).HasPrecision(18, 2);
            modelBuilder.Entity<Promotion>().Property(p => p.DiscountPercentage).HasPrecision(18, 2);
            modelBuilder.Entity<Promotion>().Property(p => p.MaxDiscountAmount).HasPrecision(18, 2);
            modelBuilder.Entity<Promotion>().Property(p => p.MinPurchaseAmount).HasPrecision(18, 2);
            modelBuilder.Entity<TransactionHis>().Property(t => t.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<TransactionHis>().Property(t => t.GstAmount).HasPrecision(18, 2);
            modelBuilder.Entity<TransactionHis>().Property(t => t.Promotion).HasPrecision(18, 2);
            modelBuilder.Entity<TransactionHis>().Property(t => t.SubtotalAmount).HasPrecision(18, 2);
            modelBuilder.Entity<CheckoutAttempt>()
                .HasIndex(attempt => attempt.RequestId)
                .IsUnique();

            modelBuilder
                .Entity<User>()
                .Property(u => u.UserStatus)
                .HasConversion<string>();

            modelBuilder
                .Entity<Seller>()
                .Property(s => s.SellerStatus)
                .HasConversion<string>();

            modelBuilder
                .Entity<Product>()
                .Property(p => p.ProductStatus)
                .HasConversion<string>();

            modelBuilder
                .Entity<Promotion>()
                .Property(p => p.PromotionStatus)
                .HasConversion<string>();
        }
    }
}
