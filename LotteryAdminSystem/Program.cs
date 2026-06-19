using LotteryCore.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LotteryAdminSystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                //.WriteTo.Console()
                .WriteTo.File("logs/lottery_service_.log", rollingInterval: RollingInterval.Day)
                .CreateBootstrapLogger();
            var builder = WebApplication.CreateBuilder(args);

            // 将DataProtection密钥存到程序目录，不读取用户目录
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "DataProtectKeys")));

            builder.Host.UseWindowsService();
            builder.Host.UseSerilog();

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

            // ========== 注释：开奖拉取配置绑定 ==========
            //builder.Services.Configure<LotteryPullSetting>(builder.Configuration.GetSection("LotteryPullSetting"));

            // ========== 注释：拉取专用HttpClient，不再请求第三方开奖接口 ==========
            //builder.Services.AddHttpClient("LotteryApiClient", client =>
            //{
            //    // 外层总超时 > 内部http独立超时，避免冲突
            //    client.Timeout = TimeSpan.FromSeconds(18);
            //    // CDN校验必需，删除直接403拦截
            //    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/125.0.0.0 Safari/537.36");
            //    client.DefaultRequestHeaders.Referrer = new Uri("shturl.cc/Kyx79z41Nkr7MxN1");
            //    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            //})
            //.ConfigurePrimaryHttpMessageHandler(() =>
            //{
            //    return new HttpClientHandler
            //    {
            //        ServerCertificateCustomValidationCallback = (msg, cert, chain, err) => true,
            //        UseCookies = true,
            //        CookieContainer = new System.Net.CookieContainer(),
            //        MaxConnectionsPerServer = 20
            //    };
            //});

            // EF Core MySQL DbContext（网站后台必须保留，不能注释）
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDBContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            // Session 配置（后台登录会话保留）
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // ========== 注释：后台定时拉取开奖后台服务 ==========
            //builder.Services.AddHostedService<LotteryPullBackgroundService>();

            // Cookie 身份认证（后台登录必备，保留）
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
            // 标准固定顺序：Routing → Session → 认证 → 授权
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapRazorPages();

            app.Run();
        }
    }
}