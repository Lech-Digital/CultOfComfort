using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Little ink-drawn glyphs for header buttons: the Fell fonts have no ✕ or ⌂, and the phone drew them as boxes
    /// (owner 2026-10-15: "labels and texts are weird").
    /// </summary>
    public static class UiGlyphs
    {
        private static readonly Color Ink = new Color(0.23f, 0.16f, 0.09f);

        public static Button Close(string name, Action clicked) => Glyph(name, clicked, (p, w, h) =>
        {
            var s = Mathf.Min(w, h) * 0.22f;
            var c = new Vector2(w / 2f, h / 2f);
            p.BeginPath();
            p.MoveTo(c + new Vector2(-s, -s));
            p.LineTo(c + new Vector2(s, s));
            p.MoveTo(c + new Vector2(s, -s));
            p.LineTo(c + new Vector2(-s, s));
            p.Stroke();
        });

        public static Button Home(string name, Action clicked) => Glyph(name, clicked, (p, w, h) =>
        {
            var s = Mathf.Min(w, h) * 0.26f;
            var c = new Vector2(w / 2f, h / 2f + s * 0.15f);
            p.BeginPath();
            p.MoveTo(c + new Vector2(-s * 1.1f, -s * 0.05f));
            p.LineTo(c + new Vector2(0f, -s * 1.05f));
            p.LineTo(c + new Vector2(s * 1.1f, -s * 0.05f));
            p.MoveTo(c + new Vector2(-s * 0.75f, -s * 0.3f));
            p.LineTo(c + new Vector2(-s * 0.75f, s * 0.85f));
            p.LineTo(c + new Vector2(s * 0.75f, s * 0.85f));
            p.LineTo(c + new Vector2(s * 0.75f, -s * 0.3f));
            p.Stroke();
        });

        private static Button Glyph(string name, Action clicked, Action<Painter2D, float, float> draw)
        {
            var button = new Button(clicked) { name = name, text = string.Empty };
            button.generateVisualContent += context =>
            {
                var w = context.visualElement.resolvedStyle.width;
                var h = context.visualElement.resolvedStyle.height;
                var p = context.painter2D;
                p.strokeColor = Ink;
                p.lineWidth = 2.4f;
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                draw(p, w, h);
            };
            return button;
        }
    }
}
