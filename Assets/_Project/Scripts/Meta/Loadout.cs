using System.Collections.Generic;
using UnityEngine;
using Roguelike.Player;
using Roguelike.Weapons;

namespace Roguelike.Meta
{
    /// <summary>
    /// What the player is physically carrying: weapons in their hands and in the holsters on their body.
    /// That, and nothing else, is what goes into a mission and what comes back out of one.
    /// </summary>
    public static class Loadout
    {
        /// <summary>
        /// True when the current mission was entered through the hub's gate. A mission started directly in the
        /// editor has no loadout chosen, so it hands out the starter weapon instead.
        /// </summary>
        public static bool EnteredFromHub;

        /// <summary>The item id of a weapon object, or null if it is not a known item.</summary>
        public static string IdOf(Grabbable item)
        {
            if (item == null) return null;
            var weapon = item.GetComponent<Weapon>();
            return weapon != null && weapon.Definition != null ? weapon.Definition.name : null;
        }

        /// <summary>Every item currently in the player's hands or body holsters.</summary>
        public static List<Grabbable> CarriedItems()
        {
            var carried = new List<Grabbable>();
            foreach (var grabber in Object.FindObjectsOfType<PhysicsHandGrabber>())
                if (grabber.Held != null && !carried.Contains(grabber.Held)) carried.Add(grabber.Held);
            foreach (var holster in Object.FindObjectsOfType<Holster>())
                if (holster.OnBody && holster.Item != null && !carried.Contains(holster.Item)) carried.Add(holster.Item);
            return carried;
        }

        public static List<string> CarriedIds()
        {
            var ids = new List<string>();
            foreach (var item in CarriedItems())
            {
                string id = IdOf(item);
                if (id != null) ids.Add(id);
            }
            return ids;
        }

        /// <summary>Spawns the given items and puts them in the player's body holsters, as far as there is room.</summary>
        public static void Equip(IList<string> ids)
        {
            var free = new List<Holster>();
            foreach (var holster in Object.FindObjectsOfType<Holster>())
                if (holster.OnBody && holster.Item == null) free.Add(holster);
            // Hips before the back, left before right, so the first weapon ends up where it always was.
            free.Sort((a, b) => string.CompareOrdinal(Order(a), Order(b)));

            var cam = Camera.main;
            Vector3 dropPoint = cam != null ? cam.transform.position + cam.transform.forward * 0.6f : Vector3.up;

            foreach (string id in ids)
            {
                var entry = ItemDatabase.Find(id);
                if (entry == null || entry.prefab == null) continue;

                GameObject spawned = Object.Instantiate(entry.prefab, dropPoint, Quaternion.identity);
                var grabbable = spawned.GetComponent<Grabbable>();
                if (grabbable != null && free.Count > 0)
                {
                    free[0].Store(grabbable);
                    free.RemoveAt(0);
                }
            }
        }

        static string Order(Holster holster)
        {
            if (holster.name.Contains("Left Hip")) return "0";
            if (holster.name.Contains("Right Hip")) return "1";
            return "2" + holster.name;
        }
    }
}
