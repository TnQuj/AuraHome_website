using AISmartHome.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddDbContext<AISmartHomeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AISmartHome.Services.TaoTinTuDong>();
builder.Services.AddHostedService<AISmartHome.Services.CleanupGuestCartService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// =========================================================
// 1. CẤU HÌNH SESSION (Gộp lại thành 1 cái duy nhất cho toàn bộ hệ thống)
// =========================================================
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "AuraHome_Global_Session";
    options.IdleTimeout = TimeSpan.FromHours(24); // Sống 24 tiếng
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// =========================================================
// 2. CẤU HÌNH COOKIE ĐĂNG NHẬP (Dùng chung, sống dai 30 ngày)
// =========================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuraHome_Global_Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30); // 30 ngày không cần đăng nhập lại
        options.SlidingExpiration = true;

        // Nếu chưa đăng nhập mà vào vùng cấm, mặc định đuổi về trang Khách (Admin tự có link riêng)
        options.LoginPath = "/Home/Index";
    });

// Cấu hình giới hạn form
builder.Services.Configure<FormOptions>(options => {
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
});

builder.Services.AddSignalR();

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

// =========================================================
// 3. THỨ TỰ BẮT BUỘC: Session -> Authentication -> Authorization
// =========================================================
app.UseSession();
app.UseAuthentication(); // 👈 RẤT QUAN TRỌNG: Mình vừa thêm dòng này cho bạn!
app.UseAuthorization();

// =========================================================
// 4. CẤU HÌNH ĐƯỜNG DẪN (ROUTE)
// =========================================================
app.MapHub<AISmartHome.Hubs.OrderHub>("/orderHub");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Admin}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");



app.Run();