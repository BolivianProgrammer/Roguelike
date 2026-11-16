using System.Collections.Generic;
using UnityEngine;
using Roguelike.Audio;
using Roguelike.Gear;
using Roguelike.Meta;
using Roguelike.Player;
using Roguelike.Run;
using Roguelike.UI;
using Roguelike.Weapons;

namespace Roguelike.Hub
{
    /// <summary>
    /// Runs the hub: lays out the weapons the player owns on the racks, opens the menus of the characters the
    /// player talks to (vendor, mentor, armorer, quartermaster, mission board), and sends the player
    /// into the chosen mission through the gate.
    ///
    /// What is in the player's hands or holsters, and the armor and trinkets they wear, go through the gate with
    /// them and come back whatever happens; dying only loses what the run itself gave.
    /// </summary>
    public class HubManager : MonoBehaviour
    {
        [Header("Stash")]
        [Tooltip("Rack slots that hold the player's stored weapons.")]
        [SerializeField] Holster[] rackSlots;
        [Tooltip("Where weapons go when the racks are full.")]
        [SerializeField] Transform overflowPoint;

        [Header("Vendor")]
        [SerializeField] HubNpc vendor;
        [Tooltip("Places on the vendor's stand where bought weapons appear, used in turn.")]
        [SerializeField] Transform[] vendorSpawnPoints;
        [Tooltip("How many changing items the vendor offers besides the basic weapons.")]
        [SerializeField] int rotatingStock = 4;

        [Header("Other characters")]
        [SerializeField] HubNpc mentor;
        [SerializeField] HubNpc armorer;
        [SerializeField] HubNpc quartermaster;
        [Tooltip("Where weapons taken out of the stash are handed over.")]
        [SerializeField] Transform quartermasterSpawnPoint;
        [SerializeField] HubNpc missionBoard;

        [Header("Other")]
        [SerializeField] ExtractionZone missionGate;
        [Tooltip("The sign by the gate, showing which mission it leads to.")]
        [SerializeField] Roguelike.UI.Signboard gateSign;
        [Tooltip("A board in the plaza showing essence, coins and level.")]
        [SerializeField] Roguelike.UI.Signboard statusSign;

        /// <summary>Every weapon object in the hub that the player owns.</summary>
        readonly List<Grabbable> owned = new List<Grabbable>();
        /// <summary>Weapons owned but not laid out (the racks are full): kept by the quartermaster.</summary>
        readonly List<string> stored = new List<string>();
        int nextSpawnPoint;
        bool leaving;
        string vendorPage = "buy";

        void Start()
        {
            Loadout.EnteredFromHub = false;
            Enemies.Difficulty.Reset();
            SaveData data = SaveSystem.Data;
            Combat.CombatLog.Log("HUB start: stash [" + string.Join(", ", data.stash) + "], leftover loadout [" + string.Join(", ", data.loadout) + "], in mission " + data.inMission);

            // The game was closed in the middle of a mission: that counts as dying there.
            if (data.inMission) AbandonedRun(data);

            // Anything that came back from a mission belongs in the stash again.
            data.stash.AddRange(data.loadout);
            data.loadout.Clear();

            // The armory starts empty. A player who owns no weapon at all and cannot afford one gets enough coins
            // for the cheapest, so a death never locks them out.
            int grant = WeaponGrant(data);
            SaveSystem.Save();
            Equipment.Apply();

            SpawnStash(data.stash);

            if (vendor != null) vendor.Interacted += OpenShop;
            if (mentor != null) mentor.Interacted += OpenMentor;
            if (armorer != null) armorer.Interacted += OpenArmorer;
            if (quartermaster != null) quartermaster.Interacted += OpenStash;
            if (missionBoard != null) missionBoard.Interacted += OpenMissions;
            if (missionGate != null) missionGate.Extracted += EnterMission;

            RefreshSigns();

            if (grant > 0)
                Announcer.Show("You have no weapon.\nThe vendor will sell you one: +" + grant + " coins", new Color(1f, 0.85f, 0.45f), 5f);

            // What happened on the last run, shown once.
            if (!string.IsNullOrEmpty(data.lastRun.outcome))
            {
                RunSummaryPanel.Show(data.lastRun);
                data.lastRun = new RunSummary();
                SaveSystem.Save();
            }
        }

