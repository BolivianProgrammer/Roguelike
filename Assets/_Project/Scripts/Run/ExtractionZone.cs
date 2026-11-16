using System;
using UnityEngine;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// The way out. Stand inside the circle to open the portal; stay until it is open and you leave with
    /// everything you are carrying. Stepping out resets it, so leaving under attack is a real decision.
    /// </summary>
    public class ExtractionZone : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] float radius = 1.5f;
        [Tooltip("Seconds the player must stand in the zone for the portal to open.")]
        [SerializeField] float openTime = 6f;
        [Tooltip("Seconds it takes once the level is complete.")]
        [SerializeField] float openTimeAfterVictory = 2f;
        [Tooltip("Shown with a countdown while the player stands in the zone.")]
        [SerializeField] string openingText = "Portal opening...";
        [Tooltip("Shown if the player steps out early.")]
        [SerializeField] string cancelledText = "Extraction cancelled";
        [SerializeField] Renderer ring;
        [SerializeField] Color idleColor = new Color(0.1f, 0.5f, 0.6f);
        [SerializeField] Color openColor = new Color(0.3f, 1f, 0.9f);

        MaterialPropertyBlock propertyBlock;
        PlayerHealth player;
        float progress;
        int lastSecond = -1;
        bool used;

        /// <summary>Set once the boss is dead: the portal then opens quickly.</summary>
        public bool Fast { get; set; }

        /// <summary>Raised when the player has stood in the zone long enough and leaves the mission.</summary>
        public event Action Extracted;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (ring == null) ring = GetComponent<Renderer>();
            SetColor(idleColor);
        }

        /// <summary>Sets the zone up from code, for portals made at runtime.</summary>
        public void Configure(float zoneRadius, float secondsToOpen, string opening, string cancelled)
        {
            radius = zoneRadius;
            openTime = secondsToOpen;
            openingText = opening;
            cancelledText = cancelled;
        }

        void Start()
        {
            player = FindObjectOfType<PlayerHealth>();
        }

        void Update()
        {
            if (used || player == null || player.IsDead) return;

            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            bool inside = offset.magnitude <= radius;
            float needed = Fast ? openTimeAfterVictory : openTime;

            if (!inside)
            {
                if (progress > 0f) Announcer.Show(cancelledText, new Color(1f, 0.6f, 0.3f), 1.5f);
                progress = 0f;
                lastSecond = -1;
                SetColor(idleColor);
                return;
            }

            progress += Time.deltaTime;
            SetColor(Color.Lerp(idleColor, openColor, progress / needed));

            int secondsLeft = Mathf.CeilToInt(needed - progress);
            if (secondsLeft != lastSecond && secondsLeft > 0)
            {
                lastSecond = secondsLeft;
                Announcer.Show(openingText + " " + secondsLeft, openColor, 1.2f);
            }

            // A shimmer rises from the ring while it opens.
            if (UnityEngine.Random.value < Time.deltaTime * 12f)
                Combat.HitSpark.Spawn(transform.position + UnityEngine.Random.insideUnitSphere * radius * 0.8f + Vector3.up * 0.1f, Vector3.up, openColor, 2, 1.5f, 0.04f);

            if (progress >= needed)
            {
                used = true;
                Combat.HitSpark.Ring(transform.position, openColor, radius, 50);
                Audio.Sfx.Play(Audio.SfxKind.PortalOpen, transform.position + Vector3.up);
                Extracted?.Invoke();
            }
        }

        void SetColor(Color color)
        {
            if (ring == null) return;
            ring.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor("_Color", color);
            ring.SetPropertyBlock(propertyBlock);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
