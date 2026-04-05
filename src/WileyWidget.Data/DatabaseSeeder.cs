using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public DatabaseSeeder(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // The Amplify database is managed externally, so migrations are opt-in.
            if (!_configuration.GetValue<bool>("Database:ApplyMigrations"))
            {
                return;
            }

            // EF Core HasData still applies when migrations are explicitly enabled.
            await _context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
