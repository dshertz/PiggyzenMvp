using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PiggyzenMvp.API.Data;

/// <summary>
/// Provides a lightweight factory so dotnet-ef can construct the DbContext without bootstrapping the full host.
/// </summary>
public class DesignTimePiggyzenMvpContextFactory : IDesignTimeDbContextFactory<PiggyzenMvpContext>
{
    public PiggyzenMvpContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PiggyzenMvpContext>();
        optionsBuilder.UseSqlite("Data Source=piggyzen.db");

        return new PiggyzenMvpContext(optionsBuilder.Options);
    }
}
