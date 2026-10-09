using UnityEngine;
using UnityEngine.InputSystem;

namespace CultOfComfort.Client
{
    /// <summary>One code path for mouse (editor) and touch (phone) via the Input System's Pointer.</summary>
    public static class PointerInput
    {
        public static bool TryGetTap(out Vector2 screenPosition)
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        public static bool TryGetHover(out Vector2 screenPosition)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }
    }
}
