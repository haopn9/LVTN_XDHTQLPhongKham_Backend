using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PhongKhamBackend.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ===== 1. DbContext =====
builder.Services.AddDbContext<QuanLyPhongKhamDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== 2. Memory Cache (lưu login attempts & token blacklist) =====
builder.Services.AddMemoryCache();

// ===== 3. JWT Authentication =====
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
});

// ========== Build App ==========
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");       // ✅ CORS phải đứng TRƯỚC redirect & auth
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
