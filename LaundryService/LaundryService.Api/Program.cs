using DocumentFormat.OpenXml.Presentation;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using LaundryService.Api.Extensions;
using LaundryService.Api.Hub;
using LaundryService.Api.Services;
using LaundryService.Domain.Interfaces;
using LaundryService.Domain.Interfaces.Services;
using LaundryService.Infrastructure;
using LaundryService.Service;
using LaundryService.Service.BackgroundServices;
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
    Credential = GoogleCredential.FromFile("notification-laundry-firebase-adminsdk.json")
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/trackingHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
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
builder.Services.AddScoped<IExcelService, ExcelsService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IMapboxService, MapboxService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IFirebaseNotificationService, FirebaseNotificationService>();
builder.Services.AddScoped<IFirebaseStorageService, FirebaseStorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDashBoardServices, DashBoardService>();
builder.Services.AddScoped<IOrderAssignmentHistoryService, OrderAssignmentHistoryService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddHostedService<AssignmentAutoFailService>();
builder.Services.AddScoped<ITrackingPermissionService, TrackingPermissionService>();


builder.Services.AddSignalR(options =>
{
    //Test lỗi kết nối server
    options.EnableDetailedErrors = true; // Bật chế độ lỗi chi tiết cho SignalR
});



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
        //Type = SecuritySchemeType.ApiKey, // <--- Dùng apiKey thay vì Http
        Type = SecuritySchemeType.Http,     // <-- đổi thành Http. Dùng cái này để khỏi nhập 'Bearer ' trước token
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Chỉ cần nhập JWT token (không cần 'Bearer ' prefix)."
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
// Đăng ký IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ISpeedSmsService, SpeedSmsService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>(); // Get IConfiguration directly
    var memoryCache = provider.GetRequiredService<IMemoryCache>(); // Get IMemoryCache from DI container
    return new SpeedSmsService(configuration, memoryCache); // Pass IConfiguration directly
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
                "https://captonse-project-fe.vercel.app", // FE trên vercel
                "https://laundry.vuhai.me",               // domain chính
                "http://localhost:5173",                  // dev local nếu cần
                "http://localhost:4173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Phải có nếu bạn dùng JWT/Cookie
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

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Laundry Service");
    //c.DefaultModelsExpandDepth(-1); // Ẩn phần Models phía dưới mỗi trang Swagger UI
});

app.MapControllers();
app.MapHub<ChatHub>("/chatHub"); // Đăng ký SignalR Hub
app.MapHub<TrackingHub>("/trackingHub");

app.Run();