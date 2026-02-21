using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Implementation.Auth;

var builder = WebApplication.CreateBuilder(args);

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

// ===== SERVICIOS =====
builder.Services.AddScoped<IAuthService, AuthService>();


// ===== CONTROLLERS Y SWAGGER =====
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();

// ===== MIDDLEWARE =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===== HTTPS REDIRECTION Y ENDPOINTS =====
app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/", () => "Hello World!");
// app.UseAuthentication();
// app.UseAuthorization();

app.Run();
