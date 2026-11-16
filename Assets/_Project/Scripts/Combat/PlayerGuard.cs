using UnityEngine;
using Roguelike.Player;
using Roguelike.Weapons;

namespace Roguelike.Combat
{
    /// <summary>
    /// What happens when an enemy blow meets something the player is holding. Blocking is no longer free:
    /// - a parry (meeting the blow with a fast swing) stops it completely, as before;
    /// - a plain block costs stamina in proportion to the blow, and lets part of it through ("chip"), less with a
    ///   sturdy weapon (<see cref="WeaponDefinition.blockStability"/>) and less again with both hands on it;
    /// - blocking without the stamina to pay for it, or blocking a guard-breaking attack (the glowing heavy swings),
    ///   breaks the guard: the weapon is knocked aside, more of the blow lands, and for a moment nothing the player
    ///   holds can block, so the next blow lands in full.
    /// One place for these rules, used by enemy weapons and by bolts alike.
    /// </summary>
    public static class PlayerGuard
    {
        public enum Outcome { Parried, Blocked, GuardBroken }

        /// <summary>Stamina a block costs per point of damage it stops.</summary>
        public const float StaminaPerDamage = 1.1f;
        /// <summary>Guard-breaking attacks cost this many times more to hold off.</summary>
        public const float GuardBreakerCost = 2f;
        /// <summary>Seconds the guard stays open after it breaks.</summary>
        public const float BreakDuration = 1.1f;
        /// <summary>Share of the blow that lands when a guard-breaking attack is blocked.</summary>
        public const float BrokenThrough = 0.5f;
        /// <summary>Stability of a held item that is not a weapon (a bottle, a shard...).</summary>
        const float ItemStability = 0.4f;
        /// <summary>Extra stability with both hands on the weapon.</summary>
        const float TwoHandBonus = 0.1f;

        static float brokenUntil = -10f;

        /// <summary>True while the guard is broken: held items cannot block.</summary>
        public static bool Broken => Time.time < brokenUntil;

        /// <summary>True if this held item can block right now (not while the guard is broken).</summary>
        public static bool CanBlockWith(Grabbable item) => item != null && item.IsHeld && !Broken;

        /// <summary>
        /// Settles a blow of the given damage that met a held item. Applies the stamina cost, the chip damage and a
        /// guard break, and gives the feedback for them. Returns what happened, so the attacker can react.
        /// </summary>
        public static Outcome Resolve(Grabbable item, float damage, DamageType type, bool parried, bool guardBreaker,
            Vector3 point, Vector3 direction, GameObject source)
        {
            if (parried) return Outcome.Parried;

            float stability = Stability(item);
            float cost = damage * StaminaPerDamage * (guardBreaker ? GuardBreakerCost : 1f);
            bool paid = PlayerStamina.TryUse(cost);

            float through;
            Outcome outcome;
            if (guardBreaker || !paid)
            {
                outcome = Outcome.GuardBroken;
                through = damage * (guardBreaker ? BrokenThrough : Mathf.Min(1f, (1f - stability) * 2f));
                Break(item, direction, point);
            }
            else
            {
                outcome = Outcome.Blocked;
                through = damage * (1f - stability);
            }

            var player = PlayerHealth.Instance;
            if (player != null && through >= 0.5f)
                player.TakeDamage(new DamageInfo
                {
                    Amount = through,
                    Type = type,
                    Point = point,
                    Direction = direction,
                    Source = source,
                    // A blow that broke the guard hurts like a hit; a held block only stings.
                    Blocked = outcome == Outcome.Blocked,
                });

            CombatLog.Log("Guard: " + outcome + " vs " + damage.ToString("F0") + " dmg (stability " + stability.ToString("F2")
                + ", stamina cost " + cost.ToString("F0") + (paid ? "" : " UNPAID") + ", " + through.ToString("F1") + " through)");
            return outcome;
        }

        /// <summary>How much of a blow the held item stops (0-1).</summary>
        public static float Stability(Grabbable item)
        {
            if (item == null) return 0f;
            var weapon = item.GetComponent<Weapon>();
            float stability = weapon != null && weapon.Definition != null ? weapon.Definition.blockStability : ItemStability;
            if (item.Holders.Count >= 2) stability += TwoHandBonus;
            return Mathf.Clamp01(stability);
        }

        /// <summary>Opens the guard: the weapon is knocked aside and nothing can block for a moment.</summary>
        public static void Break(Grabbable item, Vector3 direction, Vector3 point)
        {
            brokenUntil = Time.time + BreakDuration;
            PlayerStamina.DelayRegen(BreakDuration);

            if (item != null)
            {
                foreach (var holder in item.Holders) holder.Hand.Haptic(1f, 0.3f);
                // A shove the hand has to fight back against: the blade visibly swings out of the way.
                if (item.Body != null) item.Body.AddForceAtPosition(direction.normalized * 4f + Vector3.down * 2f, point, ForceMode.VelocityChange);
            }
            DamagePopup.Spawn(point, "GUARD BROKEN", new Color(1f, 0.45f, 0.2f));
            Audio.Sfx.Play(Audio.SfxKind.Block, point, 1f, 0.7f);
            HitSpark.Spawn(point, -direction, new Color(1f, 0.5f, 0.2f), 30, 4.5f);
        }
    }
}
