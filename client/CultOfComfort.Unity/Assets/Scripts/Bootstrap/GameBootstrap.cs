using UnityEngine;
using UnityEngine.SceneManagement;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Code-first entry point: builds the whole game from scratch whenever a scene loads, so nothing
    /// has to be wired up by hand in the editor. Just press Play.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // -= first: with "Enter Play Mode Options" (no domain reload) statics survive between sessions.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            DeterminismProbe.Run();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Object.FindAnyObjectByType<GameController>() != null)
            {
                return;
            }

            var root = new GameObject("Cult of Comfort");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<GameController>();
        }
    }
}
