using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Implementation.Auth;
using Bookmerang.Api.Services.Interfaces.Books;
using Bookmerang.Api.Services.Implementation.Books;
using Bookmerang.Api.Models.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Data;
using Bookmerang.Api.Middleware;          
using Microsoft.OpenApi.Models;           
using Npgsql;

DotNetEnv.Env.Load();
//DotNetEnv.Env.Load(File.Exists(".env.local") ? ".env.local" : ".env"); //para desarrollo

var builder = WebApplication.CreateBuilder(args);

builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("CONNECTION_STRING");
builder.Configuration["Supabase:JwtSecret"] = Environment.GetEnvironmentVariable("SUPABASE_JWT_SECRET");
builder.Configuration["Supabase:Url"] = Environment.GetEnvironmentVariable("SUPABASE_URL");

// Registrar enums de PostgreSQL
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
dataSourceBuilder.MapEnum<CoverType>("cover_type");
dataSourceBuilder.MapEnum<BookCondition>("book_condition");
dataSourceBuilder.MapEnum<BookStatus>("book_status");
dataSourceBuilder.MapEnum<PricingPlan>("pricing_plan");
dataSourceBuilder.UseNetTopologySuite();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource, o => o.UseNetTopologySuite()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                "http://localhost:8081",
                "http://localhost:3000"
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
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IGenreRepository, GenreRepository>();
builder.Services.AddScoped<ILanguageRepository, LanguageRepository>(); 

// Temporal: hasta que termine marcher
builder.Services.AddScoped<IMatcherNotifier, MatcherNotifier>(); // ← nuevo

// ===== CONTROLLERS Y SWAGGER =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Bookmerang API",
        Version = "v1",
        Description = """
            API de Bookmerang.

            ## Cómo autenticarse
            1. Abrir la base de datos y en authentication, crear un usuario con email y password (o usar uno existente).
            2. Ejecuta en PowerShell con tus credenciales:
            ```
            Invoke-RestMethod -Method POST `
              -Uri "http://127.0.0.1:54321/auth/v1/token?grant_type=password" `
              -Headers @{ "apikey" = "tu_publishable_authentication_key" } `
              -ContentType "application/json" `
              -Body '{"email":"tu_email","password":"tu_password"}'
            ```
            3. Copia el valor de `access_token` de la respuesta
            4. Pulsa el botón **Authorize** 🔒 arriba a la derecha
            5. Escribe: `<tu_access_token>`
            6. Registrate en la apliacación.
            """
    });

    // Botón Authorize en Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduce el JWT de Supabase. Ejemplo: `Bearer eyJhbGc...`"
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
            []
        }
    });

    c.EnableAnnotations(); // Activa [SwaggerResponse] en los controladores
});

var app = builder.Build();

// ===== SEED DE DATOS (TEMPORAL, NO SE COMO SE HARÁ PERO HECHO PARA LAS PRUEBAS DE SUBIR LIBRO)=====
// Inserta géneros e idiomas en la BD al arrancar si no existen
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DataSeeder.SeedAsync(db);
}

app.UseCors();

// ===== MIDDLEWARE DE EXCEPCIONES =====
// Convierte excepciones en respuestas HTTP con JSON automáticamente
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bookmerang API v1");
        c.DisplayRequestDuration();
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();