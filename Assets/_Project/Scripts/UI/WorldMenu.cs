using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Roguelike.Player;

namespace Roguelike.UI
{
    /// <summary>
    /// A menu panel that floats in the world, such as a vendor's wares. You pick a line by pointing at it:
    /// in the headset a beam comes out of each hand and the trigger selects; on a monitor the mouse cursor
    /// is freed and a click selects. Only one menu is open at a time.
    /// The panel and its rows are prefabs (Prefabs/UI, made by Roguelike > Build UI Prefabs) coloured by the
    /// <see cref="UiTheme"/>; this class only fills them in and handles pointing.
    /// </summary>
    public class WorldMenu : MonoBehaviour
    {
        /// <summary>One selectable line of a menu.</summary>
        public class Entry
        {
            public string Label;
            /// <summary>Shown at the right of the line, e.g. a price.</summary>
            public string Detail;
            /// <summary>False greys the line out, e.g. when the player cannot afford it.</summary>
            public bool Enabled = true;
            public Action Action;
        }

        static WorldMenu current;

        class Row
        {
            public Entry Entry;
            public RectTransform Rect;
            public MenuRowView View;
        }

        string title;
        Func<string> header;
        Func<List<Entry>> buildEntries;
        Transform owner;
        float closeDistance;
        readonly List<Row> rows = new List<Row>();
        Row hovered;
        PhysicsHand[] hands;
        LineRenderer[] beams;
        InputAction leftSelect, rightSelect;
        float openedAt;
        UiTheme theme;
        MenuPanelView view;

        /// <summary>True while any menu is open. Other controls (attacks, weapon specials) stand down meanwhile.</summary>
        public static bool IsOpen => current != null;

        /// <summary>Raised when the open menu closes, for whatever reason.</summary>
        public event Action Closed;

        /// <summary>
        /// Opens a menu beside something (an NPC), facing the player; with no owner, straight ahead of the player.
        /// The header and entries are asked for again after every selection, so prices and totals stay up to date.
        /// It closes itself if the player walks away from its owner.
        /// </summary>
        public static WorldMenu Open(string title, Func<string> header, Func<List<Entry>> entries, Transform owner, float closeDistance = 4f)
        {
            Close();

            var go = new GameObject("World Menu - " + title);
            current = go.AddComponent<WorldMenu>();
            current.title = title;
            current.header = header;
            current.buildEntries = entries;
            current.owner = owner;
            current.closeDistance = closeDistance;
            current.Place();
            current.Rebuild();
            Audio.Sfx.PlayUi(Audio.SfxKind.UiClick, 0.6f);
            return current;
        }

        public static void Close()
        {
            if (current == null) return;
            WorldMenu closing = current;
            current = null;
            DesktopRig.InputBlocked = false;
            closing.Closed?.Invoke();
            Destroy(closing.gameObject);
        }

