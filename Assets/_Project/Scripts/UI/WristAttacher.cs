using UnityEngine;
using Roguelike.Meta;

namespace Roguelike.UI
{
    /// <summary>Puts the wrist display on the off hand, and moves it if the player switches their dominant hand.</summary>
    public class WristAttacher : MonoBehaviour
    {
        WristDisplay display;
        bool leftHanded;

        void Start()
        {
            leftHanded = GameSettings.Current.leftHanded;
            display = WristDisplay.Attach();
            GameSettings.Changed += OnSettingsChanged;
        }

        void OnDestroy() => GameSettings.Changed -= OnSettingsChanged;

        void OnSettingsChanged()
        {
            if (GameSettings.Current.leftHanded == leftHanded) return;
            leftHanded = GameSettings.Current.leftHanded;
            if (display != null)
            {
                foreach (Transform child in display.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Wrist Display") Destroy(child.gameObject);
                Destroy(display);
            }
            // The old display is gone at the end of this frame; the new one goes on the other hand now.
            display = WristDisplay.Attach();
        }
    }
}
