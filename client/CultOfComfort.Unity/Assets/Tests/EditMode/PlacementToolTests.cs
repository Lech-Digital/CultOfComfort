using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class PlacementToolTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        private static BaseSnapshot Fresh() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).Snapshot(T0);

        [Test]
        public void StartNew_OnTheRoad_SlidesToTheNearestSpotThatFits()
        {
            // Arrange — the middle of the view is on the raiders' road.
            var snapshot = Fresh();

            // Act
            var tool = PlacementTool.StartNew(BuildingType.Cottage, new GridPosition(17, 5), snapshot, VillageMap.Demo, BuildingTable.Default);

            // Assert
            Assert.IsNull(tool.Problem(snapshot, VillageMap.Demo, BuildingTable.Default));
            Assert.LessOrEqual(Math.Max(Math.Abs(tool.Anchor.X - 17), Math.Abs(tool.Anchor.Y - 5)), 4);
        }

        [TestCase(BuildingType.PeatCut)]
        [TestCase(BuildingType.Cottage)]
        [TestCase(BuildingType.Woodcutter)]
        public void StartNew_FromTheHearth_StartsWhereItFitsAndWorks(BuildingType type)
        {
            // Arrange — found on the Pixel: a Peat Cut took five drags to find a spot, and a Cottage stood unconnected.
            var snapshot = Fresh();
            var map = VillageMap.Demo;

            // Act
            var tool = PlacementTool.StartNew(type, map.HearthTiles[0], snapshot, map, BuildingTable.Default);

            // Assert
            Assert.IsNull(tool.Problem(snapshot, map, BuildingTable.Default));
            Assert.IsTrue(map.WouldConnect(snapshot, tool.Tiles(BuildingTable.Default)), "on the road network");
        }

        [Test]
        public void StartNew_ATower_StartsBesideTheRaidersRoad()
        {
            // Arrange
            var snapshot = Fresh();
            var map = VillageMap.Demo;

            // Act
            var tool = PlacementTool.StartNew(BuildingType.SlurryChute, map.HearthTiles[0], snapshot, map, BuildingTable.Default);

            // Assert
            var t = tool.Anchor;
            Assert.IsNull(tool.Problem(snapshot, map, BuildingTable.Default));
            Assert.IsTrue(new[] { new GridPosition(t.X + 1, t.Y), new GridPosition(t.X - 1, t.Y), new GridPosition(t.X, t.Y + 1), new GridPosition(t.X, t.Y - 1) }.Any(map.Road.Contains));
        }

        [Test]
        public void Drag_GrabbedByAnyTile_KeepsTheFingerOnThatTile()
        {
            // Arrange
            var snapshot = Fresh();
            var tool = PlacementTool.StartNew(BuildingType.Cottage, new GridPosition(20, 11), snapshot, VillageMap.Demo, BuildingTable.Default);
            var anchor = tool.Anchor;

            // Act — grab its far corner and drag two tiles right.
            Assert.IsTrue(tool.TryGrab(new GridPosition(anchor.X + 1, anchor.Y + 1), BuildingTable.Default));
            tool.DragTo(new GridPosition(anchor.X + 3, anchor.Y + 1));

            // Assert
            Assert.AreEqual(new GridPosition(anchor.X + 2, anchor.Y), tool.Anchor);
            Assert.IsFalse(tool.TryGrab(new GridPosition(anchor.X - 5, anchor.Y), BuildingTable.Default), "a finger off the ghost pans the camera");
        }

        [Test]
        public void Problem_OnTheRoad_SaysSo()
        {
            // Arrange
            var snapshot = Fresh();
            var tool = PlacementTool.StartNew(BuildingType.Cottage, new GridPosition(20, 11), snapshot, VillageMap.Demo, BuildingTable.Default);
            tool.TryGrab(tool.Anchor, BuildingTable.Default);

            // Act
            tool.DragTo(new GridPosition(16, 5)); // covers (17,5): the road

            // Assert
            Assert.AreEqual("That's the raiders' road. Build beside it.", tool.Problem(snapshot, VillageMap.Demo, BuildingTable.Default));
        }

        [Test]
        public void Command_NewAndMove_AreTheRightOrders()
        {
            // Arrange
            var snapshot = Fresh();
            var build = PlacementTool.StartNew(BuildingType.Woodcutter, new GridPosition(20, 11), snapshot, VillageMap.Demo, BuildingTable.Default);
            var startedTurned = build.Rotated; // it may start turned, to reach a road
            build.Rotate();
            var move = PlacementTool.StartMove(new BuildingView(7, BuildingType.Woodcutter, 1, new GridPosition(15, 5), 0, 0, 0, null, Rotated: true));

            // Act
            var place = build.Command();
            var relocate = move.Command();

            // Assert
            Assert.IsInstanceOf<PlaceBuilding>(place);
            Assert.AreEqual(!startedTurned, ((PlaceBuilding)place).Rotated);
            Assert.IsInstanceOf<MoveBuilding>(relocate);
            Assert.AreEqual(7, ((MoveBuilding)relocate).BuildingId);
            Assert.IsTrue(((MoveBuilding)relocate).Rotated, "a move starts as it stands");
        }
    }
}
