using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Implementation.Auth;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Services.Implementation.Chats;
using Bookmerang.Api.Services.Interfaces.Genres;
using Bookmerang.Api.Services.Implementation.Genres;
using Bookmerang.Api.Services.Interfaces.PilotUsers;
using Bookmerang.Api.Services.Implementation.PilotUsers;
using Bookmerang.Api.Services.Interfaces.Books;
using Bookmerang.Api.Services.Implementation.Books;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Bookmerang.Api.Services.Implementation.Matcher;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Bookmerang.Api.Services.Implementation.Inkdrops;
using Bookmerang.Api.Services.Interfaces.Streaks;
using Bookmerang.Api.Services.Implementation.Streaks;
using Bookmerang.Api.Services.Interfaces.Communities;
using Bookmerang.Api.Services.Implementation.Communities;
using Bookmerang.Api.Validators.Communities;
using Bookmerang.Api.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Bookmerang.Api.Services.Interfaces.Bookdrop;
using Bookmerang.Api.Services.Implementation.Bookdrop;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Bookmerang.Api.Services.Implementation.Bookspots;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Bookmerang.Api.Services.Implementation.Subscriptions;
using Stripe;
using Bookmerang.Api.Services.Interfaces.Leveling;
using Bookmerang.Api.Services.Implementation.Leveling;

//DotNetEnv.Env.Load();
DotNetEnv.Env.Load(System.IO.File.Exists(".env.local") ? ".env.local" : ".env"); //para desarrollo

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("CONNECTION_STRING");
builder.Configuration["Supabase:JwtSecret"] = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET");
builder.Configuration["Supabase:Url"] = Environment.GetEnvironmentVariable("SUPABASE_URL");
builder.Configuration["Supabase:ServiceRoleKey"] = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
builder.Configuration["Auth:JwtSecret"] = Environment.GetEnvironmentVariable("JWT_SECRET");
builder.Configuration["Auth:JwtIssuer"] = Environment.GetEnvironmentVariable("JWT_ISSUER");
builder.Configuration["Auth:JwtAudience"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
builder.Configuration["Auth:JwtAccessTokenMinutes"] = Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_MINUTES");

// Stripe
var stripeSecretKeyEnv = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
if (!string.IsNullOrEmpty(stripeSecretKeyEnv))
    builder.Configuration["Stripe:SecretKey"] = stripeSecretKeyEnv;
var stripePriceId = Environment.GetEnvironmentVariable("STRIPE_PREMIUM_PRICE_ID");
if (!string.IsNullOrEmpty(stripePriceId))
    builder.Configuration["Stripe:PremiumPriceId"] = stripePriceId;
var stripeBookdropPriceId = Environment.GetEnvironmentVariable("STRIPE_BOOKDROP_PRICE_ID");
if (!string.IsNullOrEmpty(stripeBookdropPriceId))
    builder.Configuration["Stripe:BookdropPriceId"] = stripeBookdropPriceId;
var stripeSuccessUrl = Environment.GetEnvironmentVariable("STRIPE_SUCCESS_URL");
if (!string.IsNullOrEmpty(stripeSuccessUrl))
    builder.Configuration["Stripe:SuccessUrl"] = stripeSuccessUrl;
var stripeCancelUrl = Environment.GetEnvironmentVariable("STRIPE_CANCEL_URL");
if (!string.IsNullOrEmpty(stripeCancelUrl))
    builder.Configuration["Stripe:CancelUrl"] = stripeCancelUrl;
var stripeBookdropSuccessUrl = Environment.GetEnvironmentVariable("STRIPE_BOOKDROP_SUCCESS_URL");
if (!string.IsNullOrEmpty(stripeBookdropSuccessUrl))
    builder.Configuration["Stripe:BookdropSuccessUrl"] = stripeBookdropSuccessUrl;
var stripeBookdropCancelUrl = Environment.GetEnvironmentVariable("STRIPE_BOOKDROP_CANCEL_URL");
if (!string.IsNullOrEmpty(stripeBookdropCancelUrl))
    builder.Configuration["Stripe:BookdropCancelUrl"] = stripeBookdropCancelUrl;
var stripePublishableKey = Environment.GetEnvironmentVariable("STRIPE_PUBLISHABLE_KEY");
if (!string.IsNullOrEmpty(stripePublishableKey))
    builder.Configuration["Stripe:PublishableKey"] = stripePublishableKey;
var bookdropPaymentRequired = Environment.GetEnvironmentVariable("BOOKDROP_PAYMENT_REQUIRED");
if (!string.IsNullOrEmpty(bookdropPaymentRequired))
    builder.Configuration["Bookdrop:RequirePayment"] = bookdropPaymentRequired;

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

#pragma warning disable CS0618 // Global type mapper is obsolete in Npgsql 7.0+ but needed for enum mapping with NTS
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ChatType>("chat_type", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BooksExtension>("books_extension", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookStatus>("book_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookCondition>("book_condition", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<CoverType>("cover_type", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<SwipeDirection>("swipe_direction", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<MatchStatus>("match_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ExchangeStatus>("exchange_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ExchangeMode>("exchange_mode", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ExchangeMeetingStatus>("exchange_meeting_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<CommunityStatus>("community_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<CommunityRole>("community_role", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<MeetupStatus>("meetup_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<MeetupAttendanceStatus>("meetup_attendance_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookspotStatus>("bookspot_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<PricingPlan>("pricing_plan", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookdropExchangeStatus>("bookdrop_exchange_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<SubscriptionStatus>("subscription_status", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<SubscriptionPlatform>("subscription_platform", new NpgsqlNullNameTranslator());
Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<InkdropsActionType>("inkdrops_action_type", new NpgsqlNullNameTranslator());
#pragma warning restore CS0618

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        o => o.UseNetTopologySuite()
    ));

builder.Services.AddHttpClient();

// ===== CONFIGURACIÓN =====
builder.Services.Configure<MatcherSettings>(
    builder.Configuration.GetSection(MatcherSettings.SectionName));

// ===== RATE LIMITING =====
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("swipe", httpContext =>
    {
        var userId = httpContext.User.FindFirstValue(
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "anon";
        return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 30,          // 30 swipes
            Window = TimeSpan.FromMinutes(1), // por minuto
            SegmentsPerWindow = 6,     // ventanas de 10 s
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                "http://localhost:8081",
                "http://localhost:3000",
                "https://bookmerang-frontend.onrender.com",
                "https://bookmerang-front.jollytree-74260255.spaincentral.azurecontainerapps.io",
                "https://bookmerang-frontend.whitedune-16348441.spaincentral.azurecontainerapps.io"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

// ===== JWT =====
var jwtSecret = builder.Configuration["Auth:JwtSecret"] ?? "";
var jwtIssuer = builder.Configuration["Auth:JwtIssuer"] ?? "bookmerang-api";
var jwtAudience = builder.Configuration["Auth:JwtAudience"] ?? "bookmerang-client";

if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException("JWT_SECRET debe estar configurado y tener al menos 32 caracteres.");
}

var jwtKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtKey,
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Bearer "))
                {
                    context.Token = authHeader.Substring(7).Trim();
                }
                return Task.CompletedTask;
            }
        };
    });

// ===== AUTORIZACIÓN (policies por tipo de usuario) =====
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BookdropOnly", policy =>
        policy.RequireClaim("user_type", ((int)BaseUserType.BOOKDROP_USER).ToString()));
    options.AddPolicy("UserOnly", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim("user_type", ((int)BaseUserType.USER).ToString()) ||
            ctx.User.HasClaim("user_type", ((int)BaseUserType.ADMIN).ToString())));
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("user_type", ((int)BaseUserType.ADMIN).ToString()));
});

