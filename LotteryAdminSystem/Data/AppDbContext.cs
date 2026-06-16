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
        public DbSet<Admins> Admins { get; set; }
        public DbSet<Lottery> Lottery { get; set; }
        public DbSet<PlayConfig> PlayConfig { get; set; }
        public DbSet<LotteryInfo> LotteryInfos { get; set; }
        public DbSet<LotteryIssueRecord> LotteryIssueRecords { get; set; }
        public DbSet<UserFundLog> UserFundLogs { get; set; }
        public DbSet<UserBet> BetRecords { get; set; }
        public DbSet<LotteryData> LotteryDatas { get; set; }
        public DbSet<UsdtAccount> UsdtAccounts { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 联合唯一索引：彩种+期号不能重复
            modelBuilder.Entity<LotteryIssueRecord>()
                .HasIndex(x => new { x.LotteryCode, x.IssueNo })
                .IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<Admins>().HasIndex(a => a.AdminName).IsUnique();
        }
    }
}