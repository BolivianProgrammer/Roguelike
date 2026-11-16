using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Roguelike.Audio;

namespace Roguelike.Meta
{
    /// <summary>
    /// The title screen: a title out of the dark, "press any button", then a short list to pick from
    /// (Continue, New Game, Settings, Quit). Works with keyboard (arrows or W/S, Enter, Esc), mouse (hover and
    /// click) and VR controllers (thumbstick up/down, trigger or A to confirm, B to go back).
    /// The Settings entry swaps the list for the options, using the same controls.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        enum Stage { Intro, PressAny, Menu, Leaving }

        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text prompt;
        [SerializeField] TMP_Text footer;
        [Tooltip("Menu entries from top to bottom: Continue, New Game, Quit. A Settings entry is added before Quit.")]
        [SerializeField] MainMenuItem[] items;
        [SerializeField] RectTransform selectionMarker;

        [Header("Look")]
        [SerializeField] Color idleColor = new Color(0.45f, 0.43f, 0.4f);
        [SerializeField] Color selectedColor = new Color(0.95f, 0.9f, 0.8f);
        [SerializeField] Color disabledColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] float introFade = 2.5f;

        const int ContinueIndex = 0, NewGameIndex = 1, SettingsIndex = 2;

        Stage stage = Stage.Intro;
        float stageTime;
        int selected;
        bool confirmNewGame;
        float stickCooldown;
        InputAction navigate;
        InputAction submit;
        InputAction back;
        readonly List<int> enabledItems = new List<int>();

        MainMenuItem[] mainItems;
        MainMenuItem[] settingsItems;
        MainMenuItem[] current;
        bool inSettings;

        class Setting
        {
            public string name;
            public Func<string> value;
            public Action change;
        }
        List<Setting> settings;

        void Awake()
        {
            // Keyboard and both VR controllers drive the same actions.
            navigate = new InputAction("Navigate", InputActionType.Value);
            navigate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            navigate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
            navigate.AddBinding("<XRController>/{Primary2DAxis}/y");
            navigate.AddBinding("<Gamepad>/leftStick/y");

            submit = new InputAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Keyboard>/e");
            submit.AddBinding("<XRController>/{TriggerButton}");
            submit.AddBinding("<XRController>/{PrimaryButton}");
            submit.AddBinding("<Gamepad>/buttonSouth");

            back = new InputAction("Back", InputActionType.Button);
            back.AddBinding("<Keyboard>/escape");
            back.AddBinding("<Keyboard>/backspace");
            back.AddBinding("<XRController>/{SecondaryButton}");
            back.AddBinding("<Gamepad>/buttonEast");
        }

        void OnEnable()
        {
            navigate.Enable();
            submit.Enable();
            back.Enable();
        }

        void OnDisable()
        {
            navigate.Disable();
            submit.Disable();
            back.Disable();
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BuildMainItems();
            BuildSettingsItems();
            current = mainItems;

            foreach (var item in mainItems) item.gameObject.SetActive(false);
            foreach (var item in settingsItems) item.gameObject.SetActive(false);
            if (selectionMarker != null) selectionMarker.gameObject.SetActive(false);

            SetAlpha(title, 0f);
            SetAlpha(subtitle, 0f);
            SetAlpha(prompt, 0f);
            SetAlpha(footer, 0f);
        }

        /// <summary>The scene's entries, with a Settings entry added before Quit (moved one line down).</summary>
        void BuildMainItems()
        {
            if (items.Length != 3)
            {
                mainItems = items;
                return;
            }
            Vector3 spacing = items[1].transform.localPosition - items[0].transform.localPosition;
            MainMenuItem settingsItem = Instantiate(items[2], items[2].transform.parent);
            settingsItem.name = "Item SETTINGS";
            settingsItem.transform.localPosition = items[2].transform.localPosition;
            settingsItem.Label.text = "SETTINGS";
            items[2].transform.localPosition += spacing;
            mainItems = new[] { items[0], items[1], settingsItem, items[2] };
            for (int i = 0; i < mainItems.Length; i++) mainItems[i].Bind(this, i);
        }

        void BuildSettingsItems()
        {
            SettingsData s = GameSettings.Current;
            settings = new List<Setting>
            {
                new Setting { name = "MASTER VOLUME", value = () => Percent(s.masterVolume), change = () => s.masterVolume = Step(s.masterVolume) },
                new Setting { name = "EFFECTS VOLUME", value = () => Percent(s.sfxVolume), change = () => s.sfxVolume = Step(s.sfxVolume) },
                new Setting { name = "MUSIC VOLUME", value = () => Percent(s.musicVolume), change = () => s.musicVolume = Step(s.musicVolume) },
                new Setting { name = "COMFORT VIGNETTE", value = () => s.comfortVignette ? "ON" : "OFF", change = () => s.comfortVignette = !s.comfortVignette },
                new Setting { name = "DOMINANT HAND", value = () => s.leftHanded ? "LEFT" : "RIGHT", change = () => s.leftHanded = !s.leftHanded },
                new Setting { name = "SEATED HEIGHT", value = () => "+" + Mathf.RoundToInt(s.seatedHeightOffset * 100f) + " CM", change = () => s.seatedHeightOffset = s.seatedHeightOffset >= 0.6f ? 0f : s.seatedHeightOffset + 0.1f },
                new Setting { name = "MOUSE SENSITIVITY", value = () => s.mouseSensitivity.ToString("F2"), change = () => s.mouseSensitivity = s.mouseSensitivity >= 0.3f ? 0.04f : s.mouseSensitivity + 0.02f },
                new Setting { name = "BACK", value = () => "", change = null },
            };

            // Rows made from copies of the first entry, closer together so all of them fit.
            Vector3 top = mainItems[0].transform.localPosition + Vector3.up * 50f;
            settingsItems = new MainMenuItem[settings.Count];
            for (int i = 0; i < settings.Count; i++)
            {
                MainMenuItem row = Instantiate(mainItems[0], mainItems[0].transform.parent);
                row.name = "Setting " + settings[i].name;
                row.transform.localPosition = top + Vector3.down * 50f * i;
                row.transform.localScale = Vector3.one * 0.75f;
                row.Bind(this, i);
                settingsItems[i] = row;
            }
            RefreshSettingLabels();
        }

        void RefreshSettingLabels()
        {
            for (int i = 0; i < settings.Count; i++)
            {
                string value = settings[i].value();
                settingsItems[i].Label.text = string.IsNullOrEmpty(value) ? settings[i].name : settings[i].name + "   " + value;
            }
        }

        void Update()
        {
            stageTime += Time.unscaledDeltaTime;
            stickCooldown -= Time.unscaledDeltaTime;

            switch (stage)
            {
                case Stage.Intro:
                    // The title surfaces slowly out of black.
                    float fade = Mathf.Clamp01(stageTime / introFade);
                    SetAlpha(title, fade);
                    SetAlpha(subtitle, Mathf.Clamp01(stageTime / introFade - 0.5f) * 0.7f);
                    if (fade >= 1f) Enter(Stage.PressAny);
                    break;

                case Stage.PressAny:
                    SetAlpha(prompt, 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(stageTime * 2.2f)));
                    if (stageTime > 0.3f && AnyButtonPressed()) OpenMenu();
                    break;

                case Stage.Menu:
                    UpdateMenu();
                    break;
            }
        }

        static bool AnyButtonPressed()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
            foreach (var device in InputSystem.devices)
            {
                if (device is Keyboard || device is Mouse) continue;
                foreach (var control in device.allControls)
                {
                    var button = control as UnityEngine.InputSystem.Controls.ButtonControl;
                    if (button != null && !button.synthetic && button.wasPressedThisFrame) return true;
                }
            }
            return false;
        }

        void OpenMenu()
        {
            Enter(Stage.Menu);
            SetAlpha(prompt, 0f);
            SetAlpha(footer, 0.5f);
            ShowList(mainItems);
            Sfx.PlayUi(SfxKind.UiClick);
        }

        /// <summary>Shows one list (the main entries or the settings) and hides the other.</summary>
        void ShowList(MainMenuItem[] list)
        {
            foreach (var item in mainItems) item.gameObject.SetActive(list == mainItems);
            foreach (var item in settingsItems) item.gameObject.SetActive(list == settingsItems);
            current = list;
            inSettings = list == settingsItems;

            bool hasSave = SaveSystem.Exists;
            enabledItems.Clear();
            for (int i = 0; i < current.Length; i++)
            {
                // Continue (the first main entry) needs a save to continue from.
                bool usable = inSettings || i != ContinueIndex || hasSave;
                current[i].Usable = usable;
                if (usable) enabledItems.Add(i);
            }
            selected = enabledItems.Count > 0 ? enabledItems[0] : 0;
            if (selectionMarker != null)
            {
                selectionMarker.gameObject.SetActive(true);
                selectionMarker.localPosition = current[selected].transform.localPosition;
            }
            Refresh();
        }

        void UpdateMenu()
        {
            float direction = navigate.ReadValue<float>();
            if (Mathf.Abs(direction) < 0.3f) stickCooldown = 0f;
            else if (stickCooldown <= 0f)
            {
                Step(direction > 0f ? -1 : 1);
                stickCooldown = 0.28f;
            }

            if (stageTime > 0.25f && submit.WasPressedThisFrame()) Choose(selected);
            if (inSettings && back.WasPressedThisFrame()) CloseSettings();

            if (selectionMarker != null)
            {
                Vector3 goal = current[selected].transform.localPosition;
                selectionMarker.localPosition = Vector3.Lerp(selectionMarker.localPosition, goal, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            }
        }

        void Step(int delta)
        {
            if (enabledItems.Count == 0) return;
            int position = Mathf.Max(0, enabledItems.IndexOf(selected));
            position = (position + delta + enabledItems.Count) % enabledItems.Count;
            Select(enabledItems[position]);
        }

        /// <summary>Called by the keyboard, the stick, or a menu entry the mouse is hovering over.</summary>
        public void Select(int index)
        {
            if (stage != Stage.Menu || index >= current.Length || !current[index].Usable || index == selected) return;
            selected = index;
            confirmNewGame = false;
            Sfx.PlayUi(SfxKind.UiHover, 0.5f);
            Refresh();
        }

        /// <summary>Called on Enter, the trigger, or a mouse click on an entry.</summary>
        public void Choose(int index)
        {
            if (stage != Stage.Menu || index >= current.Length || !current[index].Usable) return;
            selected = index;
            Sfx.PlayUi(SfxKind.UiClick);

            if (inSettings)
            {
                if (settings[index].change == null)
                {
                    CloseSettings();
                    return;
                }
                settings[index].change();
                GameSettings.Save();
                RefreshSettingLabels();
                Refresh();
                return;
            }

            switch (index)
            {
                case ContinueIndex:
                    Leave();
                    SaveSystem.Load();
                    SceneFlow.Load(SceneFlow.HubScene);
                    break;

                case NewGameIndex: // Ask once before wiping an existing save.
                    if (SaveSystem.Exists && !confirmNewGame)
                    {
                        confirmNewGame = true;
                        Refresh();
                        return;
                    }
                    Leave();
                    SaveSystem.NewGame();
                    SceneFlow.Load(SceneFlow.HubScene);
                    break;

                default:
                    if (mainItems.Length == 4 && index == SettingsIndex)
                    {
                        ShowList(settingsItems);
                        return;
                    }
                    // Quit
                    Leave();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        void CloseSettings()
        {
            Sfx.PlayUi(SfxKind.UiBack);
            ShowList(mainItems);
            Select(Mathf.Min(SettingsIndex, mainItems.Length - 1));
        }

        void Leave() => Enter(Stage.Leaving);

        void Refresh()
        {
            for (int i = 0; i < current.Length; i++)
            {
                Color color = !current[i].Usable ? disabledColor : i == selected ? selectedColor : idleColor;
                current[i].Label.color = color;
                if (!inSettings && i == NewGameIndex) current[i].Label.text = confirmNewGame ? "ERASE EXISTING JOURNEY?" : "NEW GAME";
            }
        }

        void Enter(Stage next)
        {
            stage = next;
            stageTime = 0f;
        }

        static float Step(float value) => value >= 0.999f ? 0f : Mathf.Min(1f, value + 0.1f);

        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        static void SetAlpha(TMP_Text text, float alpha)
        {
            if (text == null) return;
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }
    }
}
