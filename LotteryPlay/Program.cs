using LotteryCore.Data;
using LotteryPlay.Hubs;
using LotteryPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 注册数据库上下文 MySQL
        builder.Services.AddDbContext<AppDBContext>(options =>
        {
            var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
             options.UseMySql(connStr, ServerVersion.AutoDetect(connStr), mysqlOpt =>
            {
                // 开启连接自动重连
                mysqlOpt.EnableRetryOnFailure(
                    maxRetryCount: 3,       // 失败重试3次
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorNumbersToAdd: null
                );
            });
        });

        // 注册Session
        builder.Services.AddSession(options =>
        {
            options.Cookie.IsEssential = true;
            options.IdleTimeout = TimeSpan.FromHours(2);
            options.Cookie.HttpOnly = true;
            // Cookie 作用域：全站根路径 /，所有子目录都能读取
            options.Cookie.Path = "/";

        });

        // 注册SignalR
        builder.Services.AddSignalR();


        // 注册RazorPages
        builder.Services.AddRazorPages(options =>
        {
            // 全局关闭所有页面的防伪验证
            options.RootDirectory = "/Pages";
            options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
        });
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAdminOrigin", cfg =>
            {
                cfg.WithOrigins("https://localhost:7129")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
            });
        });

        builder.Services.AddSignalR();
        var app = builder.Build();
        // 生产环境异常页
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        // 静态资源、路由、Session、授权中间件
        app.UseStaticFiles();
        // 启用跨域
        app.UseSession();
        app.UseRouting();
        app.UseAuthorization();
        app.UseAuthentication();
        app.UseAuthorization(); 
        // 路由映射
        app.MapRazorPages();
        //路由注册
        app.MapHub<ChatHub>("/chathub");
        // 映射SignalR Hub端点（与前端地址一致 /lotteryHub）
        app.MapHub<LotteryHub>("/lotteryHub");

        app.Run();
    }
}