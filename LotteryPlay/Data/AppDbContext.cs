using Microsoft.EntityFrameworkCore;
using LotteryPlay.Models;
using LotteryModels;

namespace LotteryPlay.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }


        public DbSet<User> Users { get; set; }
        public DbSet<UserFundLog> UserFundLogs { get; set; }
        public DbSet<UserBet> UserBets { get; set; }
        public DbSet<LotteryData> LotteryDatas { get; set; }
        public DbSet<LotteryCategory> LotteryCategories { get; set; }
        // 新增：彩种、玩法
        public DbSet<LotteryModels.Lottery> Lottery { get; set; } = null!;
        public DbSet<PlayConfig> PlayConfig { get; set; } = null!;
        public DbSet<UserTrace> UserTrace { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<LotteryData>().HasIndex(l => l.PeriodNo).IsUnique();
        }
    }
}
