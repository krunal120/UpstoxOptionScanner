using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NiftyQuant.Domain.Entities;
using NiftyQuant.Domain.Enums;
using NiftyQuant.Infrastructure.Persistence;
using Xunit;

namespace NiftyQuant.IntegrationTests;

public class DatabaseTests
{
    private const string ConnectionString = "Server=.\\SQLSERVER2022_2;Database=NiftyQuantDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

    [Fact]
    public async Task CanConnectToSqlServer_AndQueryTables()
    {
        var options = new DbContextOptionsBuilder<NiftyQuantDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        using var dbContext = new NiftyQuantDbContext(options);

        // Can connect
        var canConnect = await dbContext.Database.CanConnectAsync();
        canConnect.Should().BeTrue();

        // Can insert and retrieve paper account
        var account = await dbContext.PaperAccounts.FirstOrDefaultAsync();
        if (account == null)
        {
            account = new PaperAccount
            {
                AccountName = "Initial Integration Test Account",
                InitialCapital = 50000m,
                AvailableBalance = 50000m
            };
            dbContext.PaperAccounts.Add(account);
            await dbContext.SaveChangesAsync();
        }

        account.InitialCapital.Should().Be(50000m);
    }
}
