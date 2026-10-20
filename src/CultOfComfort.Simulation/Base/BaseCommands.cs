using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>An intent sent to whoever holds authority over the village (the device today, the server later).</summary>
public abstract record BaseCommand(Guid CommandId);

/// <param name="Position">The anchor: the footprint's lowest x and y.</param>
/// <param name="Rotated">Turned a quarter: width and depth swap.</param>
/// <param name="Flipped">Turned a half more: the same tiles, facing the other way.</param>
public sealed record PlaceBuilding(Guid CommandId, BuildingType Type, GridPosition Position, bool Rotated = false, bool Flipped = false) : BaseCommand(CommandId);

/// <summary>Paints roads, fences or walls over many tiles at once: tiles that can't take it are skipped.</summary>
public sealed record PaintTiles(Guid CommandId, BuildingType Type, IReadOnlyList<GridPosition> Tiles) : BaseCommand(CommandId);

/// <summary>Picks a building up and sets it down elsewhere: free, keeping its level, output, construction and damage.</summary>
public sealed record MoveBuilding(Guid CommandId, int BuildingId, GridPosition Position, bool Rotated, bool Flipped = false) : BaseCommand(CommandId);

/// <summary>Repairs every wrecked building at once, or none if the village can't pay for all of them.</summary>
public sealed record RepairAll(Guid CommandId) : BaseCommand(CommandId);

public sealed record UpgradeBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

public sealed record UpgradeHearth(Guid CommandId) : BaseCommand(CommandId);

public sealed record DemolishBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <param name="BuildingId">Null collects from every producer.</param>
public sealed record Collect(Guid CommandId, int? BuildingId) : BaseCommand(CommandId);

/// <summary>Development and tests only (a server refuses it): records a Night's outcome directly. Real Nights use <see cref="ResolveNight"/>.</summary>
/// <param name="WreckedBuildingIds">Buildings smashed to nothing in the Night (structure ids are building ids).</param>
public sealed record RecordNightResult(Guid CommandId, int Night, int Stars, IReadOnlyList<int>? WreckedBuildingIds = null) : BaseCommand(CommandId);

