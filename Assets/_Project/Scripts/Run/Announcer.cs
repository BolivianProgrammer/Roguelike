using UnityEngine;

namespace Roguelike.Run
{
    /// <summary>
    /// Big messages for the player ("Totem destroyed", "You died"): floating text a couple of meters ahead
    /// in the world, which works in the headset and on a monitor alike.
    /// </summary>
    public class Announcer : MonoBehaviour
    {
        static Announcer current;
        static Font font;

        TextMesh text;
        float endTime;
        float duration;
        Color color;

        /// <summary>Shows a message in front of the player for a few seconds, replacing any message still showing.</summary>
        public static void Show(string message, Color color, float seconds = 3.5f)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (current != null) Destroy(current.gameObject);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("Announcement");
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = message;
            mesh.font = font;
            mesh.fontSize = 96;
            mesh.characterSize = 0.012f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            current = go.AddComponent<Announcer>();
            current.text = mesh;
            current.color = color;
            current.duration = seconds;
            current.endTime = Time.unscaledTime + seconds;
            current.Follow(cam, 1f);
        }

        public static void Show(string message) => Show(message, Color.white);

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Drift after the player's view instead of being glued to it, which is uncomfortable in a headset.
            Follow(cam, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));

            float remaining = endTime - Time.unscaledTime;
            var faded = color;
            faded.a = Mathf.Clamp01(remaining / 0.6f);
            text.color = faded;
            if (remaining <= 0f) Destroy(gameObject);
        }

        void Follow(Camera cam, float blend)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = cam.transform.up;
            forward.Normalize();

            Vector3 goal = cam.transform.position + forward * 2.2f + Vector3.up * 0.25f;
            transform.position = Vector3.Lerp(transform.position, goal, blend);
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
