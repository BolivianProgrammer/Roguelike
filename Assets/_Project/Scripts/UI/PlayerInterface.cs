using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Player;

namespace Roguelike.UI
{
    /// <summary>
    /// Adds the player's interface to every scene that has a player: the wrist display, the pause menu, the
    /// comfort options and the armored gloves. Nothing has to be placed in scenes by hand.
    /// </summary>
    public static class PlayerInterface
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The title screen has its own menu (with settings); no pause menu or wrist display there.
            if (scene.name == Meta.SceneFlow.MenuScene) return;
            var player = Object.FindObjectOfType<PlayerHealth>();
            if (player == null || player.GetComponent<PauseMenu>() != null) return;

            player.gameObject.AddComponent<PauseMenu>();
            player.gameObject.AddComponent<PlayerComfort>();
            player.gameObject.AddComponent<Gauntlets>();
            // After the hands have found their controllers.
            player.gameObject.AddComponent<WristAttacher>();
        }
    }
}
