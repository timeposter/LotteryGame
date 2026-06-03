using LotteryPlay.Data;
using LotteryPlay.Hubs;
using LotteryPlay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 注册数据库上下文 MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    );
});

// 注册Session
builder.Services.AddSession(options =>
{
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(2);
});

// 注册SignalR
builder.Services.AddSignalR();

// 注册彩票后台定时轮询服务（核心，解决provider空报错）
builder.Services.AddHostedService<LotteryBackgroundService>();

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
app.UseRouting();

// 启用跨域
app.UseSession();
app.UseAuthorization();

// 路由映射
app.MapRazorPages();
//路由注册
app.MapHub<ChatHub>("/chathub");
// 映射SignalR Hub端点（与前端地址一致 /lotteryHub）
app.MapHub<LotteryHub>("/lotteryHub");

app.Run();