        void Awake()
        {
            openedAt = Time.unscaledTime;
            theme = UiTheme.Instance;
            hands = FindObjectsOfType<PhysicsHand>();

            leftSelect = new InputAction("Menu Select Left", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
            rightSelect = new InputAction("Menu Select Right", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            leftSelect.Enable();
            rightSelect.Enable();

            // On a monitor the mouse becomes a pointer while the menu is up.
            if (DesktopRig.IsDesktop) DesktopRig.InputBlocked = true;
            else CreateBeams();

            if (theme.menuPanel != null)
            {
                view = Instantiate(theme.menuPanel, transform).GetComponent<MenuPanelView>();
                view.transform.localPosition = Vector3.zero;
                view.transform.localRotation = Quaternion.identity;
                view.transform.localScale = Vector3.one * theme.pixelSize;
                var canvas = view.GetComponent<Canvas>();
                if (canvas != null) canvas.worldCamera = Camera.main;
            }
            else Debug.LogError("[WorldMenu] No menu prefab in the UI theme. Run Roguelike > Build UI Prefabs.");
        }

        void OnDestroy()
        {
            leftSelect?.Disable();
            rightSelect?.Disable();
            if (beams != null)
                foreach (var beam in beams)
                    if (beam != null) Destroy(beam.gameObject);
            if (current == this)
            {
                current = null;
                DesktopRig.InputBlocked = false;
            }
        }

        /// <summary>Beside the owner, on the player's side, turned to face the player. With no owner, straight ahead.</summary>
        void Place()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 position;
            if (owner == null)
            {
                Vector3 ahead = cam.transform.forward;
                ahead.y = 0f;
                ahead = ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward;
                position = cam.transform.position + ahead * 1.3f + Vector3.down * 0.1f;
            }
            else
            {
                Vector3 toPlayer = cam.transform.position - owner.position;
                toPlayer.y = 0f;
                toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.back;
                Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
                position = owner.position + toPlayer * 0.75f + side * 0.75f;
                position.y = cam.transform.position.y - 0.05f;
            }
            transform.position = position;
            // The canvas faces -Z of its transform when read, so look away from the player.
            transform.rotation = Quaternion.LookRotation(position - cam.transform.position, Vector3.up);
        }

        // ---------------------------------------------------------------- Building

        void Rebuild()
        {
            if (view == null) return;
            foreach (var row in rows) Destroy(row.View.gameObject);
            rows.Clear();
            hovered = null;

            List<Entry> entries = buildEntries != null ? buildEntries() : new List<Entry>();
            string headerText = header != null ? header() : "";

            view.title.text = title;
            view.header.text = headerText;
            view.header.gameObject.SetActive(!string.IsNullOrEmpty(headerText));
            view.footer.text = DesktopRig.IsDesktop ? "Click a line to choose  -  walk away to close" : "Point and pull the trigger  -  walk away to close";

            foreach (Entry entry in entries)
            {
                var rowView = Instantiate(theme.menuRow, view.rows).GetComponent<MenuRowView>();
                rowView.label.text = entry.Label;
                rowView.detail.text = entry.Detail ?? "";
                rowView.detail.gameObject.SetActive(!string.IsNullOrEmpty(entry.Detail));
                rowView.Paint(theme, entry.Enabled, false);
                rows.Add(new Row { Entry = entry, Rect = rowView.rect, View = rowView });
            }

            // The panel grows to fit its rows.
            var content = (RectTransform)view.rows.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            view.root.sizeDelta = new Vector2(view.root.sizeDelta.x, content.rect.height);
        }

        // ---------------------------------------------------------------- Pointing

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Walking away puts the menu down.
            if (owner != null)
            {
                Vector3 offset = cam.transform.position - owner.position;
                offset.y = 0f;
                if (offset.magnitude > closeDistance)
                {
                    Close();
                    return;
                }
            }

            Row pointed = null;
            bool select = false;

            if (DesktopRig.IsDesktop)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    pointed = RowUnder(cam.ScreenPointToRay(mouse.position.ReadValue()), out _);
                    select = mouse.leftButton.wasPressedThisFrame;
                }
            }
            else
            {
                for (int i = 0; i < hands.Length; i++)
                {
                    PhysicsHand hand = hands[i];
                    if (hand == null) continue;

                    // Point with the real controller: it keeps moving even while the game is paused.
                    Transform pointer = hand.Target != null ? hand.Target : hand.transform;
                    var ray = new Ray(pointer.position, pointer.forward);
                    Row row = RowUnder(ray, out float distance);
                    DrawBeam(i, ray, row != null ? distance : 1.5f, row != null);
                    if (row == null) continue;

                    pointed = row;
                    InputAction trigger = hand.HandNode == UnityEngine.XR.XRNode.LeftHand ? leftSelect : rightSelect;
                    if (trigger.WasPressedThisFrame())
                    {
                        select = true;
                        hand.Haptic(0.5f, 0.05f);
                        break;
                    }
                }
            }

            if (pointed != hovered)
            {
                if (hovered != null) hovered.View.Paint(theme, hovered.Entry.Enabled, false);
                hovered = pointed;
                if (hovered != null)
                {
                    hovered.View.Paint(theme, hovered.Entry.Enabled, hovered.Entry.Enabled);
                    if (hovered.Entry.Enabled) Audio.Sfx.PlayUi(Audio.SfxKind.UiHover, 0.4f);
                }
            }

            // A short pause after opening, so the press that opened the menu cannot also pick from it.
            if (!select || hovered == null || !hovered.Entry.Enabled || Time.unscaledTime - openedAt < 0.3f) return;

            Action action = hovered.Entry.Action;
            Audio.Sfx.PlayUi(Audio.SfxKind.UiClick);
            action?.Invoke();
            // The action may have closed the menu; if not, show the new totals and prices.
            if (current == this) Rebuild();
        }

        /// <summary>The row a ray points at: where it crosses the panel's plane, inside which row's rectangle.</summary>
        Row RowUnder(Ray ray, out float distance)
        {
            distance = 0f;
            if (view == null) return null;
            var plane = new Plane(view.transform.forward, view.transform.position);
            if (!plane.Raycast(ray, out distance) || distance > 8f) return null;
            Vector3 point = ray.GetPoint(distance);
            foreach (Row row in rows)
            {
                Vector3 local = row.Rect.InverseTransformPoint(point);
                if (row.Rect.rect.Contains(new Vector2(local.x, local.y))) return row;
            }
            return null;
        }

        void CreateBeams()
        {
            beams = new LineRenderer[hands.Length];
            for (int i = 0; i < hands.Length; i++)
            {
                var go = new GameObject("Menu Pointer Beam");
                var beam = go.AddComponent<LineRenderer>();
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = 0.006f;
                beam.endWidth = 0.002f;
                beam.sharedMaterial = Combat.FlatMaterials.Shared;
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beams[i] = beam;
            }
        }

        void DrawBeam(int index, Ray ray, float length, bool onRow)
        {
            if (beams == null || index >= beams.Length || beams[index] == null) return;
            LineRenderer beam = beams[index];
            Color color = onRow ? theme.hover : new Color(1f, 1f, 1f, 0.25f);
            beam.startColor = color;
            beam.endColor = color;
            beam.SetPosition(0, ray.origin);
            beam.SetPosition(1, ray.origin + ray.direction * length);
        }
    }
}
