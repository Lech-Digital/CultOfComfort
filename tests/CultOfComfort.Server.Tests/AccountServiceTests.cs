using CultOfComfort.Server.Application;
using CultOfComfort.Simulation.Base;
using NSubstitute;

namespace CultOfComfort.Server.Tests;

public class AccountServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGoogleTokenVerifier _google = Substitute.For<IGoogleTokenVerifier>();
    private readonly Guid _player = Guid.NewGuid();

    private AccountService Service(params string[] googleClientIds) =>
        new(_players, _villages, _google, new AccountOptions { GoogleClientIds = googleClientIds.ToList() }, BuildingTable.Default, VillageMap.Demo, new FixedClock(Now));

    [Fact]
    public async Task IssueRecoveryCodeAsync_Player_StoresOnlyTheHashOfAReadableCode()
    {
        // Act
        var code = await Service().IssueRecoveryCodeAsync(_player, CancellationToken.None);

        // Assert
        code.Should().MatchRegex("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", "no 0/O or 1/I to misread");
        await _players.Received(1).SetRecoveryCodeHashAsync(_player, Arg.Is<string>(h => h.Length == 64 && !h.Contains(code)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverAsync_KnownCodeTypedSloppily_GivesThatVillageANewToken()
    {
        // Arrange
        string? stored = null;
        await _players.SetRecoveryCodeHashAsync(_player, Arg.Do<string>(h => stored = h), Arg.Any<CancellationToken>());
        var code = await Service().IssueRecoveryCodeAsync(_player, CancellationToken.None);
        _players.FindByRecoveryCodeHashAsync(Arg.Is<string>(h => h == stored), Arg.Any<CancellationToken>()).Returns(_player);

        // Act
        var account = await Service().RecoverAsync(" " + code.ToLowerInvariant().Replace("-", " ") + " ", CancellationToken.None);

        // Assert
        account.Should().NotBeNull();
        account!.PlayerId.Should().Be(_player);
        await _players.Received(1).SetTokenHashAsync(_player, Arg.Is<string>(h => h.Length == 64 && h != account.Token), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverAsync_UnknownCode_Null()
    {
        // Act
        var account = await Service().RecoverAsync("ABCD-EFGH-JKLM", CancellationToken.None);

        // Assert
        account.Should().BeNull();
        await _players.DidNotReceiveWithAnyArgs().SetTokenHashAsync(default, default!, default);
    }

    [Fact]
    public async Task RecoverAsync_WrongLength_NeverLooksItUp()
    {
        // Act
        var account = await Service().RecoverAsync("ABC", CancellationToken.None);

        // Assert
        account.Should().BeNull();
        await _players.DidNotReceiveWithAnyArgs().FindByRecoveryCodeHashAsync(default!, default);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_NotConfigured_Null()
    {
        // Act
        var account = await Service().SignInWithGoogleAsync("token", _player, CancellationToken.None);

        // Assert
        account.Should().BeNull();
        await _google.DidNotReceiveWithAnyArgs().SubjectAsync(default!, default!, default);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_GuestSignsInFirstTime_KeepsTheGuestVillage()
    {
        // Arrange
        _google.SubjectAsync("id-token", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns("google-123");
        _players.LinkGoogleSubjectAsync(_player, "google-123", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var account = await Service("web-client").SignInWithGoogleAsync("id-token", _player, CancellationToken.None);

        // Assert
        account!.PlayerId.Should().Be(_player);
        await _villages.DidNotReceiveWithAnyArgs().AddAsync(default, default!, default);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_AccountAlreadyLinked_ReturnsThatVillage()
    {
        // Arrange — a new phone (a fresh guest) signs into an account that already has a village.
        var home = Guid.NewGuid();
        _google.SubjectAsync("id-token", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns("google-123");
        _players.FindByGoogleSubjectAsync("google-123", Arg.Any<CancellationToken>()).Returns(home);

        // Act
        var account = await Service("web-client").SignInWithGoogleAsync("id-token", _player, CancellationToken.None);

        // Assert
        account!.PlayerId.Should().Be(home);
        await _players.DidNotReceiveWithAnyArgs().LinkGoogleSubjectAsync(default, default!, default);
    }

    [Fact]
    public async Task SignInWithGoogleAsync_InvalidToken_Null()
    {
        // Act
        var account = await Service("web-client").SignInWithGoogleAsync("forged", _player, CancellationToken.None);

        // Assert
        account.Should().BeNull();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
