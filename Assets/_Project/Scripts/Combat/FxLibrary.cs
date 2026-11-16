using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>The effects the game plays.</summary>
    public enum FxKind { BloodBurst, BloodStream, HitSparks, BloodDecal, LootShard, Shockwave, ExecuteMarker, Bolt }

    /// <summary>
    /// Every visual effect as a prefab asset (particle systems with their settings and materials saved in the
    /// asset, editable in the Inspector), and a pool that reuses instances instead of creating and destroying
    /// them per hit. Lives at Resources/FxLibrary; made by Roguelike > Build Effect Prefabs.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Fx Library", fileName = "FxLibrary")]
    public class FxLibrary : ScriptableObject
    {
        public GameObject bloodBurst;
        public GameObject bloodStream;
        public GameObject hitSparks;
        public GameObject bloodDecal;
        public GameObject lootShard;
        public GameObject shockwave;
        public GameObject executeMarker;
        public GameObject bolt;

        static FxLibrary instance;
        static bool looked;
        static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();

        public static FxLibrary Instance
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    instance = Resources.Load<FxLibrary>("FxLibrary");
                }
                return instance;
            }
        }

        public GameObject Prefab(FxKind kind)
        {
            switch (kind)
            {
                case FxKind.BloodBurst: return bloodBurst;
                case FxKind.BloodStream: return bloodStream;
                case FxKind.HitSparks: return hitSparks;
                case FxKind.BloodDecal: return bloodDecal;
                case FxKind.Shockwave: return shockwave;
                case FxKind.ExecuteMarker: return executeMarker;
                case FxKind.Bolt: return bolt;
                default: return lootShard;
            }
        }

        /// <summary>
        /// Takes an instance of an effect from its pool (or makes one) and places it. Particle effects return to
        /// the pool by themselves when they finish. Returns null if the library has no prefab for it.
        /// </summary>
        public static GameObject Spawn(FxKind kind, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var library = Instance;
            GameObject prefab = library != null ? library.Prefab(kind) : null;
            if (prefab == null) return null;

            if (!pools.TryGetValue(prefab, out Stack<GameObject> pool)) pools[prefab] = pool = new Stack<GameObject>();
            GameObject go = null;
            // Instances from an earlier scene were destroyed with it.
            while (pool.Count > 0 && go == null) go = pool.Pop();
            if (go == null)
            {
                go = Instantiate(prefab);
                // (Not "??": Unity's destroyed or missing objects only compare equal to null with ==.)
                var pooled = go.GetComponent<PooledFx>();
                if (pooled == null) pooled = go.AddComponent<PooledFx>();
                pooled.Prefab = prefab;
            }

            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            foreach (var system in go.GetComponentsInChildren<ParticleSystem>()) system.Play(true);
            return go;
        }

        /// <summary>Puts an instance from <see cref="Spawn"/> back in its pool.</summary>
        public static void Release(GameObject go)
        {
            if (go == null) return;
            var pooled = go.GetComponent<PooledFx>();
            if (pooled != null && pooled.Prefab != null) Release(go, pooled.Prefab);
            else Destroy(go);
        }

        /// <summary>Puts an instance back in its pool, hidden, ready to be reused.</summary>
        public static void Release(GameObject go, GameObject prefab)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(null, false);
            if (!pools.TryGetValue(prefab, out Stack<GameObject> pool)) pools[prefab] = pool = new Stack<GameObject>();
            pool.Push(go);
        }
    }
}
