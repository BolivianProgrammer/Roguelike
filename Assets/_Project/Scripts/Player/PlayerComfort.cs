using UnityEngine;
using UnityEngine.UI;
using Roguelike.Meta;

namespace Roguelike.Player
{
    /// <summary>
    /// Comfort options for the headset: a vignette that narrows the view while moving with the stick (it eases
    /// motion sickness), and a height boost for players sitting down. Both come from the settings and update
    /// as soon as they change. Does nothing in desktop play.
    /// </summary>
    public class PlayerComfort : MonoBehaviour
    {
        Image vignette;
        Transform floorOffset;
        float baseFloorHeight;
        float strength;

        void Start()
        {
            if (DesktopRig.IsDesktop)
            {
                enabled = false;
                return;
            }

            var cam = Camera.main;
            if (cam != null) CreateVignette(cam.transform);

            // The XR Origin's camera offset object holds the floor height; raise it for seated players.
            var origin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null && origin.CameraFloorOffsetObject != null)
            {
                floorOffset = origin.CameraFloorOffsetObject.transform;
                baseFloorHeight = floorOffset.localPosition.y;
            }
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDestroy() => GameSettings.Changed -= Apply;

        void Apply()
        {
            if (floorOffset == null) return;
            Vector3 position = floorOffset.localPosition;
            position.y = baseFloorHeight + GameSettings.Current.seatedHeightOffset;
            floorOffset.localPosition = position;
        }

        void Update()
        {
            if (vignette == null) return;
            // Only stick movement (the whole rig sliding along), never the player's own head movement.
            float moving = GameSettings.Current.comfortVignette ? Mathf.Clamp01(PlayerBody.RigVelocity.magnitude / 2f) : 0f;
            strength = Mathf.MoveTowards(strength, moving, Time.deltaTime * 3f);
            vignette.color = new Color(0f, 0f, 0f, strength * 0.85f);
            vignette.enabled = strength > 0.01f;
        }

        void CreateVignette(Transform head)
        {
            var go = new GameObject("Comfort Vignette", typeof(Canvas));
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;
            var rect = (RectTransform)go.transform;
            rect.SetParent(head, false);
            rect.sizeDelta = new Vector2(1000f, 1000f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0f, 0f, 0.3f);

            var imageGo = new GameObject("Ring", typeof(Image));
            var imageRect = (RectTransform)imageGo.transform;
            imageRect.SetParent(rect, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            vignette = imageGo.GetComponent<Image>();
            vignette.raycastTarget = false;
            vignette.sprite = RingSprite();
            vignette.enabled = false;
        }

        /// <summary>A soft dark ring: clear in the middle, opaque at the edges.</summary>
        static Sprite RingSprite()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)(255 * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, r)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
