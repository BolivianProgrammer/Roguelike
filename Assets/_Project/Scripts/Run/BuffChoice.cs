using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Combat;
using Roguelike.Player;

namespace Roguelike.Run
{
    /// <summary>
    /// One of the buffs offered after a totem falls: a floating orb with its name and effect written above it.
    /// Take it by hitting it with a weapon or touching it with a hand; the other orbs then vanish.
    /// Choosing with your hands instead of a menu works the same in the headset and on a monitor.
    /// </summary>
    public class BuffChoice : MonoBehaviour, IDamageable
    {
        static Font font;

        BuffDefinition buff;
        List<BuffChoice> siblings;
        Action<BuffDefinition> onChosen;
        PhysicsHand[] hands;
        Transform label;
        Vector3 basePosition;
        float age;
        bool taken;

        /// <summary>Lays out one orb per buff in an arc around a point, facing the player.</summary>
        public static void Offer(Vector3 center, IList<BuffDefinition> buffs, Action<BuffDefinition> onChosen)
        {
            var cam = Camera.main;
            Vector3 toPlayer = cam != null ? cam.transform.position - center : Vector3.back;
            toPlayer.y = 0f;
            toPlayer = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer);

            var group = new List<BuffChoice>();
            for (int i = 0; i < buffs.Count; i++)
            {
                float offset = (i - (buffs.Count - 1) * 0.5f) * 0.9f;
                Vector3 position = center + toPlayer * 0.9f + side * offset + Vector3.up * 1.15f;
                group.Add(Create(position, buffs[i], group, onChosen));
            }
        }

        static BuffChoice Create(Vector3 position, BuffDefinition buff, List<BuffChoice> group, Action<BuffDefinition> onChosen)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Buff: " + buff.displayName;
            orb.transform.position = position;
            orb.transform.localScale = Vector3.one * 0.28f;

            var shader = Shader.Find("Sprites/Default");
            var orbRenderer = orb.GetComponent<MeshRenderer>();
            if (shader != null) orbRenderer.material = new Material(shader);
            orbRenderer.material.color = buff.color;

            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelGo = new GameObject("Label");
            labelGo.transform.position = position + Vector3.up * 0.38f;
            var mesh = labelGo.AddComponent<TextMesh>();
            mesh.text = buff.displayName + "\n" + buff.description;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.006f;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            var choice = orb.AddComponent<BuffChoice>();
            choice.buff = buff;
            choice.siblings = group;
            choice.onChosen = onChosen;
            choice.label = labelGo.transform;
            choice.basePosition = position;
            return choice;
        }

        void Start()
        {
            hands = FindObjectsOfType<PhysicsHand>();
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position = basePosition + Vector3.up * (Mathf.Sin(age * 2f) * 0.04f);

            var cam = Camera.main;
            if (cam != null && label != null)
                label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);

            // A short grace period so the swing that broke the totem cannot also pick a buff by accident.
            if (age < 0.8f || taken) return;
            foreach (var hand in hands)
                if (hand != null && Vector3.Distance(hand.transform.position, transform.position) < 0.22f)
                    Take();
        }

        public void TakeDamage(DamageInfo info)
        {
            if (age >= 0.8f) Take();
        }

        void Take()
        {
            if (taken) return;
            taken = true;
            onChosen?.Invoke(buff);

            foreach (var other in siblings)
                if (other != null) other.Remove();
        }

        void Remove()
        {
            taken = true;
            if (label != null) Destroy(label.gameObject);
            Destroy(gameObject);
        }
    }
}