public sealed record RepairBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <summary>Starts a Night now: the raid that is coming, or an early one sized by the current Tension.</summary>
public sealed record BeginNight(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Ends the Night begun with <see cref="BeginNight"/>: only the input log is sent; the village replays it.</summary>
public sealed record ResolveNight(Guid CommandId, int Night, IReadOnlyList<CultOfComfort.Simulation.Combat.PlayerInput> Inputs) : BaseCommand(CommandId);

/// <summary>The player has seen the latest Night report.</summary>
public sealed record AcknowledgeReport(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Takes the reward for the current errand (only once it is done) and moves on to the next one.</summary>
public sealed record ClaimErrand(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Cuts a wild tree (Timber) or clears a loose boulder (costs Timber, gives Scrap). Walls are bedrock.</summary>
/// <summary>Claims one of today's festival rewards (spec 2026-10-20 the festival).</summary>
public sealed record ClaimFestival(Guid CommandId, int Tier) : BaseCommand(CommandId);

/// <summary>Sets out on the next stage of the Pilgrims' Way (spec 2026-10-20 the Pilgrims' Way): the outcome carries its plan.</summary>
public sealed record BeginTrial(Guid CommandId) : BaseCommand(CommandId);

/// <summary>A stage of the Pilgrims' Way, fought: replayed from the inputs like a Night.</summary>
public sealed record ResolveTrial(Guid CommandId, int Stage, IReadOnlyList<CultOfComfort.Simulation.Combat.PlayerInput> Inputs) : BaseCommand(CommandId);

/// <summary>Empties the Pilgrims' alms box into the stores.</summary>
public sealed record ClaimAlms(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Puts a notable to work at a building (spec 2026-10-20 notables); a negative id sends them home.</summary>
public sealed record AssignNotable(Guid CommandId, string NotableId, int BuildingId) : BaseCommand(CommandId);

/// <summary>Chooses the Prophet's perk at the level he's reached (spec 2026-10-20 the Prophet grows).</summary>
public sealed record ChooseProphetPerk(Guid CommandId, string PerkId) : BaseCommand(CommandId);

/// <summary>Answers the visitor at the Hearth (spec 2026-10-20 visitors): give what they ask, or send them on.</summary>
public sealed record AnswerVisitor(Guid CommandId, bool Accept) : BaseCommand(CommandId);

/// <summary>Packs one tower up: off the ground, kept with its level, set down again for free from the build drawer.</summary>
public sealed record PackUpTower(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <summary>Packs every tower up at once, to lay the defence out afresh for the scout's road.</summary>
public sealed record PackUpTowers(Guid CommandId) : BaseCommand(CommandId);

/// <summary>A Guardhouse's duty: its wardens man the nearest towers (false) or go out and fight (true).</summary>
public sealed record SetDuty(Guid CommandId, int BuildingId, bool Patrol) : BaseCommand(CommandId);

/// <summary>Lays the next surface on the raiders' road (gravel, then cobbles): the village looks after itself. The raiders walk it the same.</summary>
public sealed record PaveRaidersRoad(Guid CommandId) : BaseCommand(CommandId);

public sealed record ClearTerrain(Guid CommandId, GridPosition Tile) : BaseCommand(CommandId);

/// <summary>Development only: adds Tension at once. A server refuses this.</summary>
public sealed record DebugRaiseTension(Guid CommandId, int Amount) : BaseCommand(CommandId);

/// <summary>Development only: ends a raid's warning now. A server refuses this.</summary>
public sealed record DebugSkipWarning(Guid CommandId) : BaseCommand(CommandId);

/// <summary>
/// Sends a missionary to preach at a node of the Domain for 1, 2, 4 or 8 hours (spec 2026-10-14 world map):
/// <paramref name="PreacherId"/> of the Mission House, or 0 for the first free one.
/// </summary>
public sealed record SendMissionary(Guid CommandId, int NodeId, int Hours, int PreacherId = 0) : BaseCommand(CommandId);

/// <summary>Sets how many villagers should work a building (0 to its slots; spec 2026-10-14 cultist work).</summary>
public sealed record SetCrew(Guid CommandId, int BuildingId, int Crew) : BaseCommand(CommandId);

/// <summary>Sends a missionary to a place on the shared world, <paramref name="Distance"/> hexes away (the server checks the distance against the world).</summary>
public sealed record SendWorldMissionary(Guid CommandId, int NodeId, int Hours, int Distance, int PreacherId = 0) : BaseCommand(CommandId);

/// <summary>Server only: the village joins a shared world at (Q, R). The Marchlands are left behind; the purse stays.</summary>
public sealed record JoinWorld(Guid CommandId, int Q, int R) : BaseCommand(CommandId);

/// <summary>Server only: the world's verdict on this cult: its new influence-driven Tension rate, plus a one-off spike, and the places it is
/// Patron of (null: unchanged).</summary>
public sealed record WorldUpdate(Guid CommandId, int TensionPerHourMilli, int TensionSpike, int? Patronages = null) : BaseCommand(CommandId);

/// <summary>Server only: offerings from the world's purse arrive; the village keeps what its stores hold (the outcome says how much).</summary>
public sealed record ReceiveOfferings(Guid CommandId, ResourceBag Offerings) : BaseCommand(CommandId);

/// <summary>Takes the offerings the faithful of the Marchlands have sent.</summary>
public sealed record CollectOfferings(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Pays the Authority to calm down: Tension drops by the tithe's worth (and an announced raid may be called off).</summary>
public sealed record PayTithe(Guid CommandId, ResourceBag Amount) : BaseCommand(CommandId);

/// <summary>Claims one of today's errands (its slot, 0–2) once it's done (spec 2026-10-15 daily errands).</summary>
public sealed record ClaimDaily(Guid CommandId, int Slot) : BaseCommand(CommandId);

/// <summary>Opens the Thermos: all three of today's errands claimed. Towels, Devotion, and the streak goes on.</summary>
public sealed record ClaimThermos(Guid CommandId) : BaseCommand(CommandId);

/// <summary>
/// Spends Towels: hurry a construction (<paramref name="Slot"/>: the building's id + 1; 0: whichever finishes first), or
/// swap one of today's errands (<paramref name="Slot"/>: its place on the list).
/// </summary>
public sealed record SpendTowels(Guid CommandId, TowelUse Use, int Slot = 0) : BaseCommand(CommandId);

/// <summary>The player's clock (minutes from UTC): their day starts at 04:00 on it.</summary>
public sealed record SetDayClock(Guid CommandId, int OffsetMinutes) : BaseCommand(CommandId);

/// <summary>Delivers what the lord demanded (spec 2026-10-15): his temper drops twice a tithe's worth.</summary>
public sealed record MeetDemand(Guid CommandId, int DemandId) : BaseCommand(CommandId);

/// <summary>Ends a road tale a preacher brought home (spec 2026-10-16 mission board): <paramref name="Choice"/> 0 or 1.</summary>
public sealed record ChooseTale(Guid CommandId, int TaleId, int Choice) : BaseCommand(CommandId);

/// <summary>Tries something on the lord over his open demand instead of paying (spec 2026-10-16 the lord's hall).</summary>
public sealed record PleadWithLord(Guid CommandId, int DemandId, Plea Plea) : BaseCommand(CommandId);

/// <summary>Starts a study at the Hearth (one at a time; spec 2026-10-14, research).</summary>
public sealed record StartResearch(Guid CommandId, string TechId) : BaseCommand(CommandId);

/// <summary>Stops the study under way; what it cost comes back (owner 2026-10-16: "no way to cancel the build or research").</summary>
public sealed record CancelResearch(Guid CommandId) : BaseCommand(CommandId);

/// <summary>
/// Stops a construction or upgrade: a new building is taken down, an upgrade stays at its level. What it cost comes
/// back (as far as the stores have room); the time spent is the price.
/// </summary>
public sealed record CancelConstruction(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <summary>The Trolley Market: buys <paramref name="Amount"/> of <paramref name="Get"/> with <paramref name="Give"/>, at a bad rate.</summary>
public sealed record Trade(Guid CommandId, ResourceType Give, ResourceType Get, int Amount) : BaseCommand(CommandId);

/// <summary>Development only: fills every store to its cap (for testing the later Hearth levels). A server refuses this.</summary>
public sealed record DebugFillStores(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Development only: ends the after-raid shield now (testing Nights back to back). A server refuses this.</summary>
public sealed record DebugEndShield(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Development only: completes the running construction. A server refuses (or charges for) this.</summary>
public sealed record FinishConstructionNow(Guid CommandId) : BaseCommand(CommandId);

public enum CommandStatus
{
    Accepted,
    Duplicate,
    Rejected,
}

/// <param name="Collected">Resources gained by a Collect or night salvage.</param>
public sealed record CommandOutcome(CommandStatus Status, long Version, string? Reason = null, ResourceBag Collected = default, NightPlan? Plan = null, NightReport? Report = null)
{
    internal static CommandOutcome Accept(ResourceBag collected = default) => new(CommandStatus.Accepted, 0, null, collected);

    internal static CommandOutcome Reject(string reason) => new(CommandStatus.Rejected, 0, reason);
}
