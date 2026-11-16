using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Roguelike.Meta;
using Roguelike.Player;
using Roguelike.Run;

namespace Roguelike.UI
{
    /// <summary>
    /// The pause menu: Esc on the keyboard, the Menu button on the left controller. The game stops while it is
    /// open. From here: resume, change settings, abandon the run (it counts as dying), return to the
    /// title screen, or quit. Created automatically wherever there is a player.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        InputAction toggle;
        string page = "main";
        bool confirmAbandon;
        bool open;

        /// <summary>True while the game is paused by this menu.</summary>
        public static bool IsPaused { get; private set; }

        void OnEnable()
        {
            toggle = new InputAction("Pause", InputActionType.Button);
            toggle.AddBinding("<Keyboard>/escape");
            toggle.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            toggle.AddBinding("<Gamepad>/start");
            toggle.Enable();
        }

        void OnDisable()
        {
            toggle.Disable();
            if (open) Resume();
        }

        void Update()
        {
            if (!toggle.WasPressedThisFrame()) return;
            if (open) Resume();
            else if (!WorldMenu.IsOpen && !BuffCards.IsOpen) Open();
        }

        void Open()
        {
            open = true;
            IsPaused = true;
            page = "main";
            confirmAbandon = false;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            WorldMenu menu = WorldMenu.Open("PAUSED", Header, Entries, null);
            menu.Closed += OnMenuClosed;
        }

        void OnMenuClosed()
        {
            if (open) Resume();
        }

        void Resume()
        {
            open = false;
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (WorldMenu.IsOpen) WorldMenu.Close();
        }

        string Header()
        {
            if (page == "settings") return "Settings are saved as you change them";
            var mission = MissionManager.Current;
            if (mission != null) return MissionManager.Objective;
            return SceneManager.GetActiveScene().name;
        }

        List<WorldMenu.Entry> Entries()
        {
            return page == "settings" ? SettingsEntries() : MainEntries();
        }

        List<WorldMenu.Entry> MainEntries()
        {
            var entries = new List<WorldMenu.Entry>
            {
                new WorldMenu.Entry { Label = "Resume", Action = Resume },
                new WorldMenu.Entry { Label = "Settings >", Action = () => page = "settings" },
            };

            bool inMission = MissionManager.Current != null;
            if (inMission)
            {
                entries.Add(new WorldMenu.Entry
                {
                    Label = confirmAbandon ? "Really abandon? This run's loot is lost" : "Abandon run",
                    Detail = confirmAbandon ? "select again" : "counts as dying",
                    Action = () =>
                    {
                        if (!confirmAbandon) { confirmAbandon = true; return; }
                        Resume();
                        MissionManager.Current.Abandon();
                    },
                });
            }

            // Leaving a mission for the title counts as abandoning it, so it is only offered outside missions.
            if (!inMission)
                entries.Add(new WorldMenu.Entry { Label = "Quit to title", Action = () => { Resume(); SceneFlow.Load(SceneFlow.MenuScene); } });
            entries.Add(new WorldMenu.Entry
            {
                Label = "Quit game",
                Detail = inMission ? "counts as dying" : "",
                Action = () =>
                {
                    Resume();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                },
            });
            return entries;
        }

        List<WorldMenu.Entry> SettingsEntries()
        {
            SettingsData s = GameSettings.Current;
            var entries = new List<WorldMenu.Entry>
            {
                Stepper("Master volume", Percent(s.masterVolume), () => s.masterVolume = Step(s.masterVolume, 0.1f)),
                Stepper("Effects volume", Percent(s.sfxVolume), () => s.sfxVolume = Step(s.sfxVolume, 0.1f)),
                Stepper("Music volume", Percent(s.musicVolume), () => s.musicVolume = Step(s.musicVolume, 0.1f)),
                Stepper("Comfort vignette", s.comfortVignette ? "on" : "off", () => s.comfortVignette = !s.comfortVignette),
                Stepper("Dominant hand", s.leftHanded ? "left" : "right", () => s.leftHanded = !s.leftHanded),
                Stepper("Seated height boost", Mathf.RoundToInt(s.seatedHeightOffset * 100f) + " cm", () => s.seatedHeightOffset = s.seatedHeightOffset >= 0.6f ? 0f : s.seatedHeightOffset + 0.1f),
            };
            if (DesktopRig.IsDesktop)
            {
                entries.Add(Stepper("Mouse sensitivity", s.mouseSensitivity.ToString("F2"), () => s.mouseSensitivity = s.mouseSensitivity >= 0.3f ? 0.04f : s.mouseSensitivity + 0.02f));
                entries.Add(Stepper("Key hints", s.showKeyHints ? "shown" : "hidden", () => s.showKeyHints = !s.showKeyHints));
            }
            entries.Add(new WorldMenu.Entry { Label = "< Back", Action = () => page = "main" });
            return entries;
        }

        /// <summary>A setting line: selecting it moves the value one step (wrapping round at the end).</summary>
        static WorldMenu.Entry Stepper(string label, string value, System.Action change)
        {
            return new WorldMenu.Entry
            {
                Label = label,
                Detail = value,
                Action = () =>
                {
                    change();
                    GameSettings.Save();
                },
            };
        }

        static float Step(float value, float step) => value >= 0.999f ? 0f : Mathf.Min(1f, value + step);

        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";
    }
}