        void OnDestroy()
        {
            WorldMenu.Close();
        }

        /// <summary>
        /// Coins added so a player with no weapon (none stored, none carried back) can buy the cheapest one.
        /// </summary>
        static int WeaponGrant(SaveData data)
        {
            foreach (string id in data.stash)
            {
                var owned = ItemDatabase.Find(id);
                if (owned != null && owned.kind == ItemKind.Weapon) return 0;
            }
            int cheapest = int.MaxValue;
            foreach (var item in ItemDatabase.All(i => i.kind == ItemKind.Weapon && i.price > 0 && !i.bossLoot))
                cheapest = Mathf.Min(cheapest, item.price);
            if (cheapest == int.MaxValue || data.coins >= cheapest) return 0;
            int grant = cheapest - data.coins;
            data.coins = cheapest;
            Combat.CombatLog.Log("HUB: no weapon owned; granted " + grant + " coins");
            return grant;
        }

        /// <summary>The game was closed mid-mission: the run's gains are gone, the weapons brought in come home.</summary>
        static void AbandonedRun(SaveData data)
        {
            data.stash.AddRange(data.loadout);
            data.loadout.Clear();
            data.inMission = false;
            data.deaths++;
            data.lastRun = new RunSummary { outcome = "abandoned", items = new List<string>(), mission = data.selectedMission };
            Combat.CombatLog.Log("HUB: the last mission was abandoned; its gains are lost, carried and worn gear kept");
        }

        void SpawnStash(List<string> ids)
        {
            int slot = 0;
            foreach (string id in ids)
            {
                var entry = ItemDatabase.Find(id);
                if (entry == null || entry.prefab == null) continue;

                Holster rack = null;
                while (rackSlots != null && slot < rackSlots.Length && rack == null)
                {
                    if (rackSlots[slot] != null && rackSlots[slot].Item == null) rack = rackSlots[slot];
                    slot++;
                }

                // Racks full: the quartermaster keeps the rest.
                if (rack == null)
                {
                    stored.Add(id);
                    continue;
                }

                GameObject spawned = Instantiate(entry.prefab, rack.transform.position, Quaternion.identity);
                var grabbable = spawned.GetComponent<Grabbable>();
                if (grabbable == null) continue;
                owned.Add(grabbable);
                rack.Store(grabbable);
            }
        }

        // ---------------------------------------------------------------- Vendor

        void OpenShop(HubNpc npc)
        {
            vendorPage = "buy";
            WorldMenu.Open(npc.DisplayName, () => "Coins: " + SaveSystem.Data.coins, ShopEntries, npc.transform);
        }

        /// <summary>
        /// The basic weapons are always for sale; a few more items, picked at random, change after every run.
        /// </summary>
        List<ItemDatabase.Item> Stock()
        {
            var stock = ItemDatabase.All(item => item.price > 0 && !item.bossLoot && item.rarity == Rarity.Common && item.kind == ItemKind.Weapon);
            var rotating = ItemDatabase.All(item => item.price > 0 && !item.bossLoot && !stock.Contains(item));
            var random = new System.Random(SaveSystem.Data.vendorRotation * 7919 + 17);
            for (int i = 0; i < rotatingStock && rotating.Count > 0; i++)
            {
                int pick = random.Next(rotating.Count);
                stock.Add(rotating[pick]);
                rotating.RemoveAt(pick);
            }
            return stock;
        }

