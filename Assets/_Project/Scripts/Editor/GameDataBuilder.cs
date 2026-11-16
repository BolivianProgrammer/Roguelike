using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Roguelike.Gear;
using Roguelike.Meta;
using Roguelike.Player;
using Roguelike.Run;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Creates (or updates) every piece of game data that is only numbers and names: blessings, armor, trinkets,
    /// missions and the item list the saves, vendor and chests use. Run from Roguelike > Build Game Data after
    /// adding weapons (Build Weapon Prefabs first). Assets that already exist keep their file, so references to
    /// them stay valid; their numbers are reset to the ones written here.
    /// </summary>
    public static class GameDataBuilder
    {
        const string DataFolder = "Assets/_Project/Data";
        const string ResourcesFolder = "Assets/_Project/Resources";

        [MenuItem("Roguelike/Build Game Data")]
        public static void BuildAll()
        {
            BuildBuffs();
            BuildItemDatabase();
            BuildMissions();
            BuildGameAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("[GameDataBuilder] Built blessings, armor, trinkets, missions and the item database.");
        }

        // ---------------------------------------------------------------- Blessings

        public static BuffDefinition[] BuildBuffs()
        {
            Folder("Buffs");
            var buffs = new[]
            {
                Buff("Might", "+25% damage", new Color(1f, 0.35f, 0.25f), Rarity.Common, E(StatType.Damage, 0.25f)),
                Buff("Vigor", "+30% max health", new Color(0.3f, 1f, 0.4f), Rarity.Common, E(StatType.MaxHealth, 0.3f)),
                Buff("Swiftness", "+20% move speed", new Color(0.3f, 0.8f, 1f), Rarity.Common, E(StatType.MoveSpeed, 0.2f)),
                Buff("Endurance", "+40% max stamina", new Color(1f, 0.9f, 0.3f), Rarity.Common, E(StatType.MaxStamina, 0.4f)),
                Buff("Second Wind", "+50% stamina regen", new Color(0.7f, 1f, 0.9f), Rarity.Common, E(StatType.StaminaRegen, 0.5f)),
                Buff("Glass Cannon", "+50% damage, -20% max health", new Color(1f, 0.4f, 0.9f), Rarity.Rare, E(StatType.Damage, 0.5f), E(StatType.MaxHealth, -0.2f)),
                Buff("Iron Skin", "Take 20% less damage", new Color(0.7f, 0.7f, 0.75f), Rarity.Common, E(StatType.DamageTaken, -0.2f)),
                Buff("Featherweight", "Swings cost 30% less stamina", new Color(0.9f, 1f, 0.7f), Rarity.Common, E(StatType.StaminaCost, -0.3f)),
                Buff("Greed", "+50% coins and essence found", new Color(1f, 0.8f, 0.2f), Rarity.Common, E(StatType.CoinGain, 0.5f), E(StatType.EssenceGain, 0.5f)),
                Buff("Bloodthirst", "8% of the damage you deal heals you", new Color(0.8f, 0.05f, 0.1f), Rarity.Rare, E(StatType.Lifesteal, 0.08f)),
                Buff("Spiked Hide", "Melee attackers take 60% of the damage they deal you", new Color(0.6f, 0.4f, 0.2f), Rarity.Rare, E(StatType.Thorns, 0.6f)),
                Buff("Crushing Blows", "+50% stagger: heavy hits stun far more often", new Color(1f, 0.55f, 0.1f), Rarity.Rare, E(StatType.StaggerPower, 0.5f)),
                Buff("Quickening", "Weapon specials recharge 40% faster", new Color(0.5f, 0.6f, 1f), Rarity.Rare, E(StatType.SpecialCooldown, -0.4f)),
                Buff("Foresight", "Every later totem offers one more blessing", new Color(0.8f, 0.7f, 1f), Rarity.Rare, E(StatType.BuffChoices, 1f)),
                Buff("Titan", "+40% max health, +30% damage", new Color(1f, 0.85f, 0.5f), Rarity.Legendary, E(StatType.MaxHealth, 0.4f), E(StatType.Damage, 0.3f)),
                Buff("Vampire Lord", "15% lifesteal and +20% move speed", new Color(0.6f, 0f, 0.15f), Rarity.Legendary, E(StatType.Lifesteal, 0.15f), E(StatType.MoveSpeed, 0.2f)),
            };

            var catalog = LoadOrCreate<BuffCatalog>(ResourcesFolder + "/BuffCatalog.asset");
            catalog.buffs = buffs;
            EditorUtility.SetDirty(catalog);
            return buffs;
        }

        static BuffDefinition Buff(string name, string description, Color color, Rarity rarity, params StatEffect[] effects)
        {
            var buff = LoadOrCreate<BuffDefinition>(DataFolder + "/Buffs/" + name.Replace(" ", "") + ".asset");
            buff.displayName = name;
            buff.description = description;
            buff.color = color;
            buff.rarity = rarity;
            buff.effects = effects;
            EditorUtility.SetDirty(buff);
            return buff;
        }

        static StatEffect E(StatType stat, float percent) => new StatEffect(stat, percent);

        // ---------------------------------------------------------------- Items

        public static void BuildItemDatabase()
        {
            Folder("Armor");
            Folder("Trinkets");

            var heavy = Armor("Armor_Bulwark", "Iron Bulwark", ArmorWeight.Heavy, new Color(0.45f, 0.47f, 0.5f),
                "Plate for standing your ground: shrug off blows, swing slower and tire faster.",
                E(StatType.MaxHealth, 0.5f), E(StatType.DamageTaken, -0.25f), E(StatType.StaggerPower, 0.2f), E(StatType.MoveSpeed, -0.15f), E(StatType.StaminaCost, 0.25f));
            var medium = Armor("Armor_Wayfarer", "Wayfarer's Mail", ArmorWeight.Medium, new Color(0.55f, 0.45f, 0.3f),
                "A balanced coat of rings.",
                E(StatType.MaxHealth, 0.2f), E(StatType.DamageTaken, -0.1f));
            var light = Armor("Armor_Shadow", "Shadow Leathers", ArmorWeight.Light, new Color(0.15f, 0.15f, 0.18f),
                "For those who never stand still: quick feet, light swings, little protection.",
                E(StatType.MoveSpeed, 0.18f), E(StatType.StaminaRegen, 0.3f), E(StatType.StaminaCost, -0.15f), E(StatType.MaxHealth, -0.15f));
            var legendary = Armor("Armor_Guardian", "Guardian's Carapace", ArmorWeight.Heavy, new Color(0.5f, 0.1f, 0.08f),
                "Taken from a fallen guardian. Heavy, but it hits back.",
                E(StatType.MaxHealth, 0.4f), E(StatType.DamageTaken, -0.2f), E(StatType.Thorns, 0.4f), E(StatType.MoveSpeed, -0.08f));

            var fang = Trinket("Trinket_VampireFang", "Vampire Fang", "Drinks from every wound you cause.", new Color(0.7f, 0f, 0.1f), E(StatType.Lifesteal, 0.07f));
            var thorn = Trinket("Trinket_ThornBand", "Thorned Band", "Whoever strikes you bleeds for it.", new Color(0.4f, 0.6f, 0.2f), E(StatType.Thorns, 0.5f));
            var die = Trinket("Trinket_FortuneDie", "Fortune's Die", "Totems offer one more blessing.", new Color(1f, 1f, 0.9f), E(StatType.BuffChoices, 1f));
            var coin = Trinket("Trinket_MiserCoin", "Miser's Coin", "+50% coins found.", new Color(1f, 0.8f, 0.2f), E(StatType.CoinGain, 0.5f));
            var shard = Trinket("Trinket_EssenceShard", "Essence Shard", "+50% essence found.", new Color(0.6f, 0.5f, 1f), E(StatType.EssenceGain, 0.5f));
            var glass = Trinket("Trinket_Hourglass", "Cracked Hourglass", "Weapon specials recharge 30% faster.", new Color(0.8f, 0.9f, 1f), E(StatType.SpecialCooldown, -0.3f));
            var horn = Trinket("Trinket_BullHorn", "Bull Horn", "+30% stagger.", new Color(0.8f, 0.7f, 0.6f), E(StatType.StaggerPower, 0.3f));
            var heart = Trinket("Trinket_IronHeart", "Iron Heart", "+15% max health, swings cost 10% less stamina.", new Color(0.6f, 0.6f, 0.65f),
                E(StatType.MaxHealth, 0.15f), E(StatType.StaminaCost, -0.1f));

            var database = LoadOrCreate<ItemDatabase>(ResourcesFolder + "/ItemDatabase.asset");
            database.starterItemId = "Sword_Basic";
            database.items = new[]
            {
                Weapon("Sword_Basic", "Sword", "Sword", 0, Rarity.Common, false, false),
                Weapon("Hammer_Basic", "Hammer", "Hammer", 100, Rarity.Common, false, true),
                Weapon("Dagger_Basic", "Dagger", "Dagger", 60, Rarity.Common, false, true),
                Weapon("Sword_Fine", "Fine Sword", "Sword_Fine", 260, Rarity.Rare, false, true),
                Weapon("Dagger_Fine", "Fine Dagger", "Dagger_Fine", 200, Rarity.Rare, false, true),
                Weapon("Cleaver_Revenant", "Revenant Cleaver", "Cleaver", 0, Rarity.Rare, true, false),
                Weapon("Axe_Guardian", "Guardian Axe", "GuardianAxe", 0, Rarity.Legendary, true, false),

                Gear(heavy, 220, Rarity.Common, false, false),
                Gear(medium, 140, Rarity.Common, false, false),
                Gear(light, 160, Rarity.Common, false, false),
                Gear(legendary, 0, Rarity.Legendary, true, false),

                Gear(fang, 240, Rarity.Rare, false, true),
                Gear(thorn, 180, Rarity.Rare, false, true),
                Gear(die, 300, Rarity.Rare, false, true),
                Gear(coin, 120, Rarity.Common, false, true),
                Gear(shard, 120, Rarity.Common, false, true),
                Gear(glass, 160, Rarity.Common, false, true),
                Gear(horn, 140, Rarity.Common, false, true),
                Gear(heart, 150, Rarity.Common, false, true),
            };
            EditorUtility.SetDirty(database);
        }

        static ItemDatabase.Item Weapon(string id, string name, string prefabName, int price, Rarity rarity, bool bossLoot, bool chestLoot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapons/" + prefabName + ".prefab");
            if (prefab == null) Debug.LogWarning("[GameDataBuilder] Missing weapon prefab " + prefabName + " - run Roguelike > Build Weapon Prefabs first.");
            return new ItemDatabase.Item { id = id, displayName = name, kind = ItemKind.Weapon, prefab = prefab, price = price, rarity = rarity, bossLoot = bossLoot, chestLoot = chestLoot };
        }

        static ItemDatabase.Item Gear(ScriptableObject asset, int price, Rarity rarity, bool bossLoot, bool chestLoot)
        {
            var armor = asset as ArmorDefinition;
            var trinket = asset as TrinketDefinition;
            return new ItemDatabase.Item
            {
                id = asset.name,
                displayName = armor != null ? armor.displayName : trinket.displayName,
                kind = armor != null ? ItemKind.Armor : ItemKind.Trinket,
                armor = armor,
                trinket = trinket,
                price = price,
                rarity = rarity,
                bossLoot = bossLoot,
                chestLoot = chestLoot,
            };
        }

        static ArmorDefinition Armor(string id, string name, ArmorWeight weight, Color color, string description, params StatEffect[] effects)
        {
            var armor = LoadOrCreate<ArmorDefinition>(DataFolder + "/Armor/" + id + ".asset");
            armor.displayName = name;
            armor.weight = weight;
            armor.color = color;
            armor.description = description;
            armor.effects = effects;
            EditorUtility.SetDirty(armor);
            return armor;
        }

        static TrinketDefinition Trinket(string id, string name, string description, Color color, params StatEffect[] effects)
        {
            var trinket = LoadOrCreate<TrinketDefinition>(DataFolder + "/Trinkets/" + id + ".asset");
            trinket.displayName = name;
            trinket.description = description;
            trinket.color = color;
            trinket.effects = effects;
            EditorUtility.SetDirty(trinket);
            return trinket;
        }

        // ---------------------------------------------------------------- Missions

        public static void BuildMissions()
        {
            Folder("Missions");
            // The main mission: the forest and its castle, at three tiers, each unlocking the next.
            var forest = Mission("Mission_Forest", "The Old Keep", "Mission_Forest", 0, null,
                "A forest road to a fallen castle. Break the totems at the woodcutters' camp, the chapel and the gate; the guardian waits in the courtyard.", 50f);
            var forestNight = Mission("Mission_ForestNight", "The Old Keep by Night", "Mission_Forest", 1, forest,
                "The keep again, after dark: tougher enemies, richer spoils.", 45f);
            var forestNightmare = Mission("Mission_ForestNightmare", "Nightmare Keep", "Mission_Forest", 2, forestNight,
                "For the proven. Everything hits harder; the spoils are legendary.", 40f);

            var catalog = LoadOrCreate<MissionCatalog>(ResourcesFolder + "/MissionCatalog.asset");
            catalog.missions = new[] { forest, forestNight, forestNightmare };
            EditorUtility.SetDirty(catalog);
        }

        static MissionDefinition Mission(string id, string name, string scene, int tier, MissionDefinition unlockedBy, string description, float challengeTime)
        {
            var mission = LoadOrCreate<MissionDefinition>(DataFolder + "/Missions/" + id + ".asset");
            mission.displayName = name;
            mission.sceneName = scene;
            mission.tier = tier;
            mission.unlockedBy = unlockedBy;
            mission.description = description;
            mission.challengeTime = challengeTime;
            EditorUtility.SetDirty(mission);
            return mission;
        }

        // ---------------------------------------------------------------- Runtime prefabs

        /// <summary>The enemy prefabs code spawns at runtime (mixed spawns, challenge waves).</summary>
        public static void BuildGameAssets()
        {
            Folder("Missions");
            const string enemies = "Assets/_Project/Prefabs/Enemies/";
            var assets = LoadOrCreate<GameAssets>(ResourcesFolder + "/GameAssets.asset");
            assets.grunt = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Grunt.prefab");
            assets.caster = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Caster.prefab");
            assets.runner = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Runner.prefab");
            assets.shieldbearer = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Shieldbearer.prefab");
            assets.elite = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Elite.prefab");
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- Helpers

        static void Folder(string name)
        {
            if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Data");
            if (!AssetDatabase.IsValidFolder(DataFolder + "/" + name)) AssetDatabase.CreateFolder(DataFolder, name);
            if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
