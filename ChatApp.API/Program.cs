using ChatApp.API.Hubs;
using ChatApp.API.Middleware;
using ChatApp.BLL.Interfaces;
using ChatApp.BLL.Services;
using ChatApp.DAL;
using ChatApp.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── PostgreSQL + EF Core ──────────────────────────────────────────────────────
var conn = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing 'Postgres' connection string.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(conn, npg =>
    {
        npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        npg.MigrationsAssembly("ChatApp.DAL");
    })
    .UseSnakeCaseNamingConvention()
    .EnableDetailedErrors(builder.Environment.IsDevelopment())
    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
);

// ── DAL / BLL ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUnitOfWork,    UnitOfWork>();
builder.Services.AddScoped<IUserService,   UserService>();
builder.Services.AddScoped<IRoomService,   RoomService>();
builder.Services.AddScoped<IMessageService,MessageService>();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors     = builder.Environment.IsDevelopment();
    o.ClientTimeoutInterval    = TimeSpan.FromSeconds(60);
    o.KeepAliveInterval        = TimeSpan.FromSeconds(15);
    o.MaximumReceiveMessageSize = 64 * 1024;
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
              ?? ["http://localhost:3000","http://localhost:5173"];

builder.Services.AddCors(o => o.AddPolicy("CorsPolicy", p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "NexChat API", Version = "v1" }));

var app = builder.Build();

// ── Migrate on startup ────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try { await db.Database.MigrateAsync(); log.LogInformation("DB migrated."); }
    catch (Exception ex) { log.LogError(ex, "Migration failed."); throw; }
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