        List<WorldMenu.Entry> ShopEntries()
        {
            var entries = new List<WorldMenu.Entry>();
            int coins = SaveSystem.Data.coins;

            if (vendorPage == "sell")
            {
                foreach (var sale in Sellable())
                {
                    var captured = sale;
                    entries.Add(new WorldMenu.Entry { Label = "Sell " + ItemDatabase.NameOf(sale.id), Detail = "+" + sale.item.SellPrice + " coins", Action = () => Sell(captured) });
                }
                if (entries.Count == 0) entries.Add(new WorldMenu.Entry { Label = "Nothing in the stash to sell", Enabled = false });
                entries.Add(new WorldMenu.Entry { Label = "< Back to buying", Action = () => vendorPage = "buy" });
                entries.Add(new WorldMenu.Entry { Label = "Leave", Action = WorldMenu.Close });
                return entries;
            }

            foreach (ItemDatabase.Item item in Stock())
            {
                ItemDatabase.Item captured = item;
                string kind = item.kind == ItemKind.Weapon ? "" : "  (" + item.kind.ToString().ToLower() + ")";
                entries.Add(new WorldMenu.Entry
                {
                    Label = item.displayName + kind,
                    Detail = item.price + " coins",
                    Enabled = coins >= item.price,
                    Action = () => Buy(captured),
                });
            }
            entries.Add(new WorldMenu.Entry { Label = "Sell items >", Action = () => vendorPage = "sell" });
            entries.Add(new WorldMenu.Entry { Label = "Leave", Action = WorldMenu.Close });
            return entries;
        }

        void Buy(ItemDatabase.Item item)
        {
            SaveData data = SaveSystem.Data;
            if (data.coins < item.price) return;

            data.coins -= item.price;
            Combat.CombatLog.Log("HUB bought " + item.id + " for " + item.price);
            if (vendor != null) vendor.Play("Npc_Trade");

            if (item.kind == ItemKind.Weapon)
            {
                data.stash.Add(item.id);
                // The weapon is put on the stand for the player to pick up.
                Transform point = vendor != null ? vendor.transform : transform;
                if (vendorSpawnPoints != null && vendorSpawnPoints.Length > 0)
                    point = vendorSpawnPoints[nextSpawnPoint++ % vendorSpawnPoints.Length];
                GameObject spawned = Instantiate(item.prefab, point.position, point.rotation);
                var grabbable = spawned.GetComponent<Grabbable>();
                if (grabbable != null) owned.Add(grabbable);
                Announcer.Show("Bought " + item.displayName + "\nIt is on the stand", new Color(1f, 0.85f, 0.3f), 2f);
            }
            else
            {
                // Armor and trinkets go to the armorer, ready to wear.
                data.gear.Add(item.id);
                Announcer.Show("Bought " + item.displayName + "\nWear it at the armorer", new Color(1f, 0.85f, 0.3f), 2.5f);
            }
            SaveSystem.Save();
            Sfx.PlayUi(SfxKind.Coins);
            RefreshSigns();
        }

        struct Sale
        {
            public string id;
            public ItemDatabase.Item item;
            /// <summary>The weapon object on a rack, if it is one; otherwise the id is in the stored list or the gear.</summary>
            public Grabbable onRack;
        }

        /// <summary>What the player can sell: weapons on the racks or with the quartermaster, and gear not worn.</summary>
        List<Sale> Sellable()
        {
            var list = new List<Sale>();
            List<Grabbable> carried = Loadout.CarriedItems();
            foreach (var weapon in owned)
            {
                if (weapon == null || carried.Contains(weapon)) continue;
                string id = Loadout.IdOf(weapon);
                var item = ItemDatabase.Find(id);
                if (item != null) list.Add(new Sale { id = id, item = item, onRack = weapon });
            }
            foreach (string id in stored)
            {
                var item = ItemDatabase.Find(id);
                if (item != null) list.Add(new Sale { id = id, item = item });
            }
            foreach (string id in SaveSystem.Data.gear)
            {
                var item = ItemDatabase.Find(id);
                if (item != null) list.Add(new Sale { id = id, item = item });
            }
            return list;
        }