// ===== SERVICIOS =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<IGenreService, GenreService>();
builder.Services.AddScoped<IPilotUsersService, PilotUsersService>();
builder.Services.AddScoped<IWeeklyFeedbackMailService, WeeklyFeedbackMailService>();
builder.Services.AddScoped<IMatcherService, MatcherService>();
builder.Services.AddHostedService<SwipeCleanupHostedService>();
builder.Services.AddScoped<IExchangeService, ExchangeService>();
builder.Services.AddScoped<IExchangeMeetingService, ExchangeMeetingService>();
builder.Services.AddScoped<IInkdropsService, InkdropsService>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddHostedService<StreakMaintenanceHostedService>();

builder.Services.AddHostedService<WeeklyFeedbackMailService>();
// Communities
builder.Services.AddScoped<ICommunityService, CommunityService>();
builder.Services.AddScoped<IMeetupService, MeetupService>();
builder.Services.AddScoped<ICommunityLibraryService, CommunityLibraryService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateMeetupRequestValidator>();

// Books
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IGenreRepository, GenreRepository>();
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>();

// Bookdrop (establecimientos)
builder.Services.AddScoped<IBookdropService, BookdropService>();
builder.Services.AddScoped<IBookDropExchangeService, BookDropExchangeService>();

// Bookspots
builder.Services.AddScoped<IBookspotRepository, BookspotRepository>();
builder.Services.AddScoped<IBookspotService, BookspotService>();
builder.Services.AddScoped<IBookspotValidationRepository, BookspotValidationRepository>();
builder.Services.AddScoped<IBookspotValidationService, BookspotValidationService>();

// Subscriptions (Premium)
builder.Services.AddScoped<ISubscriptionService, Bookmerang.Api.Services.Implementation.Subscriptions.SubscriptionService>();
builder.Services.AddScoped<IStripeSubscriptionService, StripeSubscriptionService>();
builder.Services.AddHostedService<SubscriptionExpirationHostedService>();
builder.Services.AddHostedService<MonthlyRankingRewardHostedService>();

// Configure Stripe
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"] ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
if (!string.IsNullOrEmpty(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

// Leveling system
builder.Services.AddScoped<ILevelingService, LevelingService>();

// ===== CONTROLLERS Y SWAGGER =====
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations();
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Introduce el token JWT: Bearer {token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.ExecuteSqlRaw("ALTER TABLE base_users ADD COLUMN IF NOT EXISTS password_hash text;");
    var sequences = new[]
    {
        "SELECT setval('books_id_seq',            COALESCE((SELECT MAX(id) FROM books), 0) + 1, false)",
        "SELECT setval('book_photos_id_seq',       COALESCE((SELECT MAX(id) FROM book_photos), 0) + 1, false)",
        "SELECT setval('swipes_id_seq',            COALESCE((SELECT MAX(id) FROM swipes), 0) + 1, false)",
        "SELECT setval('matches_id_seq',           COALESCE((SELECT MAX(id) FROM matches), 0) + 1, false)",
        "SELECT setval('messages_id_seq',          COALESCE((SELECT MAX(id) FROM messages), 0) + 1, false)",
        "SELECT setval('exchanges_id_seq',         COALESCE((SELECT MAX(id) FROM exchanges), 0) + 1, false)",
        "SELECT setval('user_preferences_id_seq',  COALESCE((SELECT MAX(id) FROM user_preferences), 0) + 1, false)",
        "SELECT setval('communities_id_seq',       COALESCE((SELECT MAX(id) FROM communities), 0) + 1, false)",
    };
    foreach (var sql in sequences)
        db.Database.ExecuteSqlRaw(sql);
}

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => "Hello World!");

app.Run();
