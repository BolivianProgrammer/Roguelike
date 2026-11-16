namespace Roguelike.Audio
{
    /// <summary>
    /// The sound mix: for every kind of sound, how loud it sits, how many may play at once, how soon it may repeat,
    /// how far away it is still heard, and how important it is when the mix is full. One table, so balancing the
    /// game's audio means editing these lines rather than hunting through gameplay code.
    ///
    /// Principles: what the player must react to (being hit, a block or parry, an enemy winding up) is important
    /// and clear; what merely confirms an action (a swing whoosh, a footstep, a coin) is quiet, short-ranged and the
    /// first to give way; big story moments duck everything else.
    /// </summary>
    public static class SfxMix
    {
        /// <summary>Sounds this important are never ducked by others.</summary>
        public const int DuckImmune = 9;

        public struct Rule
        {
            /// <summary>Level in the mix (multiplies the caller's volume and the recording's).</summary>
            public float volume;
            /// <summary>How many of this kind may play at once.</summary>
            public int maxVoices;
            /// <summary>Seconds that must pass before this kind plays again.</summary>
            public float minGap;
            /// <summary>Beyond this distance (meters) it is not played at all; it also fades out towards it.</summary>
            public float range;
            /// <summary>0 = the first to give way when the mix is full, 10 = never gives way.</summary>
            public int priority;
            /// <summary>At its cap, the newest replaces the oldest (true) or is dropped (false).</summary>
            public bool steal;
            /// <summary>How far (0-1) it ducks the music and lesser sounds while it plays. 0 = no ducking.</summary>
            public float duck;

            public Rule(float volume, int maxVoices, float minGap, float range, int priority, bool steal = false, float duck = 0f)
            {
                this.volume = volume;
                this.maxVoices = maxVoices;
                this.minGap = minGap;
                this.range = range;
                this.priority = priority;
                this.steal = steal;
                this.duck = duck;
            }
        }

        public static Rule For(SfxKind kind)
        {
            switch (kind)
            {
                //                              volume voices gap   range prio steal duck
                // Confirmation sounds: quiet, close, first to go.
                case SfxKind.Footstep:     return new Rule(0.3f, 2, 0.12f, 9f, 1);
                case SfxKind.Whoosh:       return new Rule(0.45f, 2, 0.12f, 8f, 2);
                case SfxKind.WhooshHeavy:  return new Rule(0.55f, 1, 0.2f, 10f, 3);
                case SfxKind.Pickup:       return new Rule(0.45f, 1, 0.08f, 8f, 3);
                case SfxKind.Coins:        return new Rule(0.45f, 1, 0.1f, 8f, 3);

                // Enemy voices: the attack growl is a warning, so it outranks hurt grunts.
                case SfxKind.EnemyHurt:    return new Rule(0.5f, 2, 0.15f, 14f, 4);
                case SfxKind.EnemyAttack:  return new Rule(0.55f, 2, 0.3f, 14f, 6);
                case SfxKind.EnemyDeath:   return new Rule(0.65f, 2, 0.2f, 18f, 5);
                case SfxKind.BoltCharge:   return new Rule(0.55f, 2, 0.2f, 18f, 6);
                case SfxKind.BoltRelease:  return new Rule(0.5f, 2, 0.1f, 18f, 5);

                // Blows landing: one per hit, the newest replaces the oldest.
                case SfxKind.HitSlash:     return new Rule(0.8f, 3, 0.05f, 20f, 6, true);
                case SfxKind.HitStab:      return new Rule(0.8f, 3, 0.05f, 20f, 6, true);
                case SfxKind.HitBlunt:     return new Rule(0.8f, 3, 0.05f, 20f, 6, true);
                case SfxKind.Sever:        return new Rule(0.85f, 2, 0.08f, 20f, 7, true);

                // Defence: the player's most important feedback.
                case SfxKind.Block:        return new Rule(0.75f, 2, 0.06f, 18f, 8, true);
                case SfxKind.Deflect:      return new Rule(0.8f, 2, 0.08f, 18f, 8, true);
                case SfxKind.Parry:        return new Rule(0.9f, 1, 0.1f, 20f, 9, true, 0.25f);
                case SfxKind.Special:      return new Rule(0.8f, 1, 0.3f, 15f, 8, true);
                case SfxKind.PlayerHurt:   return new Rule(0.85f, 1, 0.25f, 10f, 10, true, 0.3f);

                // Big moments: heard from anywhere, everything else steps back.
                case SfxKind.Execution:    return new Rule(1f, 1, 0.3f, 25f, 9, true, 0.45f);
                case SfxKind.EnemyRoar:    return new Rule(0.9f, 1, 1f, 40f, 9, false, 0.5f);
                case SfxKind.TotemBreak:   return new Rule(1f, 1, 0.5f, 40f, 10, true, 0.55f);
                case SfxKind.PlayerDeath:  return new Rule(1f, 1, 1f, 10f, 10, true, 0.7f);
                case SfxKind.PortalOpen:   return new Rule(0.8f, 1, 1f, 30f, 7);
                case SfxKind.ChestOpen:    return new Rule(0.8f, 1, 0.5f, 15f, 6);
                case SfxKind.ChallengeStart: return new Rule(0.8f, 1, 0.5f, 25f, 8);
                case SfxKind.ChallengeEnd: return new Rule(0.8f, 1, 0.5f, 25f, 8);

                // Menus (played flat, outside the world's voice budget).
                case SfxKind.UiHover:      return new Rule(0.35f, 1, 0.05f, 0f, 10);
                case SfxKind.UiClick:      return new Rule(0.6f, 1, 0.05f, 0f, 10);
                case SfxKind.UiBack:       return new Rule(0.6f, 1, 0.05f, 0f, 10);
                case SfxKind.Blessing:     return new Rule(0.7f, 1, 0.3f, 0f, 10, false, 0.3f);
                case SfxKind.Extract:      return new Rule(0.85f, 1, 1f, 0f, 10, false, 0.5f);
                case SfxKind.LevelUp:      return new Rule(0.8f, 1, 0.5f, 0f, 10, false, 0.3f);

                default:                   return new Rule(0.7f, 2, 0.05f, 20f, 5);
            }
        }
    }
}
