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
    }
}