        void Sell(Sale sale)
        {
            SaveData data = SaveSystem.Data;
            if (sale.onRack != null)
            {
                owned.Remove(sale.onRack);
                if (sale.onRack.Holster != null) sale.onRack.Holster.Eject();
                Destroy(sale.onRack.gameObject);
                data.stash.Remove(sale.id);
            }
            else if (sale.item.kind == ItemKind.Weapon)
            {
                stored.Remove(sale.id);
                data.stash.Remove(sale.id);
            }
            else data.gear.Remove(sale.id);

            data.coins += sale.item.SellPrice;
            SaveSystem.Save();
            Sfx.PlayUi(SfxKind.Coins);
            Announcer.Show("Sold " + sale.item.displayName + " for " + sale.item.SellPrice, new Color(1f, 0.85f, 0.3f), 2f);
            RefreshSigns();
        }

        // ---------------------------------------------------------------- Mentor

        void OpenMentor(HubNpc npc)
        {
            WorldMenu.Open(npc.DisplayName, MentorHeader, MentorEntries, npc.transform);
        }

        static string MentorHeader()
        {
            int level = PlayerProfile.Level;
            int bonus = Mathf.RoundToInt(level * PlayerProfile.DamagePerLevel * 100f);
            return "Level " + level + "   (+" + bonus + "% damage and health)   Essence: " + SaveSystem.Data.essence;
        }

        List<WorldMenu.Entry> MentorEntries()
        {
            int level = PlayerProfile.Level;
            int cost = PlayerProfile.NextLevelCost;
            return new List<WorldMenu.Entry>
            {
                new WorldMenu.Entry
                {
                    Label = "Level up  (" + level + " > " + (level + 1) + ")",
                    Detail = cost + " essence",
                    Enabled = SaveSystem.Data.essence >= cost,
                    Action = LevelUp,
                },
                new WorldMenu.Entry { Label = "Leave", Action = WorldMenu.Close },
            };
        }

        void LevelUp()
        {
            if (!PlayerProfile.TryLevelUp()) return;
            if (mentor != null) mentor.Play("Npc_Bless");
            Sfx.PlayUi(SfxKind.LevelUp);
            Announcer.Show("LEVEL " + PlayerProfile.Level + "\n+5% damage, +5% health", new Color(0.6f, 0.9f, 1f), 2.5f);
            RefreshSigns();
        }

        // ---------------------------------------------------------------- Armorer

        void OpenArmorer(HubNpc npc)
        {
            WorldMenu.Open(npc.DisplayName, ArmorerHeader, ArmorerEntries, npc.transform);
        }

        static string ArmorerHeader()
        {
            SaveData data = SaveSystem.Data;
            return "Worn: " + (string.IsNullOrEmpty(data.armor) ? "no armor" : ItemDatabase.NameOf(data.armor))
                + ", trinkets " + data.trinkets.Count + " / " + Equipment.MaxTrinkets;
        }

        List<WorldMenu.Entry> ArmorerEntries()
        {
            SaveData data = SaveSystem.Data;
            var entries = new List<WorldMenu.Entry>();

            if (!string.IsNullOrEmpty(data.armor))
            {
                string id = data.armor;
                entries.Add(new WorldMenu.Entry { Label = "Take off " + ItemDatabase.NameOf(id), Detail = Summary(id), Action = () => ChangeGear(id, false) });
            }
            foreach (string worn in data.trinkets)
            {
                string id = worn;
                entries.Add(new WorldMenu.Entry { Label = "Take off " + ItemDatabase.NameOf(id), Detail = Summary(id), Action = () => ChangeGear(id, false) });
            }
            foreach (string kept in data.gear)
            {
                string id = kept;
                var item = ItemDatabase.Find(id);
                bool canWear = item != null && (item.kind == ItemKind.Armor || data.trinkets.Count < Equipment.MaxTrinkets);
                entries.Add(new WorldMenu.Entry { Label = "Wear " + ItemDatabase.NameOf(id), Detail = Summary(id), Enabled = canWear, Action = () => ChangeGear(id, true) });
            }
            if (entries.Count == 0) entries.Add(new WorldMenu.Entry { Label = "You own no armor or trinkets yet", Enabled = false });
            entries.Add(new WorldMenu.Entry { Label = "Leave", Action = WorldMenu.Close });
            return entries;
        }

        static string Summary(string id)
        {
            var item = ItemDatabase.Find(id);
            return item != null ? item.Summary : "";
        }

        void ChangeGear(string id, bool wear)
        {
            if (wear) Equipment.Wear(id);
            else Equipment.TakeOff(id);
            Sfx.PlayUi(SfxKind.Pickup);
            RefreshSigns();
        }

        // ---------------------------------------------------------------- Quartermaster (stash)

        void OpenStash(HubNpc npc)
        {
            WorldMenu.Open(npc.DisplayName, () => "Weapons kept here: " + stored.Count + "   (racks hold " + (rackSlots != null ? rackSlots.Length : 0) + ")", StashEntries, npc.transform);
        }

        List<WorldMenu.Entry> StashEntries()
        {
            var entries = new List<WorldMenu.Entry>();
            Grabbable held = HeldWeapon();
            if (held != null)
                entries.Add(new WorldMenu.Entry { Label = "Store the " + ItemDatabase.NameOf(Loadout.IdOf(held)) + " in your hand", Action = () => StoreHeld(held) });

            for (int i = 0; i < stored.Count; i++)
            {
                int index = i;
                entries.Add(new WorldMenu.Entry { Label = "Take out " + ItemDatabase.NameOf(stored[i]), Action = () => TakeOut(index) });
            }
            if (entries.Count == 0) entries.Add(new WorldMenu.Entry { Label = "Nothing stored", Enabled = false });
            entries.Add(new WorldMenu.Entry { Label = "Leave", Action = WorldMenu.Close });
            return entries;
        }

        static Grabbable HeldWeapon()
        {
            foreach (var grabber in FindObjectsOfType<PhysicsHandGrabber>())
                if (grabber.Held != null && Loadout.IdOf(grabber.Held) != null) return grabber.Held;
            return null;
        }

        void StoreHeld(Grabbable weapon)
        {
            string id = Loadout.IdOf(weapon);
            foreach (var grabber in FindObjectsOfType<PhysicsHandGrabber>())
                if (grabber.Held == weapon) grabber.Release();
            owned.Remove(weapon);
            Destroy(weapon.gameObject);
            stored.Add(id);
            Sfx.PlayUi(SfxKind.Pickup);
        }

        void TakeOut(int index)
        {
            if (index < 0 || index >= stored.Count) return;
            var item = ItemDatabase.Find(stored[index]);
            if (item == null || item.prefab == null) return;
            stored.RemoveAt(index);

            Transform point = quartermasterSpawnPoint != null ? quartermasterSpawnPoint : quartermaster != null ? quartermaster.transform : transform;
            GameObject spawned = Instantiate(item.prefab, point.position + Vector3.up * 0.1f, point.rotation);
            var grabbable = spawned.GetComponent<Grabbable>();
            if (grabbable != null) owned.Add(grabbable);
            Announcer.Show(item.displayName + " is on the table", new Color(0.9f, 0.85f, 0.7f), 2f);
        }

        // ---------------------------------------------------------------- Mission board

        void OpenMissions(HubNpc npc)
        {
            WorldMenu.Open(npc.DisplayName, () => "The gate leads to: " + SelectedName(), MissionEntries, npc.transform);
        }

        static string SelectedName()
        {
            var mission = MissionCatalog.Selected(SaveSystem.Data);
            return mission != null ? mission.displayName : Meta.SceneFlow.MissionScene;
        }

        List<WorldMenu.Entry> MissionEntries()
        {
            var entries = new List<WorldMenu.Entry>();
            SaveData data = SaveSystem.Data;
            var selected = MissionCatalog.Selected(data);
            var catalog = MissionCatalog.Instance;
            if (catalog != null && catalog.missions != null)
                foreach (var mission in catalog.missions)
                {
                    if (mission == null) continue;
                    var captured = mission;
                    bool open = mission.IsUnlocked(data);
                    bool done = data.completedMissions.Contains(mission.name);
                    string detail = !open ? "complete " + mission.unlockedBy.displayName + " first"
                        : (mission == selected ? "> selected  " : "") + "tier " + (mission.tier + 1) + (done ? "  (done)" : "");
                    entries.Add(new WorldMenu.Entry { Label = mission.displayName, Detail = detail, Enabled = open, Action = () => SelectMission(captured) });
                }
            entries.Add(new WorldMenu.Entry { Label = "Leave", Action = WorldMenu.Close });
            return entries;
        }

