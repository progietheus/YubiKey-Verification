using System;
using System.ComponentModel.DataAnnotations;

namespace Yubikey_Verification.Models
{
    public class VerifyRequest
    {
        [Required]
        public string PassCode { get; set; } = string.Empty;
    }
}