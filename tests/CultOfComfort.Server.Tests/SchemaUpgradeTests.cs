using CultOfComfort.Server.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CultOfComfort.Server.Tests;

public sealed class SchemaUpgradeTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"coc-schema-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_db);
    }

    [Fact]
    public async Task EnsureVillageStorageAsync_DatabaseFromTheFirstDeploy_GainsTheAccountColumnsAndKeepsItsRows()
    {
        // Arrange — the players table as the first deploy made it, with one player in it.
        await using (var connection = new SqliteConnection($"Data Source={_db}"))
        {
            await connection.OpenAsync();
            var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE players ("Id" TEXT NOT NULL CONSTRAINT "PK_players" PRIMARY KEY, "TokenHash" TEXT NOT NULL, "CreatedAtUtc" INTEGER NOT NULL);
                CREATE UNIQUE INDEX "IX_players_TokenHash" ON players ("TokenHash");
                CREATE TABLE villages ("PlayerId" TEXT NOT NULL CONSTRAINT "PK_villages" PRIMARY KEY, "Version" INTEGER NOT NULL, "Json" TEXT NOT NULL, "UpdatedAtUtc" INTEGER NOT NULL);
                INSERT INTO players VALUES ('11111111-1111-1111-1111-111111111111', 'hash', 0);
                """;
            await create.ExecuteNonQueryAsync();
        }

        var services = new ServiceCollection().AddSingleton(TimeProvider.System).AddVillageStorage($"Data Source={_db}").BuildServiceProvider();

        // Act — twice: the upgrade is idempotent.
        await services.EnsureVillageStorageAsync(CancellationToken.None);
        await services.EnsureVillageStorageAsync(CancellationToken.None);

        // Assert
        await using var check = new SqliteConnection($"Data Source={_db}");
        await check.OpenAsync();
        var columns = new List<string>();
        var info = check.CreateCommand();
        info.CommandText = "PRAGMA table_info(players);";
        await using (var reader = await info.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }

        columns.Should().Contain(new[] { "RecoveryCodeHash", "GoogleSubject" });
        var count = check.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM players;";
        ((long)(await count.ExecuteScalarAsync())!).Should().Be(1);
    }
}