        void SelectMission(MissionDefinition mission)
        {
            SaveSystem.Data.selectedMission = mission.name;
            SaveSystem.Save();
            Sfx.PlayUi(SfxKind.UiClick);
            Announcer.Show(mission.displayName + "\n" + mission.description, new Color(0.8f, 0.6f, 1f), 3.5f);
            RefreshSigns();
        }

        // ---------------------------------------------------------------- Leaving

        /// <summary>The player stepped through the gate: what they carry and wear goes with them, the rest stays.</summary>
        void EnterMission()
        {
            if (leaving) return;
            leaving = true;
            WorldMenu.Close();

            SaveData data = SaveSystem.Data;
            var mission = MissionCatalog.Selected(data);
            MissionCatalog.Current = mission;

            SaveCarried();
            data.inMission = true;
            data.runs++;
            data.vendorRotation++;
            Combat.CombatLog.Log("HUB entering " + (mission != null ? mission.name : "mission") + " carrying [" + string.Join(", ", data.loadout) + "], leaving [" + string.Join(", ", data.stash) + "]");
            SaveSystem.Save();

            Loadout.EnteredFromHub = true;
            SceneFlow.Load(mission != null ? mission.sceneName : SceneFlow.MissionScene);
        }

        /// <summary>Writes down which owned weapons are carried (the loadout) and which stay (the stash).</summary>
        void SaveCarried()
        {
            SaveData data = SaveSystem.Data;
            List<Grabbable> carried = Loadout.CarriedItems();
            data.loadout.Clear();
            data.stash.Clear();
            data.stash.AddRange(stored);
            foreach (var item in owned)
            {
                if (item == null) continue;
                string id = Loadout.IdOf(item);
                if (id == null) continue;
                if (carried.Contains(item)) data.loadout.Add(id);
                else data.stash.Add(id);
            }
            SaveSystem.Save();
        }

        void RefreshSigns()
        {
            SaveData data = SaveSystem.Data;
            if (statusSign != null)
            {
                statusSign.Text = "<b>Level " + data.level + "</b>\nEssence " + data.essence + "\nCoins " + data.coins
                    + "\n" + (string.IsNullOrEmpty(data.armor) ? "No armor" : ItemDatabase.NameOf(data.armor))
                    + (data.trinkets.Count > 0 ? "\n" + string.Join(", ", data.trinkets.ConvertAll(ItemDatabase.NameOf)) : "");
            }
            if (gateSign != null) gateSign.Text = "<b>" + SelectedName().ToUpper() + "</b>\n<size=70%>Stand in the circle. What you carry and wear goes with you.</size>";
        }

        int hudSignature;

        /// <summary>Progress at a glance on the monitor's HUD. In the headset, read the sign in the plaza or the wrist.</summary>
        void LateUpdate()
        {
            DesktopHud hud = DesktopHud.Get();
            if (hud == null) return;
            SaveData data = SaveSystem.Data;
            string mission = SelectedName();
            int signature = data.level * 31 ^ data.essence * 73856093 ^ data.coins * 19349663 ^ (mission != null ? mission.GetHashCode() : 0);
            if (signature == hudSignature) return;
            hudSignature = signature;
            hud.SetObjective("Hub",
                "<color=#9C958A>Level</color> <color=#FFE9A8>" + data.level + "</color>    <color=#9C958A>Essence</color> <color=#FFD25A>" + data.essence
                + "</color>    <color=#9C958A>Coins</color> <color=#FFD25A>" + data.coins + "</color>\n"
                + "<color=#9C958A>Mission</color>  " + mission + "\n"
                + "<i>Carry what you want into the gate</i>");
        }
    }
}
