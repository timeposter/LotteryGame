using LotteryCore.Enetities;
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
            Log.Logger=new LoggerConfiguration()
                .MinimumLevel.Debug()
                //.WriteTo.Console()
                .WriteTo.File("logs/lottery_service_.log", rollingInterval: RollingInterval.Day)
                .CreateBootstrapLogger();
            var builder = WebApplication.CreateBuilder(args);
            // 新增：将DataProtection密钥存到程序目录，不读取用户目录
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

            // 配置类绑定、HttpClient
            builder.Services.Configure<LotteryPullSetting>(builder.Configuration.GetSection("LotteryPullSetting"));
            builder.Services.AddHttpClient("LotteryApiClient", client =>
            {
                // 外层总超时 > 内部http独立超时，避免冲突
                client.Timeout = TimeSpan.FromSeconds(18);
                // CDN校验必需，删除直接403拦截
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/125.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Referrer = new Uri("https://www.manycailm.com/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
.ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (msg, cert, chain, err) => true,
        UseCookies = true,
        CookieContainer = new System.Net.CookieContainer(),
        MaxConnectionsPerServer = 20
    };
});

            // EF Core MySQL DbContext
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AppDBContext>(options =>
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
            //builder.Services.AddHostedService<LotteryPullBackgroundService>();

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