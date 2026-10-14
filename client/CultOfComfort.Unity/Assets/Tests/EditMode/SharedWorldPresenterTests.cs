using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Contracts;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class SharedWorldPresenterTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        private const long Seed = 20261014;

        private static BuildingTable Rich() => new BuildingTable
        {
            StartingStock = new ResourceBag(8000, 2000, 2000, 1000), HearthStorageCaps = new[] { 9000, 9000, 9000 }, HearthBeds = 12,
        };

        /// <summary>A Hearth 2 village on World 1 at a real free spot, and the window the server would send.</summary>
        private static (BaseSnapshot Snapshot, WorldDto World, WorldNode Near) OnTheWorld(List<WorldHoldingDto> holdings = null, List<WorldCultDto> rivals = null)
        {
            var map = WorldGenerator.Generate(Seed);
            var spot = map.PlaceVillage(new List<(int, int)>(), 1).Value;
            var state = BaseState.CreateNew(Rich(), VillageMap.Demo, T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            var now = T0.AddHours(2);
            state.Apply(new JoinWorld(Guid.NewGuid(), spot.Q, spot.R), now);
            var near = map.Near(spot.Q, spot.R, 3).Where(n => n.Type == NodeType.Hamlet || n.Type == NodeType.Village).OrderBy(n => n.DistanceTo(spot.Q, spot.R)).First();
            var cults = new List<WorldCultDto> { new WorldCultDto { id = "me", name = "Cult of Here", q = spot.Q, r = spot.R } };
            cults.AddRange(rivals ?? new List<WorldCultDto>());
            var world = new WorldDto
            {
                worldId = 1, seed = Seed, cultId = "me", q = spot.Q, r = spot.R, viewQ = spot.Q, viewR = spot.R, viewRadius = 4,
                cults = cults, holdings = holdings ?? new List<WorldHoldingDto>(),
            };
            return (state.Snapshot(now), world, near);
        }

        [Test]
        public void Build_Window_EveryPlaceInsideTheMapAndHomeAtTheCentre()
        {
            // Arrange
            var (snapshot, world, _) = OnTheWorld();

            // Act
            var model = SharedWorldPresenter.Build(snapshot, Rich(), world, snapshot.AsOfUtc);

            // Assert
            Assert.IsTrue(model.Pannable);
            Assert.IsTrue(model.Nodes.All(n => n.At.x >= 0f && n.At.x <= 1f && n.At.y >= 0f && n.At.y <= 1f));
            var home = model.Nodes.Single(n => n.Home);
            Assert.AreEqual(0.5f, home.At.x, 1e-4f);
            Assert.AreEqual(0.5f, home.At.y, 1e-4f);
            StringAssert.StartsWith("The ", model.Title, "whose lands these are");
            StringAssert.DoesNotContain("World", model.Title, "the title must fit beside the close button");
            Assert.IsTrue(model.Lord.StartsWith("Lord ") || model.Lord.StartsWith("the Bishop of "), model.Lord);
            StringAssert.Contains(model.Lord.Substring(1), model.TensionLine, "the temper bar says whose temper it is");
            Assert.IsNotEmpty(model.TensionHelp);
            Assert.GreaterOrEqual(model.Help.Count, 4, "the ? sheet explains missionaries, offerings, temper and tithes");
        }

        [Test]
        public void Build_RivalHoldsMore_TheRingShowsTheirColourAndYourShare()
        {
            // Arrange — you hold 20 of the nearest place, the Damp Brethren 45 (Patron).
            var probe = OnTheWorld();
            var holdings = new List<WorldHoldingDto>
            {
                new WorldHoldingDto { node = probe.Near.Id, cult = "me", milli = 20_000 },
                new WorldHoldingDto { node = probe.Near.Id, cult = "bot-001", milli = 45_000, patron = true },
            };
            var rival = new WorldCultDto { id = "bot-001", name = "The Damp Brethren", q = probe.World.q + 3, r = probe.World.r, colour = 4 };
            var (snapshot, world, near) = OnTheWorld(holdings, new List<WorldCultDto> { rival });

            // Act
            var model = SharedWorldPresenter.Build(snapshot, Rich(), world, snapshot.AsOfUtc);
            var card = SharedWorldPresenter.Card(snapshot, Rich(), world, near.Id);

            // Assert
            var node = model.Nodes.Single(n => n.Id == near.Id);
            Assert.AreEqual(20, node.Influence);
            Assert.AreEqual(45, node.RivalShare);
            Assert.AreEqual(4, node.RivalColour);
            Assert.IsTrue(model.Nodes.Any(n => n.Camp && n.Name == "The Damp Brethren"), "the rival's village is on the map");
            Assert.IsTrue(card.Lines.Any(l => l.Contains("The Damp Brethren 45% (Patron)") && l.Contains("the Authority 35%")));
        }

        [Test]
        public void Card_PlaceOutOfReach_NoPreaching()
        {
            // Arrange — Hearth 2 reaches 3 hexes.
            var (snapshot, world, _) = OnTheWorld();
            var far = SharedWorldPresenter.MapFor(world).Near(world.q, world.r, 7).First(n => n.DistanceTo(world.q, world.r) > 3);

            // Act
            var card = SharedWorldPresenter.Card(snapshot, Rich(), world, far.Id);

            // Assert
            Assert.IsEmpty(card.Options);
            StringAssert.Contains("Too far", card.Note);
        }

        [Test]
        public void PanBy_DragHalfTheMapRight_MovesTheWindowLeft()
        {
            // Act
            var (dq, dr) = SharedWorldPresenter.PanBy(new Vector2(0.5f, 0f), 4);

            // Assert
            Assert.Less(dq, 0, "dragging the map right brings the west into view");
            Assert.AreEqual(0, dr);
        }

        [Test]
        public void At_NeighbourHexes_ApartByMoreThanANode()
        {
            // Act — at the default radius on a 380 px wide map.
            var a = SharedWorldPresenter.At(0, 0, 0, 0, SharedWorldPresenter.DefaultRadius);
            var b = SharedWorldPresenter.At(1, 0, 0, 0, SharedWorldPresenter.DefaultRadius);

            // Assert
            Assert.Greater((b - a).magnitude * 380f, 30f, "a compact node is about 30 px across");
        }
    }
}
