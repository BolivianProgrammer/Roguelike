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
            SetColor(idleColor);
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
                if (progress > 0f) Announcer.Show("Extraction cancelled", new Color(1f, 0.6f, 0.3f), 1.5f);
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
                Announcer.Show("Portal opening... " + secondsLeft, openColor, 1.2f);
            }

            if (progress >= needed)
            {
                used = true;
                Extracted?.Invoke();
            }
        }

        void SetColor(Color color)
        {
            if (ring == null) return;
            ring.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            ring.SetPropertyBlock(propertyBlock);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
