using UnityEngine;
using Roguelike.Audio;
using Roguelike.Combat;

namespace Roguelike.Run
{
    /// <summary>
    /// A small glowing shard of essence (violet) or coin (gold) that spills from fallen enemies, chests and
    /// challenges. It hops out, then flies to the player by itself and is banked into the run's loot on arrival:
    /// nothing has to be touched or hit to collect it.
    /// </summary>
    public class LootShard : MonoBehaviour
    {
        static readonly Color EssenceColor = new Color(0.65f, 0.45f, 1f);
        static readonly Color CoinColor = new Color(1f, 0.8f, 0.25f);

        int essence;
        int coins;
        Vector3 velocity;
        float age;
        Transform head;

        /// <summary>Spills a handful of shards worth this much essence and coin in total.</summary>
        public static void Burst(Vector3 position, int essence, int coins)
        {
            int essenceShards = essence > 0 ? Mathf.Clamp(essence / 6, 1, 8) : 0;
            int coinShards = coins > 0 ? Mathf.Clamp(coins / 4, 1, 8) : 0;
            for (int i = 0; i < essenceShards; i++)
                Spawn(position, Share(essence, essenceShards, i), 0, EssenceColor);
            for (int i = 0; i < coinShards; i++)
                Spawn(position, 0, Share(coins, coinShards, i), CoinColor);
        }

        /// <summary>Splits a total into near-equal whole shares that add up exactly.</summary>
        static int Share(int total, int parts, int index) => total / parts + (index < total % parts ? 1 : 0);

        static void Spawn(Vector3 position, int essence, int coins, Color color)
        {
            // Pooled: dozens fly out of every chest and enemy, so they are reused instead of created and destroyed.
            GameObject go = FxLibrary.Spawn(FxKind.LootShard, position, Random.rotation);
            if (go == null)
            {
                Debug.LogWarning("[LootShard] No loot shard prefab: run Roguelike > Build Effect Prefabs. The loot is added directly.");
                RunState.Add(essence, coins);
                return;
            }
            go.name = essence > 0 ? "Essence Shard" : "Coin Shard";
            var shardRenderer = go.GetComponent<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            shardRenderer.SetPropertyBlock(block);

            var shard = go.GetComponent<LootShard>();
            shard.essence = essence;
            shard.coins = coins;
            shard.age = 0f;
            shard.velocity = (Random.insideUnitSphere + Vector3.up * 1.5f).normalized * Random.Range(2f, 3.5f);
        }

        void Start()
        {
            var cam = Camera.main;
            head = cam != null ? cam.transform : null;
        }

        void Update()
        {
            if (WorldFreeze.Active) return;
            age += Time.deltaTime;
            transform.Rotate(220f * Time.deltaTime, 160f * Time.deltaTime, 0f);

            // First it hops out and settles; then it homes in on the player's chest.
            if (age < 0.7f || head == null)
            {
                velocity += Physics.gravity * Time.deltaTime;
                velocity *= 1f - 1.5f * Time.deltaTime;
                Vector3 next = transform.position + velocity * Time.deltaTime;
                if (next.y < 0.15f) { next.y = 0.15f; velocity.y = Mathf.Abs(velocity.y) * 0.3f; }
                transform.position = next;
                return;
            }

            Vector3 chest = head.position + Vector3.down * 0.45f;
            Vector3 toPlayer = chest - transform.position;
            float speed = Mathf.Lerp(3f, 14f, Mathf.Clamp01((age - 0.7f) / 1.5f));
            velocity = Vector3.Lerp(velocity, toPlayer.normalized * speed, 6f * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;

            if (toPlayer.magnitude < 0.35f || age > 12f)
            {
                RunState.Add(essence, coins);
                Sfx.Play(coins > 0 ? SfxKind.Coins : SfxKind.Pickup, transform.position, 0.5f);
                FxLibrary.Release(gameObject);
            }
        }
    }
}
