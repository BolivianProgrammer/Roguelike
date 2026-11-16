using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Enemies;
using Roguelike.Run;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the main mission, Mission_Forest, from the forest art set (Art/Forest/Models): a medieval forest at
    /// night with a woodcutters' camp, a ruined chapel with a stone circle, and a small castle. Same night look as
    /// the hub (SceneAtmosphere). Roguelike > Build Forest Mission (asks before replacing the scene).
    ///
    /// Layout (meters, +Z is north): start and portal in the south, camp in the west, chapel in the east, castle in
    /// the north; dirt paths link them and a stream crosses the middle under a wooden bridge.
    /// </summary>
    public static class ForestBuilder
    {
        public const string ScenePath = "Assets/Scenes/Mission_Forest.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string Enemies = "Assets/_Project/Prefabs/Enemies/";
        const string Models = ArtImporter.ForestArtFolder + "/Models/";
        const string GroundMeshPath = ArtImporter.ForestArtFolder + "/Forest_Ground.asset";
        const float Half = 55f;          // the playable square is 110 x 110 m
        const float GroundExtent = 80f;  // the ground runs on into the fog
        const float StreamZ = 2f;        // the stream runs east-west along this line
        const float StreamGap = 2.9f;    // opening in the ground for the stream bed (the bed is 3 m wide)

        // Points of interest. The camp's palisade (radius 10) and the chapel (15 m long north-south) both stay
        // clear of the stream banks; the stone circle sits south of the chapel instead of overlapping it.
        static readonly Vector3 CampCentre = new Vector3(-30f, 0f, -16f);
        static readonly Vector3 ChapelCentre = new Vector3(30f, 0f, -13f);
        static readonly Vector3 StoneCircle = new Vector3(30f, 0f, -30f);
        static readonly Vector3 WoodsHollow = new Vector3(45f, 0f, -44f);

        /// <summary>Room an enemy needs where it appears (a little wider than its body).</summary>
        const float SpawnRadius = 0.5f;

        /// <summary>Models that get no collider at all (foliage, cloth, flat decals).</summary>
        static readonly HashSet<string> NoCollider = new HashSet<string>
        {
            "Bush_A", "Bush_B", "Fern", "Banner", "Campfire", "Path_Dirt_Straight", "Path_Dirt_Bend",
        };

        static Transform level;
        static System.Random random;
        static readonly List<Vector3> keepClear = new List<Vector3>();
        static readonly List<float> keepClearRadius = new List<float>();

        [MenuItem("Roguelike/Build Forest Mission")]
        public static void BuildMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Forest mission", ScenePath + " already exists. Rebuild it from scratch? Changes made to it by hand are lost.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void Build()
        {
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            random = new System.Random(1407);
            keepClear.Clear();
            keepClearRadius.Clear();
            pathLayer = 0;

            // The same night as the hub, brighter so fights in the woods stay readable.
            SceneAtmosphere.Night(16f, 60f, 1.6f);

            level = new GameObject("Level").transform;
            Ground();

            // Places that must stay free of trees. Camp and chapel sit south of the stream, well clear of its banks.
            Clear(new Vector3(0f, 0f, -44f), 9f);   // start and portal
            Clear(CampCentre, 12f);
            Clear(ChapelCentre, 10f);
            Clear(StoneCircle, 8.5f);
            Clear(new Vector3(0f, 0f, 32f), 22f);   // castle
            Clear(new Vector3(0f, 0f, StreamZ), 6f); // bridge
            Clear(WoodsHollow, 3f);

            var totems = new List<Totem>();
            Path();
            Stream();
            Mounds();
            totems.Add(Camp(CampCentre));
            totems.Add(Chapel(ChapelCentre));
            Transform bossPoint;
            totems.Add(Castle(new Vector3(0f, 0f, 34f), out bossPoint));
            Forest();
            Boundary();

            // Player start, and the way out just behind it.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            player.transform.SetPositionAndRotation(new Vector3(0f, 0f, -42f), Quaternion.identity);
            ExtractionZone extraction = Portal(new Vector3(0f, 0f, -48f));

            // Side content: a chest at the castle's west tower, one in a hollow in the eastern woods, and a
            // challenge shrine in the chapel's stone circle.
            Group("Side Content", out Transform side);
            Chest(side, "Treasure Chest (castle tower)", new Vector3(-11.5f, 0f, 45.5f), 160f);
            Chest(side, "Treasure Chest (woods hollow)", WoodsHollow, 220f);
            Shrine(side, "Challenge Shrine (stone circle)", StoneCircle, 0f);

            // Drifting embers and creeping mist over each place the player spends time in.
            Group("Atmosphere", out Transform air);
            foreach (var spot in new[] { new Vector3(0f, 0f, -38f), CampCentre, ChapelCentre + new Vector3(-4f, 0f, 0f), new Vector3(0f, 0f, 30f), new Vector3(0f, 0f, -12f) })
            {
                SceneAtmosphere.Effect("Ambient_Motes", air, spot);
                SceneAtmosphere.Effect("Ambient_Mist", air, spot);
            }

            // Last, with all the level's colliders in place: no enemy may appear inside a wall, tent or prop.
            ClearSpawnPoints();

            new GameObject("Level NavMesh").AddComponent<LevelNavMesh>();

            var mission = new GameObject("Mission").AddComponent<MissionManager>();
            var data = new SerializedObject(mission);
            var totemList = data.FindProperty("totems");
            totemList.arraySize = totems.Count;
            for (int i = 0; i < totems.Count; i++) totemList.GetArrayElementAtIndex(i).objectReferenceValue = totems[i];
            data.FindProperty("extraction").objectReferenceValue = extraction;
            data.FindProperty("bossPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "Brute.prefab");
            data.FindProperty("bossSpawnPoint").objectReferenceValue = bossPoint;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuBuilder.SetBuildOrder();
            Debug.Log("[ForestBuilder] Built " + ScenePath + ".");
        }

        // ---------------------------------------------------------------- Ground, paths, stream

        /// <summary>
        /// The forest floor: one tiled grass mesh with a straight opening for the stream bed. UVs are world metres
        /// over 4, so the grass texture tiles every 4 m without stretching.
        /// </summary>
        static void Ground()
        {
            Group("Ground", out Transform root);
            float south = StreamZ - StreamGap * 0.5f, north = StreamZ + StreamGap * 0.5f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            void Quad(float x0, float x1, float z0, float z1)
            {
                int start = vertices.Count;
                foreach (var v in new[] { new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0) })
                {
                    vertices.Add(v);
                    uvs.Add(new Vector2(v.x, v.z) / 4f);
                }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            Quad(-GroundExtent, GroundExtent, -GroundExtent, south);
            Quad(-GroundExtent, GroundExtent, north, GroundExtent);

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, GroundMeshPath);
            }
            mesh.Clear();
            mesh.name = "Forest_Ground";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var ground = new GameObject("Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            ground.transform.SetParent(root);
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtImporter.ForestArtFolder + "/Materials/Forest_Grass.mat");
            ground.GetComponent<MeshCollider>().sharedMesh = mesh;
            ground.isStatic = true;
        }

        static void Mounds()
        {
            Transform root = level.Find("Ground");
            string[] variants = { "Ground_Mound_A", "Ground_Mound_B", "Ground_Mound_C" };
            for (int i = 0; i < 14; i++)
            {
                Vector3 at = RandomPoint(Half - 6f);
                if (NearClear(at, 6f)) continue;
                Art(root, variants[random.Next(variants.Length)], at, Range(0f, 360f));
            }
        }

        static void Path()
        {
            Group("Path_Dirt", out Transform root);
            // Start -> fork south of the stream -> camp, chapel, and north over the bridge to the castle gate.
            // The path stops at the bridge ends instead of floating over the water.
            Strip(root, new[] { new Vector3(0f, 0f, -44f), new Vector3(-3f, 0f, -26f), new Vector3(2f, 0f, -12f), new Vector3(0.4f, 0f, -2.8f) }, 3.2f);
            Strip(root, new[] { new Vector3(-0.3f, 0f, 6.8f), new Vector3(-2f, 0f, 14f), new Vector3(0f, 0f, 22f) }, 3.2f);
            // West to the camp gate, east to the chapel's open side and on round it to the stone circle.
            Vector3 campGate = CampCentre + new Vector3(10.5f, 0f, 0f);
            Strip(root, new[] { new Vector3(2f, 0f, -12f), new Vector3(-10f, 0f, -13f), campGate }, 2.4f);
            Strip(root, new[] { new Vector3(2f, 0f, -12f), new Vector3(14f, 0f, -11f), ChapelCentre + new Vector3(-6f, 0f, 0f) }, 2.4f);
            Strip(root, new[] { ChapelCentre + new Vector3(-6f, 0f, -1f), ChapelCentre + new Vector3(-6.5f, 0f, -9f), StoneCircle + new Vector3(-2f, 0f, 6f) }, 2f);
        }

        static int pathLayer;

        /// <summary>
        /// A dirt path along a polyline: straight path pieces stretched from point to point, edges feathered. Every
        /// piece in the level sits a hair above the one before, so overlapping pieces (bends, forks) never flicker.
        /// </summary>
        static void Strip(Transform parent, Vector3[] points, float width)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                float length = Vector3.Distance(a, b) + width * 0.5f;
                GameObject piece = Art(parent, "Path_Dirt_Straight", (a + b) * 0.5f, Quaternion.LookRotation(b - a, Vector3.up).eulerAngles.y);
                piece.transform.localScale = new Vector3(width / 3.5f, 1f, length / 6f);
                piece.transform.position += Vector3.up * (0.006f + 0.003f * pathLayer++);
                ClearAlong(a, b, width * 0.5f + 1f);
            }
        }

        /// <summary>A stream running east-west through the whole forest, crossed by the wooden bridge on the main path.</summary>
        static void Stream()
        {
            Group("Stream", out Transform root);
            for (float x = -GroundExtent + 3f; x < GroundExtent; x += 6f)
                Art(root, "Stream_Straight", new Vector3(x, 0f, StreamZ), 90f);
            ClearAlong(new Vector3(-Half, 0f, StreamZ), new Vector3(Half, 0f, StreamZ), 2.5f);
            Art(root, "Bridge_Wooden", new Vector3(0f, 0f, StreamZ), 0f);
        }

        // ---------------------------------------------------------------- Points of interest

        /// <summary>The woodcutters' camp: palisade, tents, log piles, a campfire. Its totem stands by the fire.</summary>
        static Totem Camp(Vector3 centre)
        {
            Group("Camp_Woodcutters", out Transform root);
            // A closed palisade ring with a gate facing the path (east). Sections run along the ring (their long
            // side is local X, tangent to the circle); 22 sections of 3 m on a 10 m radius overlap a little, so
            // there are no gaps to squeeze through. The gate takes the place of the section at angle 0.
            const int sections = 22;
            for (int i = 1; i < sections; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 360f / sections, 0f) * Vector3.right;
                Art(root, "Palisade_Section", centre + dir * 10f, Quaternion.LookRotation(dir).eulerAngles.y);
            }
            Art(root, "Palisade_Gate", centre + Vector3.right * 10f, 90f);
            Art(root, "Tent", centre + new Vector3(-5f, 0f, 4f), 30f);
            Art(root, "Tent", centre + new Vector3(-6f, 0f, -3f), 110f);
            Art(root, "Log_Pile", centre + new Vector3(3f, 0f, 6f), 0f);
            Art(root, "Log_Pile", centre + new Vector3(5f, 0f, -6f), 70f);
            Art(root, "Chopping_Block", centre + new Vector3(2f, 0f, 2.5f), 0f);
            GameObject fire = Art(root, "Campfire", centre + new Vector3(-1f, 0f, 0f), 0f);
            SceneAtmosphere.Effect("Ambient_Brazier", fire.transform, fire.transform.position + Vector3.up * 0.12f, 1.3f);

            Guards(root, "Guards (camp)", centre + new Vector3(2f, 0f, -1f), Enemies + "Grunt.prefab", 3, 2);
            Guards(root, "Caster (camp)", centre + new Vector3(-4f, 0f, 7f), Enemies + "Caster.prefab", 1, 1);
            return Totem(root, centre + new Vector3(1f, 0f, -3f));
        }

        /// <summary>A roofless chapel ruin with a stone circle south of it. Its totem stands before the altar.</summary>
        static Totem Chapel(Vector3 centre)
        {
            Group("Chapel_Ruin", out Transform root);
            Art(root, "Chapel_Ruin", centre, 0f);
            Brazier(root, centre + new Vector3(-2.5f, 0f, 5.5f));

            // The stone circle, south of the ruin, holds the challenge shrine. Its stones stand ~3.5 m apart.
            Vector3 circle = StoneCircle;
            string[] stones = { "Standing_Stone_A", "Standing_Stone_B", "Standing_Stone_C" };
            for (int i = 0; i < 9; i++)
            {
                Vector3 at = circle + Quaternion.Euler(0f, i * 40f, 0f) * Vector3.forward * 6.5f;
                GameObject stone = Art(root, stones[random.Next(stones.Length)], at, i * 40f + Range(-8f, 8f));
                stone.transform.rotation *= Quaternion.Euler(Range(-5f, 5f), 0f, Range(-5f, 5f));
            }

            Guards(root, "Guards (chapel)", centre + new Vector3(-3f, 0f, 0f), Enemies + "Grunt.prefab", 3, 2);
            Guards(root, "Caster (chapel)", centre + new Vector3(2f, 0f, 9f), Enemies + "Caster.prefab", 1, 1);
            return Totem(root, centre + new Vector3(1.5f, 0f, 2.5f));
        }

        /// <summary>
        /// A small castle: curtain walls with four round towers, a gatehouse facing south with its drawbridge, and a
        /// keep at the back. The courtyard is the guardian's arena. The last totem stands before the gate.
        /// </summary>
        static Totem Castle(Vector3 centre, out Transform bossPoint)
        {
            Group("Castle", out Transform root);
            const float side = 28f;
            float h = side * 0.5f;

            Art(root, "Castle_Wall", centre + new Vector3(0f, 0f, h), 0f);
            Art(root, "Castle_Wall", centre + new Vector3(-h, 0f, 0f), 90f);
            Art(root, "Castle_Wall", centre + new Vector3(h, 0f, 0f), 270f);
            // The south wall in two halves either side of the gatehouse.
            Art(root, "Castle_Wall_Short", centre + new Vector3(-8.5f, 0f, -h), 180f);
            Art(root, "Castle_Wall_Short", centre + new Vector3(8.5f, 0f, -h), 180f);
            foreach (var corner in new[] { new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h) })
                Art(root, "Castle_Tower", centre + corner, Mathf.Atan2(corner.x, corner.z) * Mathf.Rad2Deg);

            Art(root, "Castle_Gatehouse", centre + new Vector3(0f, 0f, -h), 0f);
            // The drawbridge is hinged at the gate and lies out towards the forest.
            GameObject bridge = Art(root, "Drawbridge", centre + new Vector3(0f, 0f, -h - 1.72f), 180f, box: false);
            var deck = bridge.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, 0.15f, 3f);
            deck.size = new Vector3(4f, 0.3f, 6f);
            foreach (float x in new[] { -3.2f, 3.2f })
                Art(root, "Banner", centre + new Vector3(x, 7.2f, -h - 1.78f), 180f);
            Brazier(root, centre + new Vector3(-3.2f, 0f, -h - 8.4f));
            Brazier(root, centre + new Vector3(3.2f, 0f, -h - 8.4f));
            // The courtyard is the guardian's arena: two fires so the fight can be read.
            Brazier(root, centre + new Vector3(-6.5f, 0f, -5f));
            Brazier(root, centre + new Vector3(6.5f, 0f, 3f));

            // Flush against the north wall (keep half depth 4.02 + wall half thickness 0.84), leaving no alley behind it.
            Art(root, "Castle_Keep", centre + new Vector3(0f, 0f, h - 4.85f), 0f);
            Art(root, "Castle_Well", centre + new Vector3(-7f, 0f, 2f), 0f);
            Art(root, "Weapon_Stand", centre + new Vector3(7.5f, 0f, -3f), 270f);

            bossPoint = new GameObject("Boss Spawn Point").transform;
            bossPoint.SetParent(root);
            bossPoint.SetPositionAndRotation(centre + new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));

            Guards(root, "Guards (gate)", centre + new Vector3(0f, 0f, -h - 10f), Enemies + "Grunt.prefab", 4, 2);
            Guards(root, "Caster (gate)", centre + new Vector3(-8f, 0f, -h - 8f), Enemies + "Caster.prefab", 1, 1);
            return Totem(root, centre + new Vector3(4f, 0f, -h - 12f));
        }

        // ---------------------------------------------------------------- Forest and edges

        static void Forest()
        {
            Group("Forest", out Transform root);
            string[] pines = { "Tree_Pine_A", "Tree_Pine_B", "Tree_Pine_C" };
            string[] oaks = { "Tree_Oak_A", "Tree_Oak_B" };
            int placed = 0;
            for (int attempt = 0; attempt < 900 && placed < 170; attempt++)
            {
                Vector3 at = RandomPoint(Half - 4f);
                if (NearClear(at, 1.5f)) continue;
                bool pine = random.NextDouble() < 0.6;
                GameObject tree = Art(root, pine ? pines[random.Next(pines.Length)] : oaks[random.Next(oaks.Length)], at, Range(0f, 360f));
                tree.transform.localScale = Vector3.one * Range(0.85f, 1.25f);
                Keep(at, 2.2f);
                placed++;
            }

            // A dense line of pines along the edge, so the boundary is a wall of trees rather than an invisible one.
            for (float t = -Half; t <= Half; t += 3.4f)
                foreach (var at in new[] { new Vector3(t, 0f, -Half + 1.5f), new Vector3(t, 0f, Half - 1.5f), new Vector3(-Half + 1.5f, 0f, t), new Vector3(Half - 1.5f, 0f, t) })
                {
                    if (Mathf.Abs(at.z - StreamZ) < 3f) continue;
                    Vector3 jitter = new Vector3(Range(-0.8f, 0.8f), 0f, Range(-0.8f, 0.8f));
                    GameObject tree = Art(root, pines[random.Next(pines.Length)], at + jitter, Range(0f, 360f));
                    tree.transform.localScale = Vector3.one * Range(1f, 1.35f);
                }

            // Undergrowth and clutter.
            string[] rocks = { "Rock_A", "Rock_B", "Rock_C" };
            for (int i = 0; i < 90; i++)
            {
                Vector3 at = RandomPoint(Half - 3f);
                if (NearClear(at, 0.8f)) continue;
                int pick = random.Next(5);
                string model = pick == 0 ? rocks[random.Next(rocks.Length)] : pick == 1 ? "Log_Fallen" : pick == 2 ? "Fern" : random.Next(2) == 0 ? "Bush_A" : "Bush_B";
                GameObject prop = Art(root, model, at, Range(0f, 360f));
                if (pick >= 2) prop.transform.localScale = Vector3.one * Range(0.8f, 1.3f);
            }
        }

        /// <summary>
        /// Colliders at the edge, behind the tree line, so the player cannot walk out of the forest. They reach below
        /// the ground (the stream bed dips under it where the stream leaves the map) and overlap at the corners.
        /// </summary>
        static void Boundary()
        {
            Group("Boundary", out Transform root);
            const float length = Half * 2f + 2f;
            foreach (var (position, size) in new[]
            {
                (new Vector3(0f, 2f, Half), new Vector3(length, 8f, 1f)), (new Vector3(0f, 2f, -Half), new Vector3(length, 8f, 1f)),
                (new Vector3(Half, 2f, 0f), new Vector3(1f, 8f, length)), (new Vector3(-Half, 2f, 0f), new Vector3(1f, 8f, length)),
            })
            {
                var wall = new GameObject("Edge", typeof(BoxCollider));
                wall.transform.SetParent(root);
                wall.transform.position = position;
                wall.GetComponent<BoxCollider>().size = size;
                wall.isStatic = true;
            }
        }

        // ---------------------------------------------------------------- Gameplay objects

        static ExtractionZone Portal(Vector3 position)
        {
            Group("Extraction_Portal", out Transform root);
            var zoneObject = new GameObject("Portal Zone");
            zoneObject.transform.SetParent(root);
            zoneObject.transform.position = position;
            GameObject art = Art(zoneObject.transform, "Extraction_Portal", position, 0f);
            Transform runes = art.transform.Find("Extraction_Portal_Runes");
            SceneAtmosphere.Glow(zoneObject.transform, "Portal Light", position + Vector3.up * 1.6f, new Color(0.35f, 0.85f, 1f), 1.6f, 7f);

            var zone = zoneObject.AddComponent<ExtractionZone>();
            var data = new SerializedObject(zone);
            data.FindProperty("ring").objectReferenceValue = runes != null ? runes.GetComponent<Renderer>() : null;
            data.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        /// <summary>
        /// A totem on its stone base. The Totem component sits on the carved pillar (with a box to hit and a violet
        /// glow); the broken stump and its chunks wait hidden beside it for when it falls.
        /// </summary>
        static Totem Totem(Transform parent, Vector3 position)
        {
            Art(parent, "Totem_Base", position, 0f);
            Vector3 top = position + Vector3.up * 0.4f;

            var pillar = new GameObject("Totem");
            pillar.transform.SetParent(parent);
            pillar.transform.position = top;
            GameObject art = Art(pillar.transform, "Totem", top, 0f, box: false);
            var hitBox = pillar.AddComponent<BoxCollider>();
            hitBox.center = new Vector3(0f, 1.05f, 0f);
            hitBox.size = new Vector3(0.9f, 2.1f, 0.7f);
            Light glow = SceneAtmosphere.Glow(pillar.transform, "Totem Glow", top + Vector3.up * 1.3f, new Color(0.65f, 0.3f, 1f), 1.4f, 6f);

            GameObject remains = Art(parent, "Totem_Broken", top, 0f, box: false);
            remains.name = "Totem Remains";
            foreach (Transform child in remains.GetComponentsInChildren<Transform>())
                child.gameObject.isStatic = false;
            foreach (Transform chunk in remains.transform)
            {
                if (!chunk.name.StartsWith("Chunk")) continue;
                chunk.gameObject.AddComponent<BoxCollider>();
                chunk.gameObject.AddComponent<Rigidbody>().mass = 4f;
            }
            remains.AddComponent<BoxCollider>().size = new Vector3(0.6f, 0.6f, 0.6f);

            var totem = pillar.AddComponent<Totem>();
            var data = new SerializedObject(totem);
            Transform crystals = art.transform.Find("Totem_Crystals");
            data.FindProperty("body").objectReferenceValue = crystals != null ? crystals.GetComponent<Renderer>() : art.GetComponentInChildren<Renderer>();
            data.FindProperty("remains").objectReferenceValue = remains;
            data.FindProperty("glow").objectReferenceValue = glow;
            data.ApplyModifiedPropertiesWithoutUndo();
            remains.SetActive(false);
            return totem;
        }

        static void Chest(Transform parent, string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            GameObject art = Art(go.transform, "Treasure_Chest", position, yaw, box: false);
            foreach (Transform t in art.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(0.9f, 0.6f, 0.6f);
            var chest = go.AddComponent<TreasureChest>();
            var data = new SerializedObject(chest);
            data.FindProperty("lid").objectReferenceValue = art.transform.Find("Lid");
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Shrine(Transform parent, string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Art(go.transform, "Challenge_Shrine", position, yaw);
            SceneAtmosphere.Glow(go.transform, "Shrine Glow", position + new Vector3(0f, 1.5f, 0.6f), new Color(1f, 0.3f, 0.2f), 0.8f, 3.5f);
            go.AddComponent<KillChallenge>();
        }

        /// <summary>An iron brazier (from the hub set) with a living fire.</summary>
        static void Brazier(Transform parent, Vector3 position)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SceneAtmosphere.BrazierModel);
            if (model == null) return;
            var brazier = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            brazier.name = "Brazier";
            brazier.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Range(0f, 360f), 0f) * model.transform.localRotation);
            brazier.isStatic = true;
            var box = brazier.AddComponent<BoxCollider>();
            Bounds b = brazier.GetComponentInChildren<Renderer>().bounds;
            box.center = brazier.transform.InverseTransformPoint(b.center);
            Vector3 size = brazier.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            SceneAtmosphere.Effect("Ambient_Brazier", brazier.transform, position + Vector3.up * 1.03f);
        }

        /// <summary>A spawner with a few spawn points around a spot; it wakes when the player comes close.</summary>
        static void Guards(Transform parent, string name, Vector3 centre, string prefabPath, int total, int atOnce)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = centre;
            var points = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var point = new GameObject("Spawn Point " + (i + 1)).transform;
                point.SetParent(go.transform);
                point.position = centre + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 2.5f;
                points.Add(point);
            }
            var spawner = go.AddComponent<EnemySpawner>();
            var data = new SerializedObject(spawner);
            data.FindProperty("enemyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var list = data.FindProperty("spawnPoints");
            list.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            data.FindProperty("maxAlive").intValue = atOnce;
            data.FindProperty("totalSpawns").intValue = total;
            data.FindProperty("respawnDelay").floatValue = 2f;
            data.FindProperty("activationRange").floatValue = 20f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>
        /// Places a forest model. Its *_Collision meshes become invisible mesh colliders; models without one get a
        /// box around them, except foliage and decals (NoCollider) or when box is false.
        /// </summary>
        static GameObject Art(Transform parent, string model, Vector3 position, float yaw, bool box = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (asset == null)
            {
                Debug.LogError("[ForestBuilder] Missing forest model " + Models + model + ".fbx");
                return new GameObject(model + " (missing)");
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = model;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;

            bool hasCollision = false;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.EndsWith("_Collision")) continue;
                hasCollision = true;
                var meshRenderer = filter.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.enabled = false;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            if (!hasCollision && box && !NoCollider.Contains(model))
            {
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    var collider = go.AddComponent<BoxCollider>();
                    collider.center = go.transform.InverseTransformPoint(bounds.center);
                    Vector3 size = go.transform.InverseTransformVector(bounds.size);
                    collider.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                }
            }
            return go;
        }

        static void Group(string name, out Transform group, Transform parent = null)
        {
            group = new GameObject(name).transform;
            group.SetParent(parent != null ? parent : level);
        }

        static void Clear(Vector3 at, float radius)
        {
            keepClear.Add(at);
            keepClearRadius.Add(radius);
        }

        static void Keep(Vector3 at, float radius) => Clear(at, radius);

        /// <summary>Keeps a strip from a to b clear (a chain of overlapping circles), not just its middle.</summary>
        static void ClearAlong(Vector3 a, Vector3 b, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / radius));
            for (int i = 0; i <= steps; i++) Clear(Vector3.Lerp(a, b, i / (float)steps), radius);
        }

        /// <summary>
        /// Moves any spawn point that would put an enemy inside a collider (a wall, tent, rock, totem...) to the
        /// nearest open spot around it, searching outwards in rings.
        /// </summary>
        static void ClearSpawnPoints()
        {
            Physics.SyncTransforms();
            foreach (var spawner in level.root.GetComponentsInChildren<EnemySpawner>(true))
                foreach (Transform point in spawner.transform)
                {
                    if (Open(point.position)) continue;
                    Vector3 from = point.position;
                    bool moved = false;
                    for (float ring = 0.75f; ring <= 6f && !moved; ring += 0.75f)
                        for (int a = 0; a < 16 && !moved; a++)
                        {
                            Vector3 at = from + Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward * ring;
                            if (!Open(at)) continue;
                            point.position = at;
                            moved = true;
                        }
                    if (!moved) Debug.LogWarning($"[ForestBuilder] {spawner.name}/{point.name} at {from} is blocked and no open spot was found nearby.");
                }

            bool Open(Vector3 at)
            {
                Vector3 bottom = at + Vector3.up * (SpawnRadius + 0.05f), top = at + Vector3.up * 1.8f;
                foreach (Collider hit in Physics.OverlapCapsule(bottom, top, SpawnRadius, ~0, QueryTriggerInteraction.Ignore))
                    if (!(hit is CharacterController)) return false;
                // Not over the stream bed either: enemies should stand on dry ground.
                return Mathf.Abs(at.z - StreamZ) > StreamGap * 0.5f + SpawnRadius;
            }
        }

        static bool NearClear(Vector3 at, float margin)
        {
            for (int i = 0; i < keepClear.Count; i++)
            {
                Vector3 d = keepClear[i] - at;
                d.y = 0f;
                if (d.magnitude < keepClearRadius[i] + margin) return true;
            }
            return false;
        }

        static Vector3 RandomPoint(float extent) => new Vector3(Range(-extent, extent), 0f, Range(-extent, extent));

        static float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
