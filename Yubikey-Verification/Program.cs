using Okta.Sdk.Abstractions;
using Okta.Sdk.Abstractions.Configuration;
using Okta.Sdk.Api;
using Okta.Sdk.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Yubikey_Verification.Models;
using Yubikey_Verification.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendOrigin",
        policyBuilder =>
        {
            policyBuilder
                .WithOrigins("http://localhost:3000", "file://") // Replace with your Electron app's actual origin(s
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()// Required for SignalR
                .SetIsOriginAllowed(_ => true);  // Allow any origin, including Electron apps
        });
});

// Configure SignalR with more lenient settings
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 102400; // 100 KB
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});


// Add EF Core DbContext for SQL Server
builder.Services.AddDbContext<VerificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Only load PEM files if not in design time (e.g., migrations)
RsaSecurityKey? rsaKey = null;
if (!builder.Environment.IsEnvironment("DesignTime"))
{
    var rsa = RSA.Create();
    string filePath = Path.Combine("public.pem");
    if (File.Exists(filePath))
    {
        var publicKeyText = File.ReadAllText(filePath);
        if (!string.IsNullOrEmpty(publicKeyText))
        {
            rsa.ImportFromPem(publicKeyText);
            rsaKey = new RsaSecurityKey(rsa);
        }
    }
}

if (rsaKey != null)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["JWT:Issuer"],
                ValidAudience = builder.Configuration["JWT:Audience"],
                IssuerSigningKey = rsaKey,
                ClockSkew = TimeSpan.Zero
            };
        });
}

builder.Services.AddScoped<IUserApi>(_ => new UserApi(
    new Configuration
    {
        OktaDomain = "", //specifcy okta url here
        Scopes = new HashSet<string> { "okta.users.read" },
        ClientId = builder.Configuration["Okta:ClientId"],
        AuthorizationMode = AuthorizationMode.SSWS,
        Token = builder.Configuration["Okta:ApiToken"]
    }));

builder.Services.AddScoped<IUserFactorApi>(_ => new UserFactorApi(
    new Configuration
    {
        OktaDomain = "", //specifcy okta url here
        Scopes = new HashSet<string> { "okta.users.read", "okta.factors.manage" },
        ClientId = builder.Configuration["Okta:ClientId"],
        AuthorizationMode = AuthorizationMode.SSWS,
        Token = builder.Configuration["Okta:ApiToken"]
    }));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Important: Order matters for middleware
app.UseRouting(); // Add this before CORS

app.UseHttpsRedirection();

app.UseCors("AllowFrontendOrigin");

// Add authentication middleware
if (rsaKey != null)
{
    app.UseAuthentication();
}
app.UseAuthorization();
// Map endpoints
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<VerificationHub>("/hubs/verification");
    endpoints.MapControllers();
});

app.Run();
