using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using LaundryService.Api.Extensions;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Infrastructure;
using LaundryService.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromFile("notification-firebase-adminsdk.json")
});

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add Authentication using JWT Bearer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true, // Kiểm tra hạn của token
            ClockSkew = TimeSpan.Zero // Không có thời gian trễ khi so sánh thời gian hết hạn
        };
    });

builder.Services.AddAuthorization(); // Bắt buộc để dùng `[Authorize]`

//Add DBConnection
builder.Services.AddDbContext<LaundryServiceDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnection"));
});

// Add services to the container.
builder.Services.AddScoped<Func<LaundryServiceDbContext>>(provider => () => provider.GetRequiredService<LaundryServiceDbContext>());
builder.Services.AddScoped<DbFactory>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUtil, Util>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IFileStorageService, B2StorageService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ISubCategoryService, SubCategoryService>();
builder.Services.AddScoped<IServiceDetailService, ServiceDetailService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IExtraCategoryService, ExtraCategoryService>();
builder.Services.AddScoped<IExtraService, ExtraService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IMapboxService, MapboxService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Laundry Service API",
        Version = "v1",
        Description = "API Documentation for Laundry Service",
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));

    // Cấu hình bảo mật JWT trong Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey, // <--- Dùng apiKey thay vì Http
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập token theo định dạng: Bearer {your_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ISpeedSmsService, SpeedSmsService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>(); // Get IConfiguration directly
    var memoryCache = provider.GetRequiredService<IMemoryCache>(); // Get IMemoryCache from DI container
    return new SpeedSmsService(configuration, memoryCache); // Pass IConfiguration directly
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseMiddleware<GlobalExceptionMiddleware>(); // Xử lý lỗi toàn cục.
                                                    //đảm bảo rằng ứng dụng trả về phản hồi lỗi nhất quán và bảo mật.
}

app.UseHttpsRedirection();
// Cho phép truy cập các file tĩnh trong wwwroot
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Laundry Service");
    //c.DefaultModelsExpandDepth(-1); // Ẩn phần Models phía dưới mỗi trang Swagger UI
});

app.MapControllers();

app.Run();
