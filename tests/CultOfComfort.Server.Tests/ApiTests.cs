using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CultOfComfort.Contracts;
using CultOfComfort.Server.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CultOfComfort.Server.Tests;

/// <summary>The real host against a temporary SQLite file: guest → read → sync, the way a phone will.</summary>
public sealed class ApiTests : IClassFixture<ApiTests.Host>
{
    private readonly Host _host;

    public ApiTests(Host host) => _host = host;

    public sealed class Host : WebApplicationFactory<Program>
    {
        private readonly string _db = Path.Combine(Path.GetTempPath(), $"coc-api-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production"); // as deployed: development commands are refused
            builder.UseSetting("ConnectionStrings:Db", $"Data Source={_db}");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_db);
        }
    }

    private async Task<HttpClient> GuestAsync()
    {
        var client = _host.CreateClient();
        var guest = await (await client.PostAsync("/api/auth/guest", null)).Content.ReadFromJsonAsync<GuestDto>(VillageJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest!.token);
        return client;
    }

    private static Task<HttpResponseMessage> SyncAsync(HttpClient client, long version, params CommandDto[] commands) =>
        client.PostAsJsonAsync("/api/base/sync", new SyncRequestDto { expectedVersion = version, commands = commands.ToList() }, VillageJson.Options);

    private static CommandDto Command(string type, Action<CommandDto>? fill = null)
    {
        var dto = new CommandDto { id = Guid.NewGuid().ToString(), type = type };
        fill?.Invoke(dto);
        return dto;
    }

    [Fact]
    public async Task Health_Always_IsOk()
    {
        // Act
        var response = await _host.CreateClient().GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBase_WithoutAToken_IsUnauthorized()
    {
        // Act
        var response = await _host.CreateClient().GetAsync("/api/base");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GuestThenSync_PlaceAWoodcutter_TheServerKeepsIt()
    {
        // Arrange
        var client = await GuestAsync();
        var start = await client.GetFromJsonAsync<VillageResponseDto>("/api/base", VillageJson.Options);

        // Act
        var response = await SyncAsync(client, start!.version, Command("PlaceBuilding", c => (c.building, c.x, c.y) = ("Woodcutter", 3, 9)));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var synced = await response.Content.ReadFromJsonAsync<VillageResponseDto>(VillageJson.Options);
        synced!.outcomes.Should().ContainSingle().Which.status.Should().Be("Accepted");
        var again = await client.GetFromJsonAsync<VillageResponseDto>("/api/base", VillageJson.Options);
        again!.version.Should().Be(start.version + 1);
        again.village.timber.Should().Be(450);
        again.village.buildings.Should().Contain(b => b.type == "Woodcutter");
    }

    [Fact]
    public async Task Sync_StaleVersion_Is409WithTheCurrentVillage()
    {
        // Arrange
        var client = await GuestAsync();
        await SyncAsync(client, 0, Command("PlaceBuilding", c => (c.building, c.x, c.y) = ("Woodcutter", 3, 9)));

        // Act — a second device still thinks the village is at version 0.
        var response = await SyncAsync(client, 0, Command("UpgradeHearth"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<VillageResponseDto>(VillageJson.Options))!.version.Should().Be(1);
    }

    [Fact]
    public async Task Sync_DevelopmentCommandInProduction_IsRejected()
    {
        // Arrange
        var client = await GuestAsync();

        // Act
        var response = await SyncAsync(client, 0, Command("DebugRaiseTension", c => c.amount = 100));

        // Assert
        var body = await response.Content.ReadFromJsonAsync<VillageResponseDto>(VillageJson.Options);
        body!.outcomes.Single().status.Should().Be("Rejected");
        body.village.tensionMilli.Should().Be(0);
    }

    [Fact]
    public async Task Sync_UnknownCommand_IsABadRequest()
    {
        // Arrange
        var client = await GuestAsync();

        // Act
        var response = await SyncAsync(client, 0, Command("SummonDragon"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sync_ANightPlayedOnTheServer_IsReplayedFromItsInputLog()
    {
        // Arrange — lamps lit. (The "lamps can wait" errand lock is the client's; the village rule allows an early Night.)
        var client = await GuestAsync();
        var begun = await (await SyncAsync(client, 0, Command("BeginNight"))).Content.ReadFromJsonAsync<VillageResponseDto>(VillageJson.Options);
        begun!.outcomes.Single().hasPlan.Should().BeTrue();

        // Act
        var response = await SyncAsync(client, begun.version, Command("ResolveNight", c => (c.night, c.inputs) = (begun.outcomes[0].plan.night, new List<InputDto>())));

        // Assert
        var resolved = await response.Content.ReadFromJsonAsync<VillageResponseDto>(VillageJson.Options);
        var outcome = resolved!.outcomes.Single();
        outcome.status.Should().Be("Accepted");
        outcome.hasReport.Should().BeTrue();
        outcome.report.hash.Should().NotBeNullOrEmpty();
        resolved.village.lastNight.Should().Be(1);
        resolved.village.shieldEndsAtTicks.Should().BeGreaterThan(0);
    }
}
