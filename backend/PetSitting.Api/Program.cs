using Azure.Identity;
using System.Security.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PetSitting.Infrastructure.Persistence;
using PetSitting.Application.Availability;
using PetSitting.Application.Common;
using PetSitting.Application.Pets;
using PetSitting.Application.Users;
using PetSitting.Infrastructure.Persistence.Repositories.Availability;
using PetSitting.Infrastructure.Persistence.Repositories.Pets;
using PetSitting.Infrastructure.Persistence.Repositories.Users;
using PetSitting.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");

// Add services to the container.

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Request-shape validation failures (DataAnnotations, JSON binding) keep the { success, message } contract.
    options.InvalidModelStateResponseFactory = context =>
    {
        // JSON binding failures are keyed "$" / "$.field"; report them first, otherwise the null body adds a misleading "field is required".
        var json = context.ModelState.FirstOrDefault(e => e.Key.StartsWith('$') && e.Value!.Errors.Count > 0);
        var message = json.Key switch
        {
            "$" => "Invalid request body.",
            { } key => $"Invalid value for '{key[2..]}'.",
            null => context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "Invalid request."
        };
        return new BadRequestObjectResult(new { success = false, message });
    };
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPetRepository, PetRepository>();
builder.Services.AddScoped<ISitterAvailabilityRepository, SitterAvailabilityRepository>();

// Add managers (one per subsystem)
builder.Services.AddScoped<IUsersManager, UsersManager>();
builder.Services.AddScoped<IPetsManager, PetsManager>();
builder.Services.AddScoped<IAvailabilityManager, AvailabilityManager>();

// Add services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IBlobService, BlobService>();

// Configure JWT authentication
// Attempt to load Jwt:Key from Key Vault or create it if missing
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");

var keyVaultUri = builder.Configuration.GetValue<string>("KeyVaultUri") ?? "https://akster-vault.vault.azure.net/";
try
{
    var credential = new DefaultAzureCredential();
    var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
    var secretName = "Jwt--Key";

    if (string.IsNullOrEmpty(jwtKey))
    {
        // Try to get from Key Vault
        try
        {
            var secret = secretClient.GetSecret(secretName);
            jwtKey = secret.Value.Value;
            builder.Configuration["Jwt:Key"] = jwtKey;
            Console.WriteLine("Loaded Jwt:Key from Key Vault.");
        }
        catch (Azure.RequestFailedException)
        {
            // Secret not found, generate and set
            var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var setSecret = secretClient.SetSecret(secretName, generated);
            jwtKey = generated;
            builder.Configuration["Jwt:Key"] = jwtKey;
            Console.WriteLine("Generated and stored Jwt:Key to Key Vault.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"KeyVault access skipped or failed: {ex.Message}");
}

if (!string.IsNullOrEmpty(jwtKey))
{
    var key = System.Text.Encoding.UTF8.GetBytes(jwtKey);

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection.GetValue<string>("Issuer"),
            ValidAudience = jwtSection.GetValue<string>("Audience"),
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key)
        };
    });

    builder.Services.AddAuthorization();
}

builder.Configuration.AddAzureKeyVault(
    new Uri("https://akster-vault.vault.azure.net/"),
    new DefaultAzureCredential()
);

var app = builder.Build();

// Initialize and seed the database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var retries = 10;
        while (retries > 0)
        {
            try
            {
                var db = services.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
                break;
            }
            catch (Exception ex)
            {
                retries--;
                Console.WriteLine($"DB not ready, retrying... ({retries} attempts left). Error: {ex.Message}");
                Thread.Sleep(5000);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");
        throw;
    }
}

// Unexpected exceptions surface as 500 with the same shape the frontend expects from managers.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { success = false, message = "An unexpected error occurred." });
}));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// The frontend calls http://localhost:5072 in dev; a redirect to https would drop its Authorization header.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
