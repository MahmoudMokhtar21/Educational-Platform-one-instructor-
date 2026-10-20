using AutoMapper;
using Educatinal_Platform.Repositories;
using Educatinal_Platform.Services;
using EduPlatformAPI.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using System.Security.Claims;
using Educatinal_Platform.Middleware;
using Educatinal_Platform.Options;

using System.Text;
var builder = WebApplication.CreateBuilder(args);

// Multipart form sections larger than 64 KB are buffered to disk by ASP.NET
// Core in Path.GetTempPath(). On this machine the per-user temp directory
// denies file creation to the worker process, so every sizable lesson upload
// fails inside the framework before application code runs. Point TEMP/TMP at
// a writable app-local directory instead; no Windows permissions are changed.
var formBufferDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "form-buffer");
Directory.CreateDirectory(formBufferDir);
Environment.SetEnvironmentVariable("TEMP", formBufferDir);
Environment.SetEnvironmentVariable("TMP", formBufferDir);

if (builder.Environment.IsDevelopment())
{
    // Windows Event Log commonly requires elevated permissions. Local development
    // uses console/debug logging so a non-admin developer can still run the API.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

var paymentProvider = (builder.Configuration["Payment:Provider"] ?? "Mock").Trim();
if (!paymentProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase) &&
    !paymentProvider.Equals("Paymob", StringComparison.OrdinalIgnoreCase) &&
    !paymentProvider.Equals("Kashier", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Payment:Provider must be Mock, Paymob, or Kashier.");
}

// ==========================================
// 1. Add Controllers
// ==========================================
builder.Services.AddControllers();



// ==========================================
// 2. Add Swagger (Test APIs)
// ==========================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EduPlatform API", Version = "v1" });

    // ability to insert Token in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});
// ==========================================
// 3. Config MongoDB Connection
// ==========================================

// 3.1. Get Connection String from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("MongoDb");
var databaseName = builder.Configuration.GetValue<string>("DatabaseName");


// 3.2. Record MongoClient (is used once in app)
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    return new MongoClient(connectionString);
});

// 3.3. Record IMongoDatabase (DI for Repositories)
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
}
);

// ==========================================
// 4. Add Repositories
// ==========================================
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
//builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
builder.Services.AddScoped<ILessonRepository, LessonRepository>();
builder.Services.AddScoped<ITrackRepository, TrackRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISponsorRepository, SponsorRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ISiteSettingsRepository, SiteSettingsRepository>();
builder.Services.AddScoped<IStudentReviewRepository, StudentReviewRepository>();
builder.Services.AddScoped<IExternalVideoRepository, ExternalVideoRepository>();
builder.Services.AddScoped<IKnowledgeArticleRepository, KnowledgeArticleRepository>();
builder.Services.AddScoped<IConsultationRequestRepository, ConsultationRequestRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
builder.Services.AddScoped<ILessonCommentRepository, LessonCommentRepository>();
builder.Services.AddScoped<ICourseAccessCodeRepository, CourseAccessCodeRepository>();




// ==========================================
// 5. Add Services
// ==========================================
builder.Services.AddScoped<ICourseService,CourseService>();
//builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICourseLessonAccessService, CourseLessonAccessService>();
builder.Services.AddOptions<LessonProgressOptions>()
    .Bind(builder.Configuration.GetSection(LessonProgressOptions.SectionName))
    .Validate(options => options.VideoCompletionPercent is >= 50 and <= 100, "LessonProgress:VideoCompletionPercent must be between 50 and 100.")
    .Validate(options => options.MaximumPlaybackRate is >= 1m and <= 4m, "LessonProgress:MaximumPlaybackRate must be between 1 and 4.")
    .Validate(options => options.InitialProgressAllowanceSeconds is >= 1 and <= 120, "LessonProgress:InitialProgressAllowanceSeconds must be between 1 and 120.")
    .ValidateOnStart();
