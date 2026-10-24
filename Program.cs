using System.Reflection;
using Microsoft.AspNetCore.Diagnostics;
using System.Text;
using Canteen.BAL.Interfaces;
using Canteen.BAL.Services;
using Canteen.DAL.Data;
using Canteen.DAL.Interfaces;
using Canteen.DAL.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<CanteenDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly("Canteen.DAL")));

// ─── DAL – Repositories ──────────────────────────────────────────────────────
builder.Services.AddScoped<ICanteenRepository, CanteenRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICanteenTableRepository, CanteenTableRepository>();
builder.Services.AddScoped<IOwnerRepository, OwnerRepository>();

// ─── BAL – Services ──────────────────────────────────────────────────────────
builder.Services.AddScoped<ICanteenService, CanteenService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICanteenTableService, CanteenTableService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ─── JWT Authentication ──────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is missing.");

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is missing.");

var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is missing.");

byte[] keyBytes;
try
{
    keyBytes = Convert.FromBase64String(jwtKey);
}
catch
{
    keyBytes = Encoding.UTF8.GetBytes(jwtKey);
}

if (keyBytes.Length < 32)
{
    throw new InvalidOperationException("JWT key must contain at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ─── CORS ────────────────────────────────────────────────────────────────────
// Origins are loaded from Cors:AllowedOrigins in appsettings (per-environment).
// Production values must be set via environment variables or a secret manager.
const string CorsPolicyName = "AllowFrontend";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // Fail fast: a missing origins list is a misconfiguration, not a
            // reason to silently open the API to every origin.
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must contain at least one origin.");
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ─── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ─── Swagger / Swashbuckle with JWT Bearer Support ───────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Canteen Management API",
        Version = "v1",
        Description = "REST API for managing canteens, tables, categories, products, and owner authentication."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer <your_token>"
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
            Array.Empty<string>()
        }
    });

    // Include XML comments so Swagger shows <summary> doc-comment text.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ─── Auto-apply migrations on startup (dev-friendly) ────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CanteenDbContext>();
    if (!app.Environment.IsEnvironment("Testing") && db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
    {
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
    }
}

// ─── Middleware pipeline ──────────────────────────────────────────────────────

// Global exception handler: returns a structured JSON 500 in all environments
// without leaking internal details (stack traces, EF queries, etc.) to clients.
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var isDev = app.Environment.IsDevelopment();

        // In development, include the exception message to assist debugging.
        // In production, return only a generic message to avoid leaking internals.
        var detail = isDev && feature?.Error != null
            ? feature.Error.Message
            : "An unexpected error occurred. Please try again later.";

        await context.Response.WriteAsJsonAsync(new
        {
            status = 500,
            error  = "Internal Server Error",
            detail
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Canteen Management API v1");
        c.RoutePrefix = string.Empty;   // serve Swagger UI at root "/"
    });
}

// CORS must run before auth so pre-flight OPTIONS requests are handled correctly.
app.UseCors(CorsPolicyName);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
