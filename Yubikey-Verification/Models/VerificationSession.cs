using System;
using System.ComponentModel.DataAnnotations;

namespace Yubikey_Verification.Models
{
    public class VerificationSession
    {
        [Key]
        public string Jti { get; set; } = string.Empty;
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public string FactorId { get; set; } = string.Empty;
        [Required]
        public string Status { get; set; } = "pending";
        [Required]
        public DateTime ExpiresAt { get; set; }
    }
}
