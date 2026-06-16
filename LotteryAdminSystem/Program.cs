using LotteryAdminSystem.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Razor Pages 服务注册
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

            // 配置类绑定、HttpClient
            builder.Services.Configure<LotteryPullSetting>(builder.Configuration.GetSection("LotteryPullSetting"));
            builder.Services.AddHttpClient();

            // EF Core MySQL DbContext
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            // Session 配置
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // 后台定时拉取开奖服务
            builder.Services.AddHostedService<LotteryPullBackgroundService>();

            // Cookie 身份认证
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Admin/Login";
                options.AccessDeniedPath = "/Admin/Login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });
            builder.Services.AddAuthorization();

            var app = builder.Build();

            // 生产环境异常处理
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            // ✅ 标准固定顺序：Routing → Session → 认证 → 授权
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();
        }
    }
}