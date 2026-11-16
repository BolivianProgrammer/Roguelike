using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Meta;
using Roguelike.Player;

namespace Roguelike.Gear
{
    /// <summary>
    /// The armor and trinkets the player wears. Their stat changes are applied whenever a scene loads (hub and
    /// missions alike) and again whenever the player changes what they wear. Worn gear goes on missions with the
    /// player and always comes back, even after a death.
    /// </summary>
    public static class Equipment
    {
        public const int MaxTrinkets = 2;

        static readonly object Source = new object();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        public static ArmorDefinition Armor
        {
            get
            {
                var item = ItemDatabase.Find(SaveSystem.Data.armor);
                return item != null ? item.armor : null;
            }
        }

        public static List<TrinketDefinition> Trinkets
        {
            get
            {
                var list = new List<TrinketDefinition>();
                foreach (string id in SaveSystem.Data.trinkets)
                {
                    var item = ItemDatabase.Find(id);
                    if (item != null && item.trinket != null) list.Add(item.trinket);
                }
                return list;
            }
        }

        /// <summary>Gives the player the effects of everything they wear right now.</summary>
        public static void Apply()
        {
            PlayerStats.RemoveAll(Source);
            ArmorDefinition armor = Armor;
            if (armor != null) StatEffect.ApplyAll(armor.effects, Source);
            foreach (var trinket in Trinkets) StatEffect.ApplyAll(trinket.effects, Source);
        }

        /// <summary>Puts on an owned piece of armor or a trinket. Armor replaces the current set; trinkets need a free slot.</summary>
        public static bool Wear(string id)
        {
            SaveData data = SaveSystem.Data;
            var item = ItemDatabase.Find(id);
            if (item == null || !data.gear.Contains(id)) return false;

            if (item.kind == ItemKind.Armor)
            {
                if (!string.IsNullOrEmpty(data.armor)) data.gear.Add(data.armor);
                data.armor = id;
            }
            else if (item.kind == ItemKind.Trinket)
            {
                if (data.trinkets.Count >= MaxTrinkets) return false;
                data.trinkets.Add(id);
            }
            else return false;

            data.gear.Remove(id);
            SaveSystem.Save();
            Apply();
            return true;
        }

        /// <summary>Takes something off and puts it back with the stored gear.</summary>
        public static void TakeOff(string id)
        {
            SaveData data = SaveSystem.Data;
            if (data.armor == id) data.armor = "";
            else if (!data.trinkets.Remove(id)) return;
            data.gear.Add(id);
            SaveSystem.Save();
            Apply();
        }
    }
}
