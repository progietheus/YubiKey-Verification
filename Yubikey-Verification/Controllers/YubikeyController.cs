using Microsoft.AspNetCore.Mvc;
using Okta.Sdk.Api;
using Okta.Sdk.Client;
using Okta.Sdk.Model;
using Microsoft.AspNetCore.SignalR;
using Yubikey_Verification.Hubs;
using Yubikey_Verification.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Yubikey_verification.Models
{
    [ApiController]
    [Route("api/[controller]")]
    public partial class YubiKeyController : ControllerBase
    {
        private readonly IUserApi _userApi;
        private readonly IUserFactorApi _userFactorApi;
        private readonly IConfiguration _configuration;
        private readonly VerificationDbContext _db;
        private readonly IHubContext<VerificationHub> _hubContext;

        public YubiKeyController(
            IUserApi userApi, 
            IUserFactorApi userFactorApi,
            IConfiguration configuration,
            VerificationDbContext db,
            IHubContext<VerificationHub> hubContext)
        {
            _userApi = userApi;
            _userFactorApi = userFactorApi;
            _configuration = configuration;
            _db = db;
            _hubContext = hubContext;
        }

        [HttpPost("sessions")]
        [ProducesResponseType(typeof(VerificationSessionResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateVerificationSession([FromBody] CreateVerificationSessionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var jti = Guid.NewGuid().ToString();
                var now = DateTimeOffset.UtcNow;
                var ttlSeconds = _configuration.GetValue<int>("JWT:TtlSeconds", 300);
                var exp = now.AddSeconds(ttlSeconds);
                var baseVerifyUrl = _configuration["JWT:BaseVerifyUrl"] ?? throw new InvalidOperationException("BaseVerifyUrl not configured");
                var verifyUrl = $"{baseVerifyUrl.TrimEnd('/')}/verify/{jti}";

                // Create claims
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, request.UserId),
                    new Claim("factorId", request.FactorId),
                    new Claim(JwtRegisteredClaimNames.Jti, jti),
                    new Claim("verifyUrl", verifyUrl)
                };

                // Get RSA key and create credentials
                var rsa = RSA.Create();

                // With this line:
                string filePath = Path.Combine("private.pem");
                var privateKeyText = System.IO.File.ReadAllText(filePath) ?? throw new InvalidOperationException("PrivateKey not configured"); 
                rsa.ImportFromPem(privateKeyText.ToCharArray());

                var key = new RsaSecurityKey(rsa);
                var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
                {
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                };

                // Create JWT token
                var token = new JwtSecurityToken(
                    issuer: _configuration["JWT:Issuer"] ?? throw new InvalidOperationException("Issuer not configured"),
                    audience: _configuration["JWT:Audience"] ?? throw new InvalidOperationException("Audience not configured"),
                    claims: claims,
                    notBefore: now.UtcDateTime,
                    expires: exp.UtcDateTime,
                    signingCredentials: credentials
                );

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.WriteToken(token);

                // Store session in SQL
                var sessionData = new VerificationSession
                {
                    Jti = jti,
                    UserId = request.UserId,
                    FactorId = request.FactorId,
                    Status = "pending",
                    ExpiresAt = exp.UtcDateTime
                };
                _db.VerificationSessions.Add(sessionData);
                await _db.SaveChangesAsync();

                var response = new VerificationSessionResponse
                {
                    Token = jwt,
                    VerifyUrl = verifyUrl,
                    Jti = jti,
                    ExpiresAt = exp.ToString("o")
                };

                Response.Headers.Append("Cache-Control", "no-store");
                Response.Headers.Append("Pragma", "no-cache");

                return CreatedAtAction(nameof(CreateVerificationSession), response);
            }
            catch (Exception ex)
            {
                // Log the error but don't expose details
                return StatusCode(500, new { message = "An error occurred while creating the verification session.", exceptionObject = ex });
            }
        }

        [HttpPost("verify/{jti}")]
        public async Task<IActionResult> VerifyYubiKey(string jti, [FromBody] VerifyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PassCode))
            {
                await NotifyVerificationStatus(jti, false, "Passcode is required.");
                return BadRequest(new VerifyResponse { Success = false, Message = "Passcode is required." });
            }

            try
            {
                var session = await _db.VerificationSessions
                    .FirstOrDefaultAsync(s => s.Jti == jti);

                if (session == null)
                {
                    await NotifyVerificationStatus(jti, false, "Verification session not found.");
                    return NotFound(new VerifyResponse { Success = false, Message = "Verification session not found." });
                }

                if (session.Status != "pending" || DateTime.UtcNow > session.ExpiresAt)
                {
                    await NotifyVerificationStatus(jti, false, "Invalid or expired verification session.");
                    return BadRequest(new VerifyResponse { Success = false, Message = "Invalid or expired verification session." });
                }

                try
                {
                    var verifyResult = await _userFactorApi.VerifyFactorAsync(
                        session.UserId,
                        session.FactorId,
                        null, null, null, null, null,
                        new UserFactorVerifyRequest { PassCode = request.PassCode }
                    );

                    session.Status = verifyResult.FactorResult == UserFactorVerifyResult.SUCCESS ? "succeeded" : "failed";
                    await _db.SaveChangesAsync();

                    var success = verifyResult.FactorResult == UserFactorVerifyResult.SUCCESS;
                    var message = success ? "YubiKey verification successful!" : $"YubiKey verification failed: {verifyResult.FactorResult}";
                    
                    await NotifyVerificationStatus(jti, success, message);
                    
                    return Ok(new VerifyResponse { 
                        Success = success,
                        Message = message
                    });
                }
                catch (ApiException ex)
                {
                    session.Status = "failed";
                    await _db.SaveChangesAsync();
                    await NotifyVerificationStatus(jti, false, $"Okta API Error: {ex.Message}");
                    return StatusCode(ex.ErrorCode, new VerifyResponse { Success = false, Message = $"Okta API Error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                await NotifyVerificationStatus(jti, false, "An internal server error occurred.");
                return StatusCode(500, new VerifyResponse { Success = false, Message = "An internal server error occurred." });
            }
        }

        [HttpGet("verify/status/{jti}")]
        public async Task<IActionResult> GetVerificationStatus(string jti)
        {
            var session = await _db.VerificationSessions.FirstOrDefaultAsync(s => s.Jti == jti);
            if (session == null)
            {
                return NotFound(new {
                    isValid = false,
                    expiresAt = (string?)null,
                    email = (string?)null,
                    message = "Verification session not found."
                });
            }

            string? email = null;
            try
            {
                var user = await _userApi.GetUserAsync(session.UserId);
                email = user.Profile?.Email;
            }
            catch
            {
                // If user not found or error, email remains null
            }

            var now = DateTime.UtcNow;
            var isExpired = now > session.ExpiresAt;
            var isValid = session.Status == "pending" && !isExpired;
            string message = isValid
                ? "Session is valid."
                : (isExpired ? "Session has expired." : $"Session status: {session.Status}");

            return Ok(new {
                isValid,
                expiresAt = session.ExpiresAt.ToString("o"),
                email,
                message
            });
        }

        private async Task NotifyVerificationStatus(string jti, bool success, string message)
        {
            await _hubContext.Clients.Group(jti).SendAsync("VerificationStatus", new
            {
                Success = success,
                Message = message,
                Timestamp = DateTimeOffset.UtcNow
            });
        }
    }
}