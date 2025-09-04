// Yubikey-Verification/Models/VerificationDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Yubikey_Verification.Models
{
    public class VerificationDbContextFactory : IDesignTimeDbContextFactory<VerificationDbContext>
    {
        public VerificationDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<VerificationDbContext>();
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection"));

            return new VerificationDbContext(optionsBuilder.Options);
        }
    }
}