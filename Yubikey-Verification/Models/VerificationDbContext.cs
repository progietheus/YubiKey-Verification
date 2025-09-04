using Microsoft.EntityFrameworkCore;

namespace Yubikey_Verification.Models
{
    public class VerificationDbContext : DbContext
    {
        public VerificationDbContext(DbContextOptions<VerificationDbContext> options) : base(options) { }
        public DbSet<VerificationSession> VerificationSessions { get; set; }
    }
}
