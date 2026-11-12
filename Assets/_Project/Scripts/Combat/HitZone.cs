using UnityEngine;

namespace Roguelike.Combat
{
    public enum HitZoneType { Torso, Head, Limb }

    /// <summary>Which part of the body a collider belongs to, for dismemberment.</summary>
    public enum BodyPart { Torso, Head, LeftArm, RightArm, LeftLeg, RightLeg }

    /// <summary>
    /// Marks a collider on an enemy's body so a hit there can be told apart: head, torso or limb.
    /// The bone this sits on is also where the body comes apart if the part is severed.
    /// </summary>
    public class HitZone : MonoBehaviour
    {
        public HitZoneType zone = HitZoneType.Torso;
        public BodyPart part = BodyPart.Torso;
    }
}
