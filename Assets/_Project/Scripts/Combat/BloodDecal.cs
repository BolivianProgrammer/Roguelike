using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Blood splashes left on the floor and walls. A fixed pool of flat quads is reused, oldest first, so a long
    /// fight never piles up more than <see cref="PoolSize"/> of them (cheap on the Quest).
    /// </summary>
    public static class BloodDecal
    {
        const int PoolSize = 48;
        static readonly Color BloodColor = new Color(0.32f, 0.02f, 0.02f, 0.95f);

        static Transform[] pool;
        static int next;
        static readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>Leaves a splash on the level surface below (or behind) a point.</summary>
        public static void Splat(Vector3 point, float size, Vector3 direction)
        {
            // Downwards first; for a spray thrown sideways, also try along its direction (walls).
            if (!FindSurface(point + Vector3.up * 0.3f, Vector3.down, 4f, out RaycastHit surface)
                && !FindSurface(point, direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.down, 2f, out surface)) return;

            Transform decal = Next();
            if (decal == null) return;
            decal.gameObject.SetActive(true);
            decal.position = surface.point + surface.normal * 0.004f;
            decal.rotation = Quaternion.LookRotation(-surface.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float s = size * Random.Range(0.7f, 1.3f);
            decal.localScale = new Vector3(s, s * Random.Range(0.6f, 1f), 1f);
        }

        /// <summary>Only the level itself takes decals: not bodies, weapons, the player or triggers.</summary>
        static bool FindSurface(Vector3 from, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(from, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.attachedRigidbody != null || hits[i].collider is CharacterController) continue;
                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    best = hits[i];
                    found = true;
                }
            }
            return found;
        }

        static Transform Next()
        {
            if (pool == null || pool[0] == null)
            {
                // The pool belongs to the current scene; it is rebuilt after a scene change.
                pool = new Transform[PoolSize];
                var root = new GameObject("Blood Decals").transform;
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", BloodColor);
                var library = FxLibrary.Instance;
                GameObject prefab = library != null ? library.bloodDecal : null;
                for (int i = 0; i < PoolSize; i++)
                {
                    if (prefab != null)
                    {
                        // The authored decal: its material already has the blood colour.
                        var authored = Object.Instantiate(prefab, root);
                        authored.name = "Blood";
                        authored.SetActive(false);
                        pool[i] = authored.transform;
                        continue;
                    }
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Blood";
                    Object.Destroy(quad.GetComponent<Collider>());
                    quad.transform.SetParent(root, false);
                    var quadRenderer = quad.GetComponent<MeshRenderer>();
                    quadRenderer.sharedMaterial = FlatMaterials.Shared;
                    quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    quadRenderer.receiveShadows = false;
                    quadRenderer.SetPropertyBlock(block);
                    quad.SetActive(false);
                    pool[i] = quad.transform;
                }
                next = 0;
            }
            Transform decal = pool[next];
            next = (next + 1) % PoolSize;
            return decal;
        }
    }
}
