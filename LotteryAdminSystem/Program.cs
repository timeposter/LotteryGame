using LotteryCore.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LotteryAdminSystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // 日志初始化放最顶部，启动崩溃也能记录日志
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File("logs/lottery_admin_.log", rollingInterval: RollingInterval.Day)
                .CreateBootstrapLogger();

            try
            {
                var builder = WebApplication.CreateBuilder(args);

                // 全局Serilog接管日志
                builder.Host.UseSerilog();

                // Razor Pages 后台页面
                builder.Services.AddRazorPages(options =>
                {
                    options.RootDirectory = "/Pages";
                })
                .AddMvcOptions(opt =>
                {
                    opt.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                })
                .AddViewOptions(v =>
                {
                    v.HtmlHelperOptions.ClientValidationEnabled = true;
                });

                // MySQL EF上下文（管理后台读写数据库必需）
                string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                builder.Services.AddDbContext<AppDBContext>(options =>
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

                // 登录Session会话
                builder.Services.AddDistributedMemoryCache();
                builder.Services.AddSession(options =>
                {
                    options.IdleTimeout = TimeSpan.FromMinutes(30);
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                });

                // Cookie登录认证
                builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Admin/Login";
                    options.AccessDeniedPath = "/Admin/Login";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                });
                builder.Services.AddAuthorization();

                var app = builder.Build();

                // 生产环境异常页面
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Error");
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
                app.UseStaticFiles();

                app.UseRouting();
                // 中间件固定顺序：路由 → Session → 认证 → 授权
                app.UseSession();
                app.UseAuthentication();
                app.UseAuthorization();

                app.MapRazorPages();

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "管理后台启动失败，程序退出");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}