using System.Collections.Generic;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>World-space sprites made from the HUD icons (Resources/UI/Icons), created once and cached.</summary>
    public static class IconSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <returns>The sprite, or null when the icon does not exist.</returns>
        public static Sprite Get(string name)
        {
            if (!Cache.TryGetValue(name, out var sprite))
            {
                var texture = Resources.Load<Texture2D>("UI/Icons/" + name);
                sprite = texture == null ? null : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 300f);
                Cache[name] = sprite;
            }

            return sprite;
        }
    }
}
