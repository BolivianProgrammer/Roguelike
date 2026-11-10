using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>A floating damage number that rises, faces the player and fades out.</summary>
    public class DamagePopup : MonoBehaviour
    {
        const float Lifetime = 0.9f;
        const float RiseSpeed = 0.4f;

        static Font font;

        TextMesh text;
        Color startColor;
        float age;

        /// <summary>Shows a damage number coloured by hit type: orange slash, cyan stab, grey blunt.</summary>
        public static void Spawn(Vector3 position, float amount, DamageType type)
        {
            Color color;
            switch (type)
            {
                case DamageType.Stab: color = new Color(0.3f, 0.9f, 1f); break;
                case DamageType.Blunt: color = new Color(0.8f, 0.8f, 0.8f); break;
                default: color = new Color(1f, 0.55f, 0.2f); break;
            }
            Spawn(position, Mathf.RoundToInt(amount).ToString(), color);
        }

        public static void Spawn(Vector3 position, string label, Color color)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("Damage Popup");
            go.transform.position = position;

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.01f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            go.AddComponent<DamagePopup>();
        }

        void Awake()
        {
            text = GetComponent<TextMesh>();
            startColor = text.color;
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            var color = startColor;
            color.a = 1f - age / Lifetime;
            text.color = color;

            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
