using UnityEngine;
using Roguelike.Gear;

namespace Roguelike.Player
{
    /// <summary>
    /// Makes the player's hands show the armor they wear: the hand shapes take the armor's colour, and a cuff
    /// appears at each wrist. A stand-in until real gloved hand models with grip poses exist.
    /// </summary>
    public class Gauntlets : MonoBehaviour
    {
        static readonly Color BareHands = new Color(0.75f, 0.72f, 0.68f);

        readonly System.Collections.Generic.List<Renderer> handRenderers = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<Renderer> cuffs = new System.Collections.Generic.List<Renderer>();
        MaterialPropertyBlock block;
        string shownArmor;

        void Start()
        {
            block = new MaterialPropertyBlock();
            foreach (var hand in FindObjectsOfType<PhysicsHand>())
            {
                foreach (var r in hand.GetComponentsInChildren<MeshRenderer>())
                    if (r.GetComponentInParent<Weapons.Grabbable>() == null && r.GetComponent<TextMesh>() == null) handRenderers.Add(r);

                var cuff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cuff.name = "Gauntlet Cuff";
                Destroy(cuff.GetComponent<Collider>());
                cuff.transform.SetParent(hand.transform, false);
                cuff.transform.localPosition = new Vector3(0f, 0f, -0.075f);
                cuff.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cuff.transform.localScale = new Vector3(0.075f, 0.035f, 0.075f);
                var cuffRenderer = cuff.GetComponent<MeshRenderer>();
                if (handRenderers.Count > 0) cuffRenderer.sharedMaterial = handRenderers[0].sharedMaterial;
                cuffs.Add(cuffRenderer);
            }
            Refresh();
        }

        void Update()
        {
            // Cheap check: the worn armor only changes in the hub's armorer menu or on death.
            string worn = Meta.SaveSystem.Data.armor;
            if (worn != shownArmor) Refresh();
        }

        void Refresh()
        {
            shownArmor = Meta.SaveSystem.Data.armor;
            ArmorDefinition armor = Equipment.Armor;
            Color color = armor != null ? armor.color : BareHands;
            foreach (var r in handRenderers) Paint(r, color);
            foreach (var r in cuffs)
            {
                r.enabled = armor != null;
                Paint(r, color * 0.8f);
            }
        }

        void Paint(Renderer target, Color color)
        {
            if (target == null) return;
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            target.SetPropertyBlock(block);
        }
    }
}
