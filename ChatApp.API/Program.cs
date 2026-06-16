using System.Text;
using ChatApp.API.Middleware;
using ChatApp.BLL.Interfaces;
using ChatApp.BLL.Services;
using ChatApp.DAL;
using ChatApp.DAL.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── 1. PostgreSQL + EF Core ───────────────────────────────────────────────────
var conn = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing 'Postgres' connection string.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(conn, npg =>
    {
        npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        npg.MigrationsAssembly("ChatApp.DAL"); // migrations live in the DAL project
    })
    .UseSnakeCaseNamingConvention()            // PostgreSQL convention: snake_case columns
    .EnableDetailedErrors(builder.Environment.IsDevelopment())
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
);

// ── 2. DI — N-Layer wiring ────────────────────────────────────────────────────
//
//  API Controller
//      ↓ depends on IAuthService
//  BLL AuthService (business rules, token generation)
//      ↓ depends on IUnitOfWork
//  DAL UnitOfWork  (EF Core, PostgreSQL)
//
builder.Services.AddScoped<IUnitOfWork,  UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ── 3. JWT Authentication ─────────────────────────────────────────────────────
//
//  AddAuthentication  → registers the auth middleware machinery.
//  AddJwtBearer       → tells ASP.NET Core HOW to validate incoming Bearer tokens:
//                       check signature, issuer, audience, and expiry.
//
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Missing 'Jwt:Key'.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,         // rejects expired tokens
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero  // no grace period after expiry
        };
    });

builder.Services.AddAuthorization();

// ── 4. CORS ───────────────────────────────────────────────────────────────────
var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
              ?? ["http://localhost:3000"];

builder.Services.AddCors(o => o.AddPolicy("CorsPolicy", p =>
    p.WithOrigins(origins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials())); // needed so the browser sends HttpOnly cookies

// ── 5. MVC + Swagger ──────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger to show a "Authorize" button for JWT Bearer tokens
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "NexChat API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT access token here. Example: eyJhbGci..."
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

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── 6. Middleware pipeline (order matters!) ───────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>(); // must be first — catches everything below

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");       // before auth so preflight OPTIONS requests work
app.UseAuthentication();         // reads and validates the JWT
app.UseAuthorization();          // checks [Authorize] attributes

app.MapControllers();

app.Run();
