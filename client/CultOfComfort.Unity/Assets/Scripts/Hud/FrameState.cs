using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>One number on the frame. <see cref="IsDemo"/> values are placeholders and get a visible "demo" mark.</summary>
    public readonly struct Reading
    {
        public Reading(int value, int cap, bool isDemo)
        {
            Value = value;
            Cap = cap;
            IsDemo = isDemo;
        }

        public int Value { get; }
        public int Cap { get; }
        public bool IsDemo { get; }
    }

    /// <summary>What the game frame shows: resources, Hearth level, Comfort and Tension.</summary>
    public readonly struct FrameState
    {
        public FrameState(Reading timber, Reading scrap, Reading peat, Reading devotion, Reading pennies, Reading comfort,
            int hearthLevel, float tension, string tensionNote, bool tensionIsDemo, Reading people = default, bool cold = false)
        {
            People = people;
            Cold = cold;
            Timber = timber;
            Scrap = scrap;
            Peat = peat;
            Devotion = devotion;
            Pennies = pennies;
            Comfort = comfort;
            HearthLevel = hearthLevel;
            Tension = tension;
            TensionNote = tensionNote;
            TensionIsDemo = tensionIsDemo;
        }

        public Reading Timber { get; }
        public Reading Scrap { get; }
        public Reading Peat { get; }
        public Reading Devotion { get; }
        public Reading Pennies { get; }
        public Reading Comfort { get; }
        public int HearthLevel { get; }

        /// <summary>Villagers (value) and beds (cap).</summary>
        public Reading People { get; }

        /// <summary>The Peat ran out: nobody works.</summary>
        public bool Cold { get; }

        /// <summary>0..1: how close the next raid is.</summary>
        public float Tension { get; }
        public string TensionNote { get; }
        public bool TensionIsDemo { get; }
    }

    /// <summary>
    /// Placeholder economy until base state exists (spec: Placeholder data). Only Scrap is real. The buildings work
    /// replaces this class; nothing else should invent resource numbers.
    /// </summary>
    public static class DemoEconomy
    {
        /// <summary>Hearth 1 storage cap per resource (docs/gdd/02-economy.md).</summary>
        public const int StorageCap = 1000;

        private const string TensionNote = "Wulfric's coming in ~3h";

        public static FrameState Frame(int scrap, int hearthLevel) => new FrameState(
            new Reading(480, StorageCap, true),
            new Reading(Mathf.Max(0, scrap), StorageCap, false),
            new Reading(120, StorageCap, true),
            new Reading(15, StorageCap, true),
            new Reading(50, 0, true),
            new Reading(42, 0, true),
            hearthLevel,
            0.62f,
            TensionNote,
            true);
    }
}
