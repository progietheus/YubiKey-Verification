using System;
using System.ComponentModel.DataAnnotations;

namespace Yubikey_Verification.Models
{
    public class VerifyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}