using LotteryCore.Enetities;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace LotteryCore.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();

            // 使用 EnsureCreated 在没有迁移时也能根据模型创建表（可替换为 db.Database.Migrate() 如果你使用迁移）
            db.Database.EnsureCreated();

            // 如果没有管理员则插入一个默认管理员
            if (!db.Admins.Any())
            {
                var pwd = "admin123"; // 请在生产环境更改默认密码
                var admin = new Admins
                {
                    AdminName = "admin",
                    PasswordHash = HashPwd(pwd),
                    CreateTime = DateTime.UtcNow
                };
                db.Admins.Add(admin);
                db.SaveChanges();
            }
        }

        private static string HashPwd(string pwd)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(pwd);
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }
}