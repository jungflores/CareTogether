using System.Text;
using CareTogether.Application;
using CareTogether.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("CareTogether") ?? "Server=localhost,1433;Database=CareTogether;User Id=sa;Password=CareTogether!2026;TrustServerCertificate=True";
builder.Services.AddDbContext<CareDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddScoped<PasswordService>(); builder.Services.AddScoped<ITokenService, TokenService>(); builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, CareTogether.Api.HttpCurrentUser>();
builder.Services.AddHttpClient();builder.Services.AddScoped<CareTogether.Api.InvitationDeliveryService>();
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "development-only-key-change-me-32-chars");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"], IssuerSigningKey = new SymmetricSecurityKey(key) });
builder.Services.AddAuthorization(); builder.Services.AddControllers(); builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new() { Title = "CareTogether API", Version = "v1" }); c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" }); c.AddSecurityRequirement(new() { [new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] }); });
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build(); app.UseSwagger(); app.UseSwaggerUI(); app.UseCors(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); app.MapHealthChecks("/health");
if (app.Configuration.GetValue("Database:ApplyMigrations", true)) using (var scope = app.Services.CreateScope()) await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<CareDbContext>(), scope.ServiceProvider.GetRequiredService<PasswordService>());
app.Run();
public partial class Program { }
