using System;
using System.Collections.Generic;
using UnityEngine;
using Roguelike.Gear;

namespace Roguelike.Meta
{
    public enum ItemKind { Weapon, Armor, Trinket }

    public enum Rarity { Common, Rare, Legendary }

    /// <summary>
    /// The list of every item the game knows about: its id, what it is, and what a vendor charges.
    /// Saves store item ids; this turns them back into objects. One asset lives in a Resources folder so any
    /// scene can find it. A weapon's id is the name of its Weapon Definition asset; armor and trinkets use the
    /// name of their own definition asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [Serializable]
        public class Item
        {
            public string id;
            public string displayName;
            public ItemKind kind = ItemKind.Weapon;
            public Rarity rarity = Rarity.Common;
            [Tooltip("Weapons: what to spawn.")]
            public GameObject prefab;
            [Tooltip("Armor: its stats.")]
            public ArmorDefinition armor;
            [Tooltip("Trinkets: their effect.")]
            public TrinketDefinition trinket;
            [Tooltip("Coins a vendor charges. 0 = never sold.")]
            public int price;
            [Tooltip("Dropped by mission guardians instead of being sold.")]
            public bool bossLoot;
            [Tooltip("Can turn up in treasure chests.")]
            public bool chestLoot;

            /// <summary>What a vendor pays for it: a third of its price, more for guardian loot.</summary>
            public int SellPrice => Mathf.Max(5, bossLoot ? 120 + price / 2 : price / 3);

            /// <summary>One line describing what the item does, for menus.</summary>
            public string Summary
            {
                get
                {
                    if (kind == ItemKind.Armor && armor != null) return armor.Summary;
                    if (kind == ItemKind.Trinket && trinket != null) return trinket.Summary;
                    return rarity == Rarity.Common ? "Weapon" : rarity + " weapon";
                }
            }
        }

        public Item[] items;
        [Tooltip("Handed out only when a mission scene is started directly in the editor (nothing was carried in).")]
        public string starterItemId = "Sword_Basic";

        static ItemDatabase instance;

        public static ItemDatabase Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<ItemDatabase>("ItemDatabase");
                return instance;
            }
        }

        public static Item Find(string id)
        {
            var database = Instance;
            if (database == null || database.items == null || string.IsNullOrEmpty(id)) return null;
            foreach (var item in database.items)
                if (item != null && item.id == id) return item;
            return null;
        }

        /// <summary>A readable name for an item id, or the id itself if it is unknown.</summary>
        public static string NameOf(string id)
        {
            var item = Find(id);
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : id;
        }

        /// <summary>Every item matching a test, in database order.</summary>
        public static List<Item> All(Predicate<Item> match)
        {
            var list = new List<Item>();
            var database = Instance;
            if (database == null || database.items == null) return list;
            foreach (var item in database.items)
                if (item != null && match(item)) list.Add(item);
            return list;
        }
    }
}
