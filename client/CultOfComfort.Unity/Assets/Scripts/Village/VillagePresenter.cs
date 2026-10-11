using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Client
{
    public sealed class BuildCardModel
    {
        public BuildingType Type; public string Name; public string Icon; public string Cost; public string Time;
        public bool Locked; public string LockText; public bool Affordable;
        public string Size = string.Empty; // "2×4"; empty for a 1×1
    }

    public sealed class BuildingCardModel
    {
        public int BuildingId; public BuildingType Type; public string Title; public string Output; public string Ready;
        public string UpgradeLabel; public bool CanUpgrade; public bool CanDemolish; public bool IsUpgrading; public string Remaining; public bool IsHearth;
        public bool IsWrecked; public string RepairLabel; public bool CanRepair;
        public string DemolishLabel = "Demolish"; public bool IsTerrain;
    }

    /// <summary>Turns a village snapshot into what the HUD shows. Pure: no Unity, easy to test.</summary>
    public static class VillagePresenter
    {
        public static string IconFor(BuildingType type) => type switch
        {
            BuildingType.Hearth => "b_hearth",
            BuildingType.Woodcutter => "b_woodcutter",
            BuildingType.PeatCut => "b_peat_cut",
            BuildingType.StorageHut => "b_storage_hut",
            BuildingType.CultistHut => "b_cultist_hut",
            BuildingType.Cistern => "b_cistern",
            BuildingType.SlurryChute => "b_slurry_chute",
            BuildingType.WattleFence => "b_wattle_fence",
            BuildingType.WoodenWall => "b_wooden_wall",
            BuildingType.Gate => "b_gate",
            BuildingType.DirtTrack => "b_dirt_track",
            BuildingType.PlankWalk => "b_plank_walk",
            BuildingType.CobbledRoad => "b_cobbled_road",
            BuildingType.Cottage => "b_cottage",
            BuildingType.Stone => "b_stone",
            BuildingType.Tree => "b_tree",
            _ => "b_sapling_flinger",
        };

        public static IReadOnlyList<BuildCardModel> BuildCards(BaseSnapshot snapshot, BuildingTable table) =>
            table.Specs.Values.Where(s => s.Type != BuildingType.Hearth).Select(spec =>
            {
                var cost = spec.Levels[0].Cost;
                var count = snapshot.Buildings.Count(b => b.Type == spec.Type);
                var locked = snapshot.HearthLevel < spec.UnlockHearth || count >= spec.MaxCount(snapshot.HearthLevel);
                return new BuildCardModel
                {
                    Type = spec.Type, Name = spec.Name, Icon = IconFor(spec.Type), Cost = cost.ToString(),
                    Time = spec.Levels[0].BuildSeconds == 0 ? "instant" : Duration(spec.Levels[0].BuildSeconds),
                    Locked = locked,
                    LockText = snapshot.HearthLevel < spec.UnlockHearth ? $"Hearth {spec.UnlockHearth}" : locked ? "Max built" : string.Empty,
                    Affordable = snapshot.Stock.Covers(cost),
                    Size = spec.Width == 1 && spec.Depth == 1 ? string.Empty : $"{spec.Width}×{spec.Depth}",
                };
            }).ToList();

        public static BuildingCardModel Card(BaseSnapshot snapshot, BuildingTable table, int buildingId)
        {
            var b = snapshot.Building(buildingId);
            var spec = table.Spec(b.Type);
            var maxed = b.Level >= spec.MaxLevel || spec.IsTower;
            var next = maxed ? null : spec.Levels[b.Level];
            var repair = table.RepairCost(b.Type, b.Level);
            var upgradeLabel = b.Wrecked ? "Wrecked — repair first" : b.IsUpgrading ? (b.Level == 0 ? "Being built" : "Upgrading") : maxed ? "Max level" : $"Upgrade · {next.Cost} · {Duration(next.BuildSeconds)}";
            if (b.Type == BuildingType.Tree)
            {
                return new BuildingCardModel
                {
                    BuildingId = b.Id, Type = b.Type, Title = b.Level == 0 ? "Tree · sapling" : "Tree · grown",
                    Output = b.Level == 0 ? $"Growing: {Duration((long)(b.UpgradeEndsAtUtc.Value - snapshot.AsOfUtc).TotalSeconds)} left" : "Ready to cut.",
                    UpgradeLabel = "Trees grow on their own", CanUpgrade = false, CanDemolish = true,
                    DemolishLabel = b.Level == 0 ? "Pull it up (nothing to show yet)" : $"Cut it down · +{table.WildTreeTimber} Timber",
                };
            }

            return new BuildingCardModel
            {
                DemolishLabel = b.Type == BuildingType.Stone ? $"Take it away · +{table.Spec(b.Type).Levels[0].Cost}" : "Demolish",
                BuildingId = b.Id, Type = b.Type, IsHearth = b.Type == BuildingType.Hearth,
                Title = b.Level == 0 ? $"{spec.Name} · being built" : $"{spec.Name} · level {b.Level}",
                IsWrecked = b.Wrecked,
                RepairLabel = b.Wrecked ? $"Repair · {repair}" : string.Empty,
                CanRepair = b.Wrecked && snapshot.Stock.Covers(repair),
                Output = b.Wrecked ? (spec.IsTower ? "Wrecked — won't fire until repaired" : "Wrecked — produces nothing")
                    : !b.Connected ? "Not connected — build a road to it"
                    : b.Level > 0 && b.CrewNeeded > 0 && b.Crew == 0 ? $"Needs {b.CrewNeeded} villager{(b.CrewNeeded == 1 ? string.Empty : "s")} — build a Cottage"
                    : b.Level > 0 && spec.Produces != null && snapshot.Cold ? "Cold — no Peat, nobody works"
                    : b.Level == 0 ? string.Empty : spec.Produces != null ? $"+{b.PerHour} {spec.Produces}/h" : Effect(spec, b.Level),
                Ready = spec.Produces != null && b.Level > 0 ? $"{b.Ready}/{b.Capacity} ready" + (b.CrewNeeded > 0 ? $" · Crew {b.Crew}/{b.CrewNeeded}" : string.Empty) : string.Empty,
                UpgradeLabel = upgradeLabel,
                CanUpgrade = !b.Wrecked && !maxed && !b.IsUpgrading && snapshot.ConstructionEndsAtUtc == null && snapshot.Stock.Covers(next.Cost)
                             && (b.Type == BuildingType.Hearth || b.Level + 1 <= snapshot.HearthLevel),
                CanDemolish = b.Type != BuildingType.Hearth && !b.IsUpgrading,
                IsUpgrading = b.IsUpgrading,
                Remaining = b.IsUpgrading ? Duration((long)(b.UpgradeEndsAtUtc.Value - snapshot.AsOfUtc).TotalSeconds) + " left" : string.Empty,
            };
        }

        /// <summary>The marker-pen note on the Tension strip.</summary>
        public static string TensionNote(BaseSnapshot s)
        {
            if (s.RaidWarningEndsAtUtc != null)
            {
                var left = Duration((long)(s.RaidWarningEndsAtUtc.Value - s.AsOfUtc).TotalSeconds);
                return s.TensionState == TensionState.Punitive ? $"Punitive raid · {left}" : $"Wulfric's men are coming · {left}";
            }

            if (s.ShieldEndsAtUtc != null)
            {
                return $"Shielded · {Duration((long)(s.ShieldEndsAtUtc.Value - s.AsOfUtc).TotalSeconds)}";
            }

            return s.TensionState == TensionState.Calm ? "Calm" : "Uneasy — Wulfric is muttering";
        }

        /// <summary>The big Day button: face the raid that is coming, or invite a smaller one early.</summary>
        public static string StartLabel(BaseSnapshot s) => s.RaidWarningEndsAtUtc != null
            ? $"Face the raid · {Duration((long)(s.RaidWarningEndsAtUtc.Value - s.AsOfUtc).TotalSeconds)} left"
            : s.ShieldEndsAtUtc != null
                ? $"Shielded · {Duration((long)(s.ShieldEndsAtUtc.Value - s.AsOfUtc).TotalSeconds)}"
                : "Light the Lamps (early)";

        /// <summary>The card for a wild tree or loose boulder on the tile, or null (nothing there, or bedrock).</summary>
        public static BuildingCardModel TerrainCard(BaseSnapshot s, VillageMap map, BuildingTable table, CultOfComfort.Simulation.Grid.GridPosition tile)
        {
            var cleared = s.ClearedTerrain ?? Array.Empty<CultOfComfort.Simulation.Grid.GridPosition>();
            if (!map.IsOpen(tile, s.HearthLevel) || s.BuildingAt(tile) != null || cleared.Contains(tile))
            {
                return null;
            }

            if (map.IsBoulder(tile))
            {
                return new BuildingCardModel
                {
                    IsTerrain = true, Title = "Boulder", Output = "Loose stone. Clearing it is free and gives the stone back as Scrap.",
                    UpgradeLabel = $"Clear it · +{table.BoulderScrap} Scrap", CanUpgrade = true,
                };
            }

            if (map.WildTrees.Contains(tile))
            {
                return new BuildingCardModel
                {
                    IsTerrain = true, Title = "Wild tree", Output = $"Grows back {table.WildTreeRegrowHours}h after it's cut, unless you build there.",
                    UpgradeLabel = $"Cut it down · +{table.WildTreeTimber} Timber", CanUpgrade = true,
                };
            }

            return null;
        }

        public static FrameState Frame(BaseSnapshot s) => new FrameState(
            new Reading(s.Stock.Timber, s.Caps.Timber, false),
            new Reading(s.Stock.Scrap, s.Caps.Scrap, false),
            new Reading(s.Stock.Peat, s.Caps.Peat, false),
            new Reading(s.Stock.Devotion, s.Caps.Devotion, false),
            new Reading(50, 0, true),
            new Reading(42, 0, true),
            s.HearthLevel, s.Tension / 150f, TensionNote(s), false, new Reading(s.Population, s.Beds, false), s.Cold);

        /// <summary>
        /// Whether <paramref name="candidate"/> should replace <paramref name="current"/>: a later version, or the same
        /// version seen later. A slow read that started before a command must not roll the screen back.
        /// </summary>
        public static bool IsNewer(BaseSnapshot candidate, BaseSnapshot current) =>
            current == null || candidate.Version > current.Version || (candidate.Version == current.Version && candidate.AsOfUtc >= current.AsOfUtc);

        public static string Duration(long seconds)
        {
            seconds = Math.Max(0, seconds);
            if (seconds < 60) return $"{seconds}s";
            if (seconds < 3600) return $"{seconds / 60}m";
            return $"{seconds / 3600}h {seconds % 3600 / 60:00}m";
        }

        private static string Effect(BuildingSpec spec, int level)
        {
            if (level == 0) return string.Empty;
            var l = spec.Levels[level - 1];
            if (l.StorageBonus > 0) return $"+{l.StorageBonus} storage for everything";
            if (l.SluiceCharges > 0) return $"+{l.SluiceCharges} Open Sluice per Night";
            if (l.Beds > 0) return $"{l.Beds} beds";
            return spec.IsTower ? "Defends the road" : spec.Type == BuildingType.Hearth ? "The heart of the cult" : string.Empty;
        }
    }
}
