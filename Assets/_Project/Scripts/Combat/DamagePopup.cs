using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Roguelike.UI;

namespace Roguelike.Combat
{
    /// <summary>
    /// Hit markers: a damage number or a word (PARRY, BLOCK, EXECUTION...) that punches in at the point of impact,
    /// drifts up and aside, and fades. Styled by the UI theme (heavy outline + shadow) and scaled with distance so
    /// it reads the same up close and across a clearing. Popups are pooled: nothing is created or destroyed per hit,
    /// and numbers are written without allocating strings.
    /// </summary>
    public class DamagePopup : MonoBehaviour
    {
        const float Lifetime = 0.85f;
        const float PunchTime = 0.12f;
        const float Rise = 0.45f;
        /// <summary>Font size at the reference distance; farther away it grows so its angular size stays similar.</summary>
        const float NumberSize = 1.1f;
        const float WordSize = 0.95f;
        const float ReferenceDistance = 2f;
        const int MaxPooled = 24;

        static readonly Stack<DamagePopup> free = new Stack<DamagePopup>();

        TextMeshPro text;
        Color color;
        Vector3 origin;
        Vector3 drift;
        float age;

        /// <summary>A damage number coloured by hit type (slash, stab, blunt); heavy hits are bigger and hotter.</summary>
        public static void Spawn(Vector3 position, float amount, DamageType type)
        {
            var theme = UiTheme.Instance;
            Color color = type == DamageType.Stab ? theme.stab : type == DamageType.Blunt ? theme.blunt : theme.slash;
            // 0 for a light hit, 1 for a heavy one: grows the number and warms its colour.
            float weight = Mathf.InverseLerp(theme.heavyHit * 0.35f, theme.heavyHit, amount);
            color = Color.Lerp(color, theme.heavy, weight * 0.6f);

            DamagePopup popup = Take(position);
            popup.text.characterSpacing = 0f;
            popup.text.fontStyle = FontStyles.Bold;
            popup.text.SetText("{0}", Mathf.Max(1, Mathf.RoundToInt(amount)));
            popup.Begin(color, NumberSize * Mathf.Lerp(0.85f, 1.35f, weight));
        }

        /// <summary>A word marker (PARRY, BLOCK, GUARD BROKEN...).</summary>
        public static void Spawn(Vector3 position, string label, Color color)
        {
            DamagePopup popup = Take(position);
            popup.text.characterSpacing = 8f;
            popup.text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            popup.text.SetText(label);
            popup.Begin(color, WordSize);
        }

        static DamagePopup Take(Vector3 position)
        {
            DamagePopup popup = null;
            // Popups left over from an earlier scene were destroyed with it.
            while (free.Count > 0 && popup == null) popup = free.Pop();
            if (popup == null) popup = Create();
            popup.origin = position;
            popup.transform.position = position;
            return popup;
        }

        static DamagePopup Create()
        {
            var go = new GameObject("Hit Marker");
            var popup = go.AddComponent<DamagePopup>();
            popup.text = GameText.World(go.transform, "Text", TextStyle.Bold, NumberSize);
            popup.text.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
            return popup;
        }

        void Begin(Color tint, float fontSize)
        {
            color = tint;
            age = 0f;
            // A little sideways scatter, so a flurry of hits fans out instead of stacking into one smudge.
            Vector2 aside = Random.insideUnitCircle * 0.12f;
            drift = new Vector3(aside.x, 0f, aside.y);
            gameObject.SetActive(true);
            // Lay the text out once; the animation below only moves, scales and recolours the finished mesh.
            text.fontSize = fontSize;
            text.color = Color.white;
            text.ForceMeshUpdate();
            Animate();
        }

        /// <summary>Recolours the built mesh in place: far cheaper than setting TMP's colour, which rebuilds it.</summary>
        void Tint(Color32 c)
        {
            TMP_TextInfo info = text.textInfo;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                Color32[] colors = info.meshInfo[m].colors32;
                if (colors == null) continue;
                for (int i = 0; i < colors.Length; i++) colors[i] = c;
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            if (age >= Lifetime)
            {
                gameObject.SetActive(false);
                if (free.Count < MaxPooled) free.Push(this);
                else Destroy(gameObject);
                return;
            }
            Animate();
        }

        void Animate()
        {
            float t = age / Lifetime;
            // Ease-out rise: quick at first, settling as it fades.
            float rise = 1f - (1f - t) * (1f - t);
            transform.position = origin + (Vector3.up * Rise + drift) * rise;

            var cam = Camera.main;
            float distance = ReferenceDistance;
            if (cam != null)
            {
                Vector3 toText = transform.position - cam.transform.position;
                distance = toText.magnitude;
                if (distance > 0.01f) transform.rotation = Quaternion.LookRotation(toText, cam.transform.up);
            }

            // Punch: starts 1.6x, snaps to full size in PunchTime.
            float punch = age < PunchTime ? Mathf.Lerp(1.6f, 1f, age / PunchTime) : 1f;
            float scale = Mathf.Clamp(distance / ReferenceDistance, 0.75f, 3f);
            transform.localScale = Vector3.one * (punch * scale);

            // Flash white for the first frames of the punch, hold the colour, then fade over the last 40%.
            Color c = age < PunchTime * 0.5f ? Color.Lerp(Color.white, color, age / (PunchTime * 0.5f)) : color;
            c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            Tint(c);
        }
    }
}
