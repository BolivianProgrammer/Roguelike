using UnityEngine;
using Roguelike.Audio;
using Roguelike.Combat;
using Roguelike.Meta;
using Roguelike.UI;

namespace Roguelike.Run
{
    /// <summary>
    /// A hidden chest. Walk up and press the use button to open it: essence and coins spill out (and fly to the
    /// player), and sometimes a weapon, armor or trinket is inside. Like everything found on a run, it only stays
    /// the player's if they make it out alive. Place it in a mission scene somewhere worth exploring for; a lid
    /// and box are made automatically if it has no model.
    /// </summary>
    public class TreasureChest : MonoBehaviour
    {
        [SerializeField] int essence = 40;
        [SerializeField] int coins = 50;
        [Tooltip("Chance (0-1) of an item inside: anything in the item database marked as chest loot.")]
        [SerializeField, Range(0f, 1f)] float itemChance = 0.5f;
        [Tooltip("The part that swings open. Made automatically if empty.")]
        [SerializeField] Transform lid;

        bool opened;
        float openAmount = -1f;
        InteractPrompt prompt;

        void Start()
        {
            if (lid == null) BuildGraybox();
            prompt = InteractPrompt.Add(gameObject, "Open the chest", 2f, 1.1f);
            prompt.Used += Open;
        }

        void BuildGraybox()
        {
            var material = FlatMaterials.Create(new Color(0.35f, 0.22f, 0.1f));
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Chest Box";
            box.transform.SetParent(transform, false);
            box.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            box.transform.localScale = new Vector3(0.9f, 0.5f, 0.55f);
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            var hinge = new GameObject("Lid Hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(0f, 0.5f, -0.275f);
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Chest Lid";
            Destroy(top.GetComponent<Collider>());
            top.transform.SetParent(hinge, false);
            top.transform.localPosition = new Vector3(0f, 0.06f, 0.275f);
            top.transform.localScale = new Vector3(0.92f, 0.12f, 0.57f);
            top.GetComponent<MeshRenderer>().sharedMaterial = FlatMaterials.Create(new Color(0.55f, 0.4f, 0.15f));
            lid = hinge;
        }

        void Open()
        {
            if (opened) return;
            opened = true;
            openAmount = 0f;
            prompt.Set("Empty", false);
            Sfx.Play(SfxKind.ChestOpen, transform.position);
            HitSpark.Spawn(transform.position + Vector3.up * 0.6f, Vector3.up, new Color(1f, 0.85f, 0.4f), 30, 3f, 0.04f);

            Vector3 top = transform.position + Vector3.up * 0.8f;
            LootShard.Burst(top, essence, coins);

            if (Random.value > itemChance) return;
            var loot = ItemDatabase.All(item => item.chestLoot);
            if (loot.Count == 0) return;
            var found = loot[Random.Range(0, loot.Count)];
            if (found.kind == ItemKind.Weapon && found.prefab != null)
            {
                Instantiate(found.prefab, top + Vector3.up * 0.3f, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f));
                Announcer.Show("Found a " + found.displayName + "\nCarry it out to keep it", new Color(1f, 0.85f, 0.4f), 3f);
            }
            else
            {
                MissionManager.FoundGear.Add(found.id);
                Announcer.Show("Found " + found.displayName + "\nReach the portal to keep it", new Color(1f, 0.85f, 0.4f), 3f);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.3f, new Vector3(0.9f, 0.6f, 0.55f));
        }

        void Update()
        {
            if (openAmount < 0f || openAmount >= 1f || lid == null) return;
            openAmount = Mathf.Min(1f, openAmount + Time.deltaTime * 2f);
            lid.localRotation = Quaternion.Euler(-110f * Mathf.SmoothStep(0f, 1f, openAmount), 0f, 0f);
        }
    }
}
