using System.Collections.Generic;
using CultOfComfort.Simulation.Combat;
using UnityEngine;

namespace CultOfComfort.Client
{
    public interface ITipStore
    {
        bool HasSeen(string id);
        void MarkSeen(string id);
        void Reset(IEnumerable<string> ids);
    }

    /// <summary>Remembers seen tips on the device.</summary>
    public sealed class PlayerPrefsTipStore : ITipStore
    {
        private const string Prefix = "tip.";

        public bool HasSeen(string id) => PlayerPrefs.GetInt(Prefix + id, 0) == 1;

        public void MarkSeen(string id)
        {
            PlayerPrefs.SetInt(Prefix + id, 1);
            PlayerPrefs.Save();
        }

        public void Reset(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                PlayerPrefs.DeleteKey(Prefix + id);
            }

            PlayerPrefs.Save();
        }
    }

    /// <summary>First-use explanations of the Prophet's skills, each shown once ever (spec: First-use tips).</summary>
    public sealed class TipBook
    {
        private static readonly Dictionary<PlayerActionType, (string Id, string Text)> Tips = new Dictionary<PlayerActionType, (string, string)>
        {
            [PlayerActionType.OpenSluiceGate] = ("sluice", "Opened the cistern: everyone got washed three tiles back down the road and is muddy for a bit — Sapling Flingers love muddy targets. Two per night."),
            [PlayerActionType.PhantomVoice] = ("voice", "The Voice from the Box: my Bluetooth speaker, full volume. Raiders near me panic and walk backwards for two seconds."),
            [PlayerActionType.HotStones] = ("stones", "Hot stones: hurts and muddies everyone in the circle. Muddy raiders take extra damage from Sapling Flingers."),
        };

        private readonly ITipStore _store;

        public TipBook(ITipStore store) => _store = store;

        /// <returns>The tip the first time this skill is used, otherwise null.</returns>
        public string Take(PlayerActionType skill)
        {
            if (!Tips.TryGetValue(skill, out var tip) || _store.HasSeen(tip.Id))
            {
                return null;
            }

            _store.MarkSeen(tip.Id);
            return tip.Text;
        }

        public void Reset()
        {
            var ids = new List<string>();
            foreach (var tip in Tips.Values)
            {
                ids.Add(tip.Id);
            }

            _store.Reset(ids);
        }
    }
}
