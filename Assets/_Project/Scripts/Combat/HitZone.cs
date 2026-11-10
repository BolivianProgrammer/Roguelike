using UnityEngine;

namespace Roguelike.Combat
{
    public enum HitZoneType { Torso, Head, Limb }

    /// <summary>
    /// Marks a collider on an enemy's body so a hit there can be told apart: head, torso or limb.
    /// Later this is also where dismemberment decides which limb comes off.
    /// </summary>
    public class HitZone : MonoBehaviour
    {
        public HitZoneType zone = HitZoneType.Torso;
    }
}