builder.Services.AddOptions<ImageUploadOptions>()
    .Bind(builder.Configuration.GetSection(ImageUploadOptions.SectionName))
    .Validate(options => options.MaxBytes > 0, "FileUploads:Images:MaxBytes must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITrackService, TrackService>();
builder.Services.AddScoped<ILessonService, LessonService>();
builder.Services.AddScoped<ILessonContentAccessService, LessonContentAccessService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddSingleton<PaymobWebhookVerifier>();
builder.Services.AddSingleton<KashierWebhookVerifier>();
if (paymentProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IPaymentService, MockPaymentService>();
}
else if (paymentProvider.Equals("Paymob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<PaymobPaymentService>(client => client.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddScoped<IPaymentService>(sp => sp.GetRequiredService<PaymobPaymentService>());
}
else if (paymentProvider.Equals("Kashier", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<KashierPaymentService>(client => client.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddScoped<IPaymentService>(sp => sp.GetRequiredService<KashierPaymentService>());
}
else
{
    throw new InvalidOperationException(
        $"Unknown Payment:Provider value '{paymentProvider}'. " +
        $"Valid values are: Mock, Paymob, Kashier.");
}
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<ISiteSettingsService, SiteSettingsService>();
builder.Services.AddScoped<IStudentReviewService, StudentReviewService>();
builder.Services.AddScoped<IContentHubService, ContentHubService>();
builder.Services.AddScoped<IConsultationRequestService, ConsultationRequestService>();
builder.Services.AddScoped<IAccessTokenService, JwtAccessTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<ILessonCommentService, LessonCommentService>();
builder.Services.AddScoped<ICourseAccessCodeService, CourseAccessCodeService>();


// ==========================================
// 6. Add AutoMapper
// ==========================================
builder.Services.AddAutoMapper(_ => { }, typeof(Program).Assembly);
 


//===========================================
// 7. JWT Authentication
// ==========================================
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "JWT:SecretKey must be configured through User Secrets or environment variables.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT:Issuer must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT:Audience must be configured.")
    .Validate(options => options.ExpiryMinutes == JwtOptions.AccessTokenLifetimeMinutes, "JWT:ExpiryMinutes must be exactly 360.")
    .ValidateOnStart();
builder.Services.AddOptions<RefreshTokenOptions>()
    .Bind(builder.Configuration.GetSection(RefreshTokenOptions.SectionName))
    .Validate(options => options.LifetimeDays > 0, "RefreshToken:LifetimeDays must be greater than zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "RefreshToken:CookieName must be configured.")
    .Validate(options => Enum.TryParse<SameSiteMode>(options.CookieSameSite, true, out _), "RefreshToken:CookieSameSite must be a valid SameSite mode.")
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSecretKey = jwtSettings.GetValue<string>(nameof(JwtOptions.SecretKey));

if (string.IsNullOrWhiteSpace(jwtSecretKey))
{
    throw new InvalidOperationException("JWT:SecretKey must be configured through User Secrets or environment variables.");
}

var key = Encoding.UTF8.GetBytes(jwtSecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = _ => Task.CompletedTask
    };

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],

        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],

        ValidateLifetime = true,

        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ==========================================
// 8. Config CORS (to be able to Communicate with Frontend)
// ==========================================
// The demo deployment serves the SPA and API from one origin, so CORS is not
// needed there. Cross-origin development must still list explicit origins
// because credentialed refresh cookies are enabled.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Any(origin => string.IsNullOrWhiteSpace(origin) || origin == "*"))
{
    throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit trusted frontend origins when refresh cookies are enabled.");
}

builder.Services.AddCors(options =>
{
    if (allowedOrigins.Length == 0) return;

    options.AddPolicy("Frontend",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
});



// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle


var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();

// Payment callback idempotency depends on these server-side uniqueness indexes.
using (var courseAccessCodeScope = app.Services.CreateScope())
{
    await courseAccessCodeScope.ServiceProvider.GetRequiredService<ICourseAccessCodeRepository>().EnsureIndexesAsync();
}

using (var paymentScope = app.Services.CreateScope())
{
    await paymentScope.ServiceProvider
        .GetRequiredService<IPaymentTransactionRepository>()
        .EnsureIndexesAsync();
}

using (var trackScope = app.Services.CreateScope())
{
    await trackScope.ServiceProvider
        .GetRequiredService<ITrackRepository>()
        .EnsureIndexesAsync();
}

using (var refreshTokenScope = app.Services.CreateScope())
{
    await refreshTokenScope.ServiceProvider
        .GetRequiredService<IRefreshTokenService>()
        .EnsureIndexesAsync();
}

using (var lessonCommentScope = app.Services.CreateScope())
{
    await lessonCommentScope.ServiceProvider
        .GetRequiredService<ILessonCommentRepository>()
        .EnsureIndexesAsync();
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AutoMapper.IConfigurationProvider>()
        .AssertConfigurationIsValid();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ==========================================
// 11. Active CORS
// ==========================================
if (allowedOrigins.Length > 0)
{
    app.UseCors("Frontend");
}

app.UseAuthentication();
app.UseAuthorization();

// Lesson files remain on local disk, but they must never be served by the
// anonymous static-file middleware. Authorized streaming is handled by
// LessonsController after the enrollment and preview checks have succeeded.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrWhiteSpace(path) &&
        (path.Equals("/uploads/lessons", StringComparison.OrdinalIgnoreCase) ||
         path.StartsWith("/uploads/lessons/", StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

app.UseStaticFiles();
app.MapControllers();

// Unknown /api routes must keep returning 404 instead of the SPA shell. The
// literal "api" segment makes this fallback pattern outrank the file fallback.
app.MapFallback("/api/{*path}", () => Results.NotFound());

// SPA deep links such as /courses/test-2 have no server endpoint; the built-in
// file fallback serves the React entry file so client-side routing resolves.
app.MapFallbackToFile("index.html");
app.Run();
