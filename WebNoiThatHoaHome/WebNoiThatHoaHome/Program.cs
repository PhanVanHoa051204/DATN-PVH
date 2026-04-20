using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using WebNoiThatHoaHome.Models;



var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
// Add services to the container.
builder.Services.AddControllersWithViews();

// 1. Khai báo kết nối Database
builder.Services.AddDbContext<HoaHomeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Khai báo dịch vụ Đăng nhập bằng Cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");    
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // BẮT BUỘC THÊM DÒNG NÀY TRƯỚC ĐỂ LÀM LOGIN
app.UseAuthorization();

app.MapHub<WebNoiThatHoaHome.Hubs.OrderHub>("/orderHub");
// Route dành riêng cho Area (Admin) - Phải đặt bên trên Default
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Route mặc định dành cho User (Khách hàng)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
    