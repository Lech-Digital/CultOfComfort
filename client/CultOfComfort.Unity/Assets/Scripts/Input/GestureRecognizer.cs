using System;
using System.Collections.Generic;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>What the player did this frame. At most one of tap / pan / pinch is meaningful at a time.</summary>
    public struct GestureFrame
    {
        public bool Tapped;
        public Vector2 TapPosition;
        public bool Panned;
        public Vector2 PanFrom;
        public Vector2 PanTo;
        public bool Pinched;
        public float PinchRatio;
        public Vector2 PinchCenter;
    }

    /// <summary>
    /// Turns raw pointer positions (one per finger, or the mouse) into tap / pan / pinch. Pure C#, no Unity
    /// objects, so it is unit-tested in EditMode. A tap fires on release, and only if the pointer stayed
    /// within <c>tapSlop</c> pixels and no second finger joined — otherwise the gesture was a camera move.
    /// </summary>
    public sealed class GestureRecognizer
    {
        private readonly float _tapSlop;
        private readonly List<Vector2> _last = new List<Vector2>();
        private bool _active;
        private bool _ignored;
        private bool _tapCancelled;
        private Vector2 _start;

        public GestureRecognizer(float tapSlopPixels)
        {
            _tapSlop = tapSlopPixels;
        }

        /// <summary>True while the current gesture has turned into a camera move (no tap will follow).</summary>
        public bool IsDragging => _active && !_ignored && _tapCancelled;

        /// <summary>A single finger is down and would tap if lifted now — lets the board preview the target tile.</summary>
        public bool TryGetPendingTap(out Vector2 position)
        {
            var pending = _active && !_ignored && !_tapCancelled && _last.Count == 1;
            position = pending ? _last[0] : default;
            return pending;
        }

        /// <param name="pointers">Positions of every pressed pointer this frame, in screen pixels.</param>
        /// <param name="isOverUi">Gestures that start on UI are ignored entirely.</param>
        public GestureFrame Update(IReadOnlyList<Vector2> pointers, Func<Vector2, bool> isOverUi)
        {
            var frame = default(GestureFrame);

            if (pointers.Count == 0)
            {
                if (_active && !_ignored && !_tapCancelled && _last.Count > 0)
                {
                    frame.Tapped = true;
                    frame.TapPosition = _last[0];
                }

                _active = false;
                _last.Clear();
                return frame;
            }

            if (!_active)
            {
                _active = true;
                _ignored = isOverUi(pointers[0]);
                _tapCancelled = false;
                _start = pointers[0];
                Remember(pointers);
                return frame;
            }

            if (_ignored)
            {
                Remember(pointers);
                return frame;
            }

            if (pointers.Count >= 2)
            {
                _tapCancelled = true;
                if (_last.Count >= 2)
                {
                    var previousCenter = (_last[0] + _last[1]) / 2f;
                    var center = (pointers[0] + pointers[1]) / 2f;
                    var previousSpan = (_last[0] - _last[1]).magnitude;
                    var span = (pointers[0] - pointers[1]).magnitude;

                    frame.Panned = true;
                    frame.PanFrom = previousCenter;
                    frame.PanTo = center;
                    if (previousSpan > 1f)
                    {
                        frame.Pinched = true;
                        frame.PinchRatio = span / previousSpan;
                        frame.PinchCenter = center;
                    }
                }
            }
            else if (_last.Count == 1)
            {
                // Coming down from two fingers to one skips this branch for a frame, so the camera doesn't jump.
                if (!_tapCancelled && (pointers[0] - _start).magnitude > _tapSlop)
                {
                    // Pan from where the finger went down, so the board catches up with the slop and stays glued.
                    _tapCancelled = true;
                    frame.Panned = true;
                    frame.PanFrom = _start;
                    frame.PanTo = pointers[0];
                }
                else if (_tapCancelled)
                {
                    frame.Panned = true;
                    frame.PanFrom = _last[0];
                    frame.PanTo = pointers[0];
                }
            }

            Remember(pointers);
            return frame;
        }

        private void Remember(IReadOnlyList<Vector2> pointers)
        {
            _last.Clear();
            for (var i = 0; i < pointers.Count; i++)
            {
                _last.Add(pointers[i]);
            }
        }
    }
}
