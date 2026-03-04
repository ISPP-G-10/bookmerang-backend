using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Implementation.Auth;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Services.Implementation.Chats;
using Bookmerang.Api.Services.Interfaces.Genres;
using Bookmerang.Api.Services.Implementation.Genres;
using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Bookmerang.Api.Services.Implementation.Matcher;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

//DotNetEnv.Env.Load();
DotNetEnv.Env.Load(File.Exists(".env.local") ? ".env.local" : ".env"); //para desarrollo

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("CONNECTION_STRING");
builder.Configuration["Supabase:JwtSecret"] = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET");
builder.Configuration["Supabase:Url"] = Environment.GetEnvironmentVariable("SUPABASE_URL");
builder.Configuration["Supabase:ServiceRoleKey"] = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");

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
#pragma warning restore CS0618

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        o => o.UseNetTopologySuite()
    ));

// ===== CONFIGURACIÓN =====
builder.Services.Configure<MatcherSettings>(
    builder.Configuration.GetSection(MatcherSettings.SectionName));

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                "http://localhost:8081",
                "http://localhost:3000",
                "https://bookmerang-frontend.onrender.com",
                "https://bookmerang-front.jollytree-74260255.spaincentral.azurecontainerapps.io"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

// ===== JWT =====
var supabaseUrl = builder.Configuration["Supabase:Url"];

var httpClient = new HttpClient();
var jwksJson = httpClient.GetStringAsync($"{supabaseUrl}/auth/v1/.well-known/jwks.json").Result;
var jwks = new JsonWebKeySet(jwksJson);
var signingKeys = jwks.GetSigningKeys();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            ValidateIssuer = true,
            ValidIssuer = $"{supabaseUrl}/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidAlgorithms = new[] { "ES256" }
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

// ===== SERVICIOS =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<IGenreService, GenreService>();
builder.Services.AddScoped<IMatcherService, MatcherService>();
builder.Services.AddScoped<IExchangeService, ExchangeService>();
builder.Services.AddScoped<IExchangeMeetingService, ExchangeMeetingService>();

// ===== CONTROLLERS Y SWAGGER =====
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();

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