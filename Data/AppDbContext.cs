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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
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
