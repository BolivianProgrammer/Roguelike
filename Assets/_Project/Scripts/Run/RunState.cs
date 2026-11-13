using System;

namespace Roguelike.Run
{
    /// <summary>
    /// What the player is carrying during a mission. Everything here is at risk:
    /// dying wipes it, and only extracting or finishing the level banks it.
    /// </summary>
    public static class RunState
    {
        /// <summary>Currency for levelling up the character, gathered this run.</summary>
        public static int Essence { get; private set; }
        /// <summary>Currency for vendors, gathered this run.</summary>
        public static int Coins { get; private set; }
        public static int Kills { get; private set; }

        /// <summary>Totals safely brought home by earlier runs. The hub will spend these.</summary>
        public static int BankedEssence { get; private set; }
        public static int BankedCoins { get; private set; }

        public static event Action Changed;

        public static void Add(int essence, int coins)
        {
            Essence += essence;
            Coins += coins;
            Changed?.Invoke();
        }

        public static void CountKill()
        {
            Kills++;
            Changed?.Invoke();
        }

        /// <summary>Starts a fresh run with empty pockets.</summary>
        public static void Begin()
        {
            Essence = 0;
            Coins = 0;
            Kills = 0;
            Changed?.Invoke();
        }

        /// <summary>The player died: everything carried is gone.</summary>
        public static void Lose() => Begin();

        /// <summary>The player made it out: what they carried is theirs to keep.</summary>
        public static void Bank()
        {
            BankedEssence += Essence;
            BankedCoins += Coins;
            Begin();
        }
    }
}
