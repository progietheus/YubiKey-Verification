using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Yubikey_Verification.Models;

namespace Yubikey_Verification.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    private readonly string _baseVerifyUrl;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _ttlSeconds;
    private readonly RSA _rsa;
    private readonly string _kid;

    public JwtService(IConfiguration configuration, IDistributedCache cache)
    {
        _configuration = configuration;
        _cache = cache;
        _baseVerifyUrl = _configuration["JWT:BaseVerifyUrl"] ?? throw new InvalidOperationException("BaseVerifyUrl not configured");
        _issuer = _configuration["JWT:Issuer"] ?? throw new InvalidOperationException("Issuer not configured");
        _audience = _configuration["JWT:Audience"] ?? throw new InvalidOperationException("Audience not configured");
        _ttlSeconds = _configuration.GetValue<int>("JWT:TtlSeconds", 300);
        _kid = _configuration["JWT:Kid"] ?? throw new InvalidOperationException("Kid not configured");
        
        _rsa = RSA.Create();
        var privateKeyPem = _configuration["JWT:PrivateKey"] ?? throw new InvalidOperationException("PrivateKey not configured");
        _rsa.ImportFromPem(privateKeyPem);
    }

    public async Task<(string token, string verifyUrl, string jti, string expiresAt)> CreateVerificationTokenAsync(
        string userId, string factorId)
    {
        var jti = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddSeconds(_ttlSeconds);
        var verifyUrl = $"{_baseVerifyUrl.TrimEnd('/')}/verify/{jti}";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim("factorId", factorId),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("verifyUrl", verifyUrl)
        };

        var key = new RsaSecurityKey(_rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: exp.UtcDateTime,
            signingCredentials: credentials
        );

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.WriteToken(token);

        // Store session in Redis
        var sessionData = new VerificationSession
        {
            Jti = jti,
            UserId = userId,
            FactorId = factorId,
            Status = "pending"
        };

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = exp
        };

        await _cache.SetAsync(
            $"verification_session:{jti}",
            JsonSerializer.SerializeToUtf8Bytes(sessionData),
            options
        );

        return (jwt, verifyUrl, jti, exp.ToString("o"));
    }

    public async Task<VerificationSession?> GetSessionAsync(string jti)
    {
        var data = await _cache.GetAsync($"verification_session:{jti}");
        if (data == null) return null;

        return JsonSerializer.Deserialize<VerificationSession>(data);
    }

    public async Task<bool> MarkSessionAsUsedAsync(string jti)
    {
        var session = await GetSessionAsync(jti);
        if (session == null) return false;

        session.Status = "used";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        await _cache.SetAsync(
            $"verification_session:{jti}",
            JsonSerializer.SerializeToUtf8Bytes(session),
            options
        );

        return true;
    }
}