using System;
using System.Collections.Generic;
using System.IO;
using CultOfComfort.Contracts;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Review tool for the wire format (JsonUtility here, System.Text.Json on the server). Step 1 writes a sync
    /// request as the client would; send it with curl; step 2 reads the server's real answers back.
    /// Folder: COC_WIRE_DIR. Run explicitly with -testFilter WireFormatProbe.
    /// </summary>
    [Explicit, Category("Live")]
    public class WireFormatProbe
    {
        private static string Folder => Environment.GetEnvironmentVariable("COC_WIRE_DIR") ?? Path.GetTempPath();

        [Test]
        public void Step1_WriteRequest()
        {
            var commands = new List<BaseCommand>
            {
                new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, new GridPosition(7, 6)),
                new PaintTiles(Guid.NewGuid(), BuildingType.DirtTrack, new[] { new GridPosition(16, 10), new GridPosition(16, 11) }),
                new Collect(Guid.NewGuid(), null),
            };
            var request = new SyncRequestDto { expectedVersion = 0, commands = commands.ConvertAll(CommandMapping.ToDto) };
            File.WriteAllText(Path.Combine(Folder, "request.json"), JsonUtility.ToJson(request));
        }

        [Test]
        public void Step2_ReadAnswers()
        {
            foreach (var name in new[] { "base.json", "sync.json", "night.json" })
            {
                var text = File.ReadAllText(Path.Combine(Folder, name));
                var response = JsonUtility.FromJson<VillageResponseDto>(text);
                var data = VillageSaveMapping.FromDto(response.village);
                Assert.Greater(response.serverTimeTicks, 0, name);
                Assert.AreEqual(response.version, data.Version, name);
                var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);
                Debug.Log($"[wire] {name}: version {response.version}, {data.Buildings.Count} buildings, outcomes " +
                          string.Join(", ", (response.outcomes ?? new List<OutcomeDto>()).ConvertAll(o => o.status + (o.hasPlan ? "+plan" : ""))));
                foreach (var outcome in response.outcomes ?? new List<OutcomeDto>())
                {
                    if (outcome.hasPlan)
                    {
                        Assert.IsNotNull(VillageSaveMapping.PlanFromDto(outcome.plan).Towers);
                    }
                }

                Assert.IsNotNull(state.Snapshot(new DateTimeOffset(response.serverTimeTicks, TimeSpan.Zero)));
            }
        }
    }
}
