using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace CultOfComfort.Client
{
    /// <summary>Raw pointer state from the Input System: every finger on a phone, or the mouse in the editor.</summary>
    public static class PointerInput
    {
        /// <summary>
        /// EnhancedTouch keeps per-touch history, so a touch that goes down and up within one frame is still
        /// reported (Began this frame, Ended the next). Polling raw Touchscreen controls loses such quick taps.
        /// </summary>
        public static void Enable()
        {
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }
        }

        /// <summary>Fills <paramref name="pointers"/> with the screen position of each finger down (or the pressed mouse).</summary>
        public static void GetPressedPointers(List<Vector2> pointers)
        {
            pointers.Clear();
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                {
                    pointers.Add(touch.screenPosition);
                }
            }

            if (pointers.Count > 0 || Touchscreen.current != null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            {
                pointers.Add(mouse.position.ReadValue());
            }
        }

        /// <summary>Mouse wheel / trackpad scroll this frame (0 on phones).</summary>
        public static float ScrollDelta()
        {
            var mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
        }

        public static bool TryGetHover(out Vector2 screenPosition)
        {
            var mouse = Mouse.current;
            if (mouse != null && Touchscreen.current == null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }
    }
}
