using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PhongKhamBackend.Models;
using PhongKhamBackend.Services;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using System.Text;

// Bắt buộc khai báo trước khi gọi bất kỳ API generate PDF nào (Document.GeneratePdf, v.v.),
// nếu không sẽ bị throw exception lúc runtime chứ không phải lỗi biên dịch/
QuestPDF.Settings.License = LicenseType.Community;

// Đăng ký font tiếng Việt để PDF hiển thị đúng dấu
// Cách 1: FontDiscoveryPaths (QuestPDF 2024.3+) – tự động quét thư mục Fonts
QuestPDF.Settings.FontDiscoveryPaths.Add(Path.Combine(AppContext.BaseDirectory, "Fonts"));

// Cách 2: RegisterFont thủ công (fallback, đảm bảo tương thích tất cả phiên bản)
using (var regularStream = File.OpenRead("Fonts/static/Roboto-Regular.ttf"))
    FontManager.RegisterFont(regularStream);

using (var boldStream = File.OpenRead("Fonts/static/Roboto-Bold.ttf"))
    FontManager.RegisterFont(boldStream);

var builder = WebApplication.CreateBuilder(args);

// ===== 1. DbContext =====
builder.Services.AddDbContext<QuanLyPhongKhamDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== 2. Memory Cache (lưu login attempts & token blacklist) =====
builder.Services.AddMemoryCache();

// ===== 3. Email Service (gửi OTP qua Gmail SMTP) =====
builder.Services.AddScoped<IEmailService, EmailService>();

// ===== 4. JWT Authentication =====
var jwtConfig = builder.Configuration.GetSection("Jwt");
var secretKey = jwtConfig["SecretKey"]!;
var issuer    = jwtConfig["Issuer"]!;
var audience  = jwtConfig["Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = issuer,
        ValidAudience            = audience,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew                = TimeSpan.Zero
    };

    // Kiểm tra blacklist: token đã đăng xuất bị từ chối ngay
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var cache      = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader["Bearer ".Length..].Trim();
                if (cache.TryGetValue($"blacklist_{token}", out _))
                    context.Fail("Token đã bị vô hiệu hóa. Vui lòng đăng nhập lại.");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ===== 4. Controllers =====
builder.Services.AddControllers();

// ===== 5. CORS – cho phép React frontend gọi API =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ===== 6. Swagger với hỗ trợ JWT =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Phòng Khám Nhật Tảo API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Nhập token: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // Fix lỗi "Failed to load API definition" khi có nested DTO class trong controller:
    // Dùng FullName thay vì ShortName để đảm bảo schema ID không bị trùng
    c.CustomSchemaIds(t => t.FullName!.Replace("+", "."));

    // Safety net: nếu có action trùng route, lấy cái đầu tiên thay vì throw exception
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
});

// ========== Build App ==========
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");       //  CORS phải đứng TRƯỚC redirect & auth
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
