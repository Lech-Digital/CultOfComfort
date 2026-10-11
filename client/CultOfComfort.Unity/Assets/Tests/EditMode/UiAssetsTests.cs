using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class UiAssetsTests
    {
        [Test]
        public void Fonts_EveryListedFont_LoadsFromResources()
        {
            foreach (var path in UiAssets.Fonts)
            {
                // Act
                var font = Resources.Load<Font>(path);

                // Assert
                Assert.IsNotNull(font, path);
            }
        }

        [Test]
        public void KitTextures_EveryListedTexture_LoadsBilinearWithoutMips()
        {
            foreach (var name in UiAssets.KitTextures)
            {
                // Act
                var texture = Resources.Load<Texture2D>("UI/Kit/" + name);

                // Assert
                Assert.IsNotNull(texture, name);
                Assert.AreEqual(FilterMode.Bilinear, texture.filterMode, name);
                Assert.AreEqual(1, texture.mipmapCount, name);
            }
        }
    }
}
