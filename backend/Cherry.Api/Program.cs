using Cherry.Core.Entities;
using Cherry.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<CherryDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

builder.Services.AddScoped<Cherry.Core.Interfaces.IStorageService, Cherry.Infrastructure.Services.DiskStorageService>();
builder.Services.AddScoped<Cherry.Core.Interfaces.IDocumentService, Cherry.Infrastructure.Services.PptxGeneratorService>();

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<CherryDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not found.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// app.UseHttpsRedirection();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<CherryDbContext>();
        
        // Ensure database is created and schema is up-to-date
        var databaseCreator = context.Database.GetService<IRelationalDatabaseCreator>() as RelationalDatabaseCreator;
        if (databaseCreator != null)
        {
            if (!databaseCreator.Exists()) await databaseCreator.CreateAsync();
            
            // Check if AspNetRoles exists as a proxy for the entire schema
            bool tablesExist = false;
            try { 
                await context.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"AspNetRoles\" LIMIT 1"); 
                tablesExist = true; 
            } catch { }

            if (!tablesExist)
            {
                try 
                {
                    await databaseCreator.CreateTablesAsync();
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07") // duplicate_table
                {
                    // Fallback for race conditions
                }
            }

            // Use EF Core Migrations to ensure all tables exist
            try
            {
                Console.WriteLine("=== Running EF Core Migrations ===");
                await context.Database.MigrateAsync();
                Console.WriteLine("=== Migrations Completed ===");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07") // duplicate_table
            {
                Console.WriteLine("=== Tables already exist, skipping migration ===");
            }
            
            // Simple Schema Migration: Add missing columns/tables if they don't exist (for backward compatibility)
            try 
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Templates\" ADD COLUMN IF NOT EXISTS \"Category\" text DEFAULT 'Standard'");
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Templates\" ADD COLUMN IF NOT EXISTS \"RegionId\" uuid");
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"TemplateId\" uuid");
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Services\" ADD COLUMN IF NOT EXISTS \"Unit\" text");
                
                // Create MasterSections table if it doesn't exist
                Console.WriteLine("Creating MasterSections table if not exists...");
                var createTableSql = "CREATE TABLE IF NOT EXISTS \"MasterSections\" (\"Id\" uuid NOT NULL PRIMARY KEY, \"SectionKey\" text NOT NULL, \"Name\" text NOT NULL, \"DefaultContentJson\" text NOT NULL, \"SortOrder\" integer NOT NULL DEFAULT 0, \"IsActive\" boolean NOT NULL DEFAULT true)";
                await context.Database.ExecuteSqlRawAsync(createTableSql);
                Console.WriteLine("MasterSections table created successfully.");
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"Schema migration warning: {ex.Message}");
            }
        }
        
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        
        Console.WriteLine("=== Starting Database Seeding ===");
        await DbInitializer.SeedAsync(context, userManager, roleManager, builder.Configuration);
        Console.WriteLine("=== Database Seeding Completed Successfully ===");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"!!! SEEDING ERROR: {ex.Message}");
        Console.WriteLine($"!!! Stack Trace: {ex.StackTrace}");
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
        throw; // Re-throw to prevent app from starting with broken database
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard();

app.MapControllers();

app.Run();
