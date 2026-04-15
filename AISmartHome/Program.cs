using AISmartHome.Data;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AISmartHomeDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AISmartHome.Services.TaoTinTuDong>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddMemoryCache(); // Thêm dòng này để dùng IMemoryCache

builder.Services.AddHttpContextAccessor();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Thời gian sống của phiên
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // RẤT QUAN TRỌNG: Bỏ qua kiểm tra Cookie Consent
});



builder.Services.Configure<FormOptions>(options => {
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
});

var app = builder.Build();

// Thêm dòng này vào khu vực builder.Services

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

// 1. THÊM ĐOẠN NÀY DÀNH CHO ADMIN AREA
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Admin}/{action=Index}/{id?}");

// 2. ROUTE MẶC ĐỊNH CỦA BẠN DÀNH CHO KHÁCH HÀNG (Giữ nguyên)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
