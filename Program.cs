using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using UniLMS.API.Data;
using UniLMS.API.Hubs;
using UniLMS.API.Middleware;
using UniLMS.API.Services.Implementations;
using UniLMS.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Database (Supabase PostgreSQL) ──
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Supabase Client ──
var supabaseUrl = builder.Configuration["Supabase:Url"]!;
var supabaseKey = builder.Configuration["Supabase:ServiceRoleKey"]!;
var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey);
await supabaseClient.InitializeAsync();
builder.Services.AddSingleton(supabaseClient);

// ── JWT Authentication ──
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };

        // Allow JWT in SignalR query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/chat") ||
                     path.StartsWithSegments("/hubs/notifications")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── HttpClient for AI Microservice (chat streaming + query) ──
builder.Services.AddHttpClient("AiService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"]!);
    // SSE streams can run for several minutes on large documents
    client.Timeout = TimeSpan.FromSeconds(300);
});

// ── HttpClient for AI RAG Ingest Engine ──
builder.Services.AddHttpClient("AiIngestService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiIngestService:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(300); // Ingestion is slower than chat
});

// ── Services DI ──
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileStorageService, SupabaseStorageService>();

// ── SignalR ──
builder.Services.AddSignalR();

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!;
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

// ── Controllers & Swagger ──
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UniLMS API",
        Version = "v1",
        Description = "AI-Powered University Learning Management System API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
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

var app = builder.Build();

// ── Middleware Pipeline ──
app.UseMiddleware<ExceptionMiddleware>();

//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");

// ── Auto-Migrate & Seed on Startup ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    if (app.Environment.IsDevelopment())
    {
        await db.Database.MigrateAsync();
    }

    // Seed default admin user
    await UniLMS.API.Data.DbSeeder.SeedAsync(db, config);
}

// ── Ensure Supabase Storage buckets exist ──
// Runs on every startup; safe to call repeatedly (skips existing buckets).
// Add any new bucket names here instead of creating them manually in the dashboard.
await EnsureStorageBucketsAsync(supabaseClient, app.Logger);

app.Run();

// ── Local helpers ─────────────────────────────────────────────────────────────

static async Task EnsureStorageBucketsAsync(Supabase.Client supabase, ILogger logger)
{
    // All buckets this application needs. Add new ones here — never rely on
    // manual dashboard setup, which breaks on fresh Supabase project deploys.
    string[] required = ["lectures"];

    try
    {
        var existing   = await supabase.Storage.ListBuckets();
        var existingIds = existing?
            .Select(b => b.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? [];

        foreach (var bucket in required)
        {
            if (existingIds.Contains(bucket))
            {
                logger.LogInformation("[Storage] Bucket '{Bucket}' already exists.", bucket);
                continue;
            }

            await supabase.Storage.CreateBucket(bucket,
                new Supabase.Storage.BucketUpsertOptions { Public = true });

            logger.LogInformation("[Storage] Created Supabase Storage bucket '{Bucket}'.", bucket);
        }
    }
    catch (Exception ex)
    {

        logger.LogError(ex,
            "[Storage] Bucket auto-create failed. File uploads may not work until resolved.");
    }
}
