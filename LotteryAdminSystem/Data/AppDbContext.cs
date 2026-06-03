using LotteryModels;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<User> Users { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Lottery> Lottery { get; set; }
        public DbSet<PlayConfig> PlayConfig { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Admin>().HasIndex(a => a.AdminName).IsUnique();
        }
    }
}