using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>A button in the hall: what it says, whether you can, and its id for the controller.</summary>
    public sealed class HallAction
    {
        public string Id;
        public string Label;
        public string Note;
        public bool Enabled;
    }

    /// <summary>The lord's hall (spec 2026-10-16): his face and temper, his demand as a letter, what you can try, the tithe.</summary>
    public sealed class LordHallModel
    {
        public string Lord = "Lord Wulfric";
        public string Seat;
        public string Mood;
        public int MoodLevel;
        public string Speech;
        public string Temper;
        public float TemperFraction;
        public string TemperHelp;

        /// <summary>The open demand (null: none): the letter, the time left, and the buttons.</summary>
        public string Letter;
        public string LetterDue;
        public HallAction Deliver;
        public List<HallAction> Pleas = new List<HallAction>();
        public string NoDemand;

        public List<TitheOption> Tithes = new List<TitheOption>();
        public string TitheNote;
        public int DemandId = -1;
        public bool CanShowSeat;
    }

    /// <summary>Builds the hall from the village. Pure: tested without Unity's UI.</summary>
    public static class LordHallPresenter
    {
        private static readonly string[] Reasons =
        {
            "for his new stables", "for the Bishop's feast", "to mend the drawbridge (again)", "for his daughter's dowry",
            "for a portrait of himself", "to restock the moat's eels", "for a war he's \"thinking about\"", "for reasons he won't explain",
        };

        /// <param name="lord">"Lord Thorkel Minor", "the Bishop of Sumpwell".</param>
        public static LordHallModel Build(BaseSnapshot snapshot, BuildingTable table, string lord, string seat, DateTimeOffset now)
        {
            var domain = snapshot.Domain;
            var tension = snapshot.Tension;
            var t = table.Tension;
            var name = Capital(lord);
            var raid = snapshot.RaidWarningEndsAtUtc != null;
            var model = new LordHallModel
            {
                Lord = name, Seat = seat, CanShowSeat = !string.IsNullOrEmpty(seat),
                TemperFraction = Mathf.Clamp01(tension / (float)t.Max),
                Temper = WorldMapPresenter.TemperLine(lord, tension, t.Max, domain?.TensionPerHourMilli ?? 0, raid),
                TemperHelp = $"Your faith in his lands makes him cross. When the tape on your village fills ({t.Hostile}), he sends men. Paying him calms him.",
            };
            (model.MoodLevel, model.Mood) = raid ? (3, "Furious: his men are coming")
                : tension >= t.Hostile ? (3, "Furious")
                : tension >= t.Uneasy ? (2, "Seething")
                : tension >= t.Uneasy / 2 ? (1, "Grumpy")
                : (0, "Content, for now");

            var demand = domain?.Demand;
            if (demand != null)
            {
                model.DemandId = demand.Id;
                var left = VillagePresenter.Duration((long)Math.Max(0, (demand.DueAtUtc - now).TotalSeconds));
                model.Letter = $"By order of {name}: {VillagePresenter.ShortCost(demand.Amount)}, {Reasons[demand.Reason % Reasons.Length]}.";
                model.LetterDue = $"{left} left. Deliver and his temper drops by {demand.TemperDrop}. Ignore it and it rises by {demand.Penalty}.";
                model.Deliver = new HallAction
                {
                    Id = "deliver", Label = $"Deliver · {VillagePresenter.ShortCost(demand.Amount)}", Enabled = snapshot.Stock.Covers(demand.Amount),
                    Note = snapshot.Stock.Covers(demand.Amount) ? null : "You haven't got that much yet.",
                };
                var haggled = demand.Haggle == DemandHaggle.None;
                model.Pleas.Add(new HallAction
                {
                    Id = "haggle", Enabled = haggled,
                    Label = demand.Haggle == DemandHaggle.Won ? "Haggled: he took less"
                        : demand.Haggle == DemandHaggle.Refused ? "Haggled: he refused"
                        : $"Haggle · ask for {VillagePresenter.ShortCost(demand.HaggledAmount)}",
                    Note = haggled ? (demand.HaggleChancePercent >= 100 ? "Flattered, he can't say no." : $"Even odds. If he refuses: temper +{Demands.RefusePenalty}.") : null,
                });
                model.Pleas.Add(new HallAction
                {
                    Id = "flatter", Enabled = haggled && !demand.Flattered && snapshot.Stock.Covers(demand.FlatterCost),
                    Label = demand.Flattered ? "Flattered: he's purring" : $"Flatter him · {VillagePresenter.ShortCost(demand.FlatterCost)}",
                    Note = demand.Flattered || !haggled ? null : "Candles and a poem about his chin. Then a haggle can't fail.",
                });
                model.Pleas.Add(new HallAction
                {
                    Id = "stall", Enabled = !demand.Stalled,
                    Label = demand.Stalled ? "Stalled once: he won't wait again" : $"Stall · +{Demands.StallHours}h",
                    Note = demand.Stalled ? null : $"\"The cart has a wobbly wheel.\" Temper +{Demands.StallPenalty}.",
                });
                model.Speech = demand.Haggle == DemandHaggle.Refused ? "\"Less? LESS? Do you know how much a moat costs?\""
                    : demand.Haggle == DemandHaggle.Won ? "\"Fine. But only because I'm in a generous mood.\""
                    : demand.Flattered ? "\"A poem about my chin? Well. It is a good chin.\""
                    : "\"You'll find my request in the letter. It isn't a request.\"";
            }
            else
            {
                var next = domain?.NextDemandAtUtc;
                model.NoDemand = next == null
                    ? $"No demands yet. They start once your Hearth reaches level {table.Domain.UnlockHearth}."
                    : $"No demand right now. His next letter comes in about {VillagePresenter.Duration((long)Math.Max(0, (next.Value - now).TotalSeconds))}.";
                model.Speech = model.MoodLevel switch
                {
                    0 => "\"Ah, the bath people. Carry on. Quietly.\"",
                    1 => "\"I hear you've been preaching in my villages.\"",
                    2 => "\"My peasants talk of nothing but your hot water. I don't like it.\"",
                    _ => "\"Enough. My men will settle this.\"",
                };
            }

            model.Tithes = WorldMapPresenter.Tithes(snapshot, table);
            model.TitheNote = domain?.TitheHalfUntilUtc != null
                ? $"He was paid recently: another tithe works at half strength for {VillagePresenter.Duration((long)(domain.TitheHalfUntilUtc.Value - now).TotalSeconds)}."
                : raid ? "Pay enough to bring his temper under " + t.Uneasy + " and he calls the raid off."
                : "A gift on top of his demands, when you want him calmer.";
            return model;
        }

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    /// <summary>
    /// The lord's hall on screen (spec 2026-10-16): a screen of its own, not the map. His portrait and mood at the top,
    /// his demand as a sealed letter with Deliver and the pleas, then the voluntary tithe. Raises events.
    /// </summary>
    public sealed class LordHallView
    {
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly VisualElement _face;
        private readonly Label _mood;
        private readonly Label _speech;
        private readonly Label _temper;
        private readonly VisualElement _temperFill;
        private readonly Label _temperHelp;
        private readonly VisualElement _letter;
        private readonly VisualElement _tithes;
        private readonly Label _titheNote;
        private readonly Button _showSeat;
        private string _signature;
        private int _demandId = -1;

        public event Action<int> DeliverClicked;
        public event Action<int, Plea> PleaClicked;
        public event Action<ResourceBag> TitheClicked;
        public event Action ShowSeatClicked;
        public event Action Closed;

        public LordHallView(VisualElement parent)
        {
            _root = new VisualElement { name = "lord-hall" };
            _root.AddToClassList("panel");
            _root.AddToClassList("world-map");
            _root.AddToClassList("hall");
            var header = new VisualElement();
            header.AddToClassList("map-header");
            _title = new Label("The lord's hall");
            _title.AddToClassList("map-title");
            _title.AddToClassList("house-title");
            var close = UiGlyphs.Close("hall-close", () =>
            {
                Show(false);
                Closed?.Invoke();
            });
            close.AddToClassList("map-close");
            header.Add(_title);
            header.Add(close);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sheet-scroll");
            scroll.style.flexGrow = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var throne = new VisualElement();
            throne.AddToClassList("hall-throne");
            _face = new VisualElement();
            _face.AddToClassList("hall-face");
            var faceArt = new VisualElement();
            faceArt.AddToClassList("icon");
            faceArt.AddToClassList("icon-portrait_lord");
            faceArt.AddToClassList("hall-face-art");
            _face.Add(faceArt);
            var words = new VisualElement();
            words.AddToClassList("hall-words");
            _mood = new Label();
            _mood.AddToClassList("hall-mood");
            _speech = new Label();
            _speech.AddToClassList("hall-speech");
            _speech.AddToClassList("wrap");
            words.Add(_mood);
            words.Add(_speech);
            throne.Add(_face);
            throne.Add(words);

            _temper = new Label();
            _temper.AddToClassList("map-line");
            var bar = new VisualElement();
            bar.AddToClassList("map-tension-bar");
            _temperFill = new VisualElement();
            _temperFill.AddToClassList("map-tension-fill");
            bar.Add(_temperFill);
            _temperHelp = new Label();
            _temperHelp.AddToClassList("small");
            _temperHelp.AddToClassList("wrap");

            _letter = new VisualElement { name = "hall-letter" };
            _tithes = new VisualElement { name = "hall-tithes" };
            _titheNote = new Label();
            _titheNote.AddToClassList("small");
            _titheNote.AddToClassList("wrap");
            _showSeat = new Button(() => ShowSeatClicked?.Invoke()) { text = "Show his seat on the map", name = "hall-seat" };
            _showSeat.AddToClassList("plain");
            _showSeat.AddToClassList("house-map-button");

            scroll.Add(throne);
            scroll.Add(_temper);
            scroll.Add(bar);
            scroll.Add(_temperHelp);
            scroll.Add(Heading("His demand"));
            scroll.Add(_letter);
            scroll.Add(Heading("A tithe, unasked"));
            scroll.Add(_tithes);
            scroll.Add(_titheNote);
            scroll.Add(_showSeat);
            _root.Add(header);
            _root.Add(scroll);
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public void Show(bool open)
        {
            _root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                _signature = null;
            }
        }

        public void Render(LordHallModel model)
        {
            _title.text = model.Seat == null ? $"{model.Lord}'s hall" : model.Seat;
            _mood.text = $"{model.Lord} · {model.Mood}";
            _speech.text = model.Speech;
            for (var i = 0; i < 4; i++)
            {
                _face.EnableInClassList($"hall-mood-{i}", model.MoodLevel == i);
            }

            _temper.text = model.Temper;
            _temperFill.style.width = Length.Percent(model.TemperFraction * 100f);
            _temperHelp.text = model.TemperHelp;
            _titheNote.text = model.TitheNote;
            _showSeat.style.display = model.CanShowSeat ? DisplayStyle.Flex : DisplayStyle.None;
            _demandId = model.DemandId;

            var signature = model.Letter + model.LetterDue + model.NoDemand + model.Deliver?.Label + model.Deliver?.Enabled
                            + string.Join("|", model.Pleas.ConvertAll(p => p.Label + p.Enabled + p.Note))
                            + "#" + string.Join("|", model.Tithes.ConvertAll(o => o.Label + o.Enabled));
            if (signature == _signature)
            {
                return;
            }

            _signature = signature;
            _letter.Clear();
            if (model.Letter == null)
            {
                var none = new Label(model.NoDemand);
                none.AddToClassList("sheet-line");
                none.AddToClassList("wrap");
                _letter.Add(none);
            }
            else
            {
                var paper = new VisualElement();
                paper.AddToClassList("hall-paper");
                var seal = new VisualElement();
                seal.AddToClassList("hall-seal");
                var text = new Label(model.Letter);
                text.AddToClassList("hall-letter-text");
                text.AddToClassList("wrap");
                var due = new Label(model.LetterDue);
                due.AddToClassList("small");
                due.AddToClassList("wrap");
                paper.Add(seal);
                paper.Add(text);
                paper.Add(due);
                _letter.Add(paper);
                _letter.Add(ActionButton(model.Deliver, "hall-deliver", () => DeliverClicked?.Invoke(_demandId)));
                var or = new Label("…or try your luck:");
                or.AddToClassList("small");
                or.AddToClassList("hall-or");
                _letter.Add(or);
                foreach (var plea in model.Pleas)
                {
                    var kind = plea.Id == "haggle" ? Plea.Haggle : plea.Id == "flatter" ? Plea.Flatter : Plea.Stall;
                    _letter.Add(ActionButton(plea, "hall-plea", () => PleaClicked?.Invoke(_demandId, kind)));
                }
            }

            _tithes.Clear();
            foreach (var option in model.Tithes)
            {
                var amount = option.Amount;
                var b = new Button(() => TitheClicked?.Invoke(amount)) { text = option.Label };
                b.AddToClassList("plain");
                b.AddToClassList("hall-tithe");
                b.SetEnabled(option.Enabled);
                _tithes.Add(b);
            }
        }

        private static VisualElement ActionButton(HallAction action, string cls, Action clicked)
        {
            var holder = new VisualElement();
            var b = new Button(clicked) { text = action.Label, name = $"hall-{action.Id}" };
            b.AddToClassList("plain");
            b.AddToClassList(cls);
            b.SetEnabled(action.Enabled);
            holder.Add(b);
            if (!string.IsNullOrEmpty(action.Note))
            {
                var note = new Label(action.Note);
                note.AddToClassList("small");
                note.AddToClassList("wrap");
                note.AddToClassList("hall-note");
                holder.Add(note);
            }

            return holder;
        }

        private static Label Heading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("sheet-heading");
            return l;
        }
    }
}
