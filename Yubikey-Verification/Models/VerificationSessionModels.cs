using System.ComponentModel.DataAnnotations;

namespace Yubikey_Verification.Models;

public class CreateVerificationSessionRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string FactorId { get; set; } = string.Empty;
}

public class VerificationSessionResponse
{
    public string Token { get; set; } = string.Empty;
    public string VerifyUrl { get; set; } = string.Empty;
    public string Jti { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
}