using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Roguelike.Hub;
using Roguelike.Player;
using Roguelike.Run;
using Roguelike.UI;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Builds the hub: a compact version of Plaza 25 de Mayo in Sucre, Bolivia, from the models in Art/Hub.
    /// A 34 x 34 m square with a 22 m wide paved centre around the monument to Antonio Jose de Sucre; every station
    /// (vendor, armorer, quartermaster, armory rack, mission board, gate) stands in that centre, facing the monument.
    /// Eight paths lead out between raised gardens; colonial facades close the square in.
    ///
    /// Roguelike > Build Hub Scene makes a fresh scene; Roguelike > Rebuild Hub Plaza And Stations replaces only the
    /// plaza, buildings and stations in the open Hub scene (lighting and anything added by hand stay).
    /// Layout angles are measured from north (+Z) clockwise.
    /// </summary>
    public static class HubBuilder
    {
        const string ScenePath = "Assets/Scenes/Hub.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string MaterialFolder = "Assets/_Project/Materials/";
        const string ArtModels = ArtImporter.HubArtFolder + "/Models/";

        // Plaza measurements in meters (must match Plaza_Ground.fbx, made by Art/Hub/Source/build_hub.py).
        const float Half = 17f;            // centre to the edge of the square
        const float CentreRadius = 11f;    // the paved centre
        const float Curb = 14.5f;          // centre to the outer edge of the gardens
        const float StationRing = 8.3f;    // where the stations stand
        const float FacadeLine = 22f;      // front of the buildings around the square

        static readonly Vector3 Spawn = new Vector3(0f, 0f, -14.5f);
        static readonly Vector3 GateCircle = new Vector3(0f, 0f, 13.2f);

        static readonly HashSet<string> NoCollider = new HashSet<string> { "Palm", "Jacaranda" };
        static Material dark, bronze, portal, signWood, signTrim;
        static Collider ground;

        [MenuItem("Roguelike/Build Hub Scene")]
        public static void Build()
        {
            GameDataBuilder.BuildAll();
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            BuildAll();

            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuBuilder.SetBuildOrder();
            Debug.Log("[HubBuilder] Built " + ScenePath);
        }

        [MenuItem("Roguelike/Rebuild Hub Plaza And Stations")]
        public static void Rebuild()
        {
            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "Plaza", "Surroundings", "Stations", "Hub Manager", "Atmosphere" })
            {
                GameObject old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }
            BuildAll();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HubBuilder] Rebuilt the hub's plaza and stations.");
        }

        static void BuildAll()
        {
            CreateMaterials();
            Transform plaza = new GameObject("Plaza").transform;
            BuildGround(plaza);
            Art(plaza, "Monument_Sucre", Vector3.zero, 0f);
            BuildTrees(plaza);
            BuildBenchesAndLamps(plaza);
            BuildSurroundings(new GameObject("Surroundings").transform);
            BuildStations(new GameObject("Stations").transform);
            BuildAtmosphere(new GameObject("Atmosphere").transform);

            // The player starts at the end of the south path, looking at the monument.
            var player = Object.FindObjectOfType<PlayerBody>();
            Transform rig = player != null ? player.transform.root : null;
            if (rig == null)
            {
                var anyRig = GameObject.Find("Player");
                rig = anyRig != null ? anyRig.transform : null;
            }
            if (rig != null) rig.SetPositionAndRotation(OnGround(Spawn), Quaternion.identity);
        }

        // ---------------------------------------------------------------- Placement helpers

        /// <summary>A point on the circle around the monument, at an angle from north (clockwise).</summary>
        static Vector3 Around(float degrees, float radius)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)) * radius;
        }

        /// <summary>The yaw that turns a model's front (+Z) from one point towards another.</summary>
        static float Facing(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>The same point, standing on the plaza's surface (paths, gardens and steps differ in height).</summary>
        static Vector3 OnGround(Vector3 point)
        {
            if (ground == null) return point;
            Physics.SyncTransforms();
            var ray = new Ray(new Vector3(point.x, 20f, point.z), Vector3.down);
            return ground.Raycast(ray, out RaycastHit hit, 40f) ? new Vector3(point.x, hit.point.y, point.z) : new Vector3(point.x, 0f, point.z);
        }

        /// <summary>
        /// Places a plaza model on the ground. Its *_Collision meshes become invisible mesh colliders; other models
        /// get a box around them, except foliage.
        /// </summary>
        static GameObject Art(Transform parent, string model, Vector3 position, float yaw, bool box = true)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtModels + model + ".fbx");
            if (asset == null)
            {
                Debug.LogError("[HubBuilder] Missing hub model " + ArtModels + model + ".fbx");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = model;
            go.transform.SetParent(parent);
            // Keep the model's own root rotation (a single-mesh Blender export lies on its back without it).
            go.transform.SetPositionAndRotation(OnGround(position), Quaternion.Euler(0f, yaw, 0f) * asset.transform.localRotation);
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

        /// <summary>A plain block (for the pieces that still wait for art: see ASSETS_NEEDED.md).</summary>
        static GameObject Block(Transform parent, string name, Vector3 position, float yaw, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(OnGround(position) + Vector3.up * size.y * 0.5f, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        // ---------------------------------------------------------------- Plaza

        static void BuildGround(Transform parent)
        {
            GameObject plazaGround = Art(parent, "Plaza_Ground", Vector3.zero, 0f, box: false);
            ground = null;
            if (plazaGround != null)
            {
                foreach (var filter in plazaGround.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    ground = collider;
                }
            }
            // The streets around the square stay a plain slab for now (Street_Paving in ASSETS_NEEDED.md).
            var road = Block(parent, "Streets", new Vector3(0f, -0.5f, 0f), 0f, new Vector3(70f, 0.5f, 70f), dark);
            road.transform.position = new Vector3(0f, -0.3f, 0f);
        }

        /// <summary>A jacaranda in the middle of every other garden, palms in the others and on the four corners.</summary>
        static void BuildTrees(Transform parent)
        {
            Transform trees = new GameObject("Trees").transform;
            trees.SetParent(parent);
            for (int i = 0; i < 8; i++)
            {
                Vector3 spot = Around(22.5f + i * 45f, (CentreRadius + Curb) * 0.5f + 0.4f);
                Art(trees, i % 2 == 0 ? "Jacaranda" : "Palm", spot, i * 73f);
            }
            float corner = (Curb + Half) * 0.5f;
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    Art(trees, "Palm", new Vector3(x * corner, 0f, z * corner), x * 40f + z * 110f);
        }

        static void BuildBenchesAndLamps(Transform parent)
        {
            Transform benches = new GameObject("Benches").transform;
            benches.SetParent(parent);
            Transform lamps = new GameObject("Lamps").transform;
            lamps.SetParent(parent);

            // Four benches round the monument's steps, facing it.
            for (int i = 0; i < 4; i++)
            {
                Vector3 spot = Around(45f + i * 90f, 5.3f);
                Art(benches, "Bench", spot, Facing(spot, Vector3.zero));
            }
            // A lamp beside every path where it meets the centre; every other one casts real light.
            for (int i = 0; i < 8; i++)
            {
                Vector3 spot = Around(i * 45f + 12f, CentreRadius - 0.5f);
                Lamp(lamps, spot, i % 2 == 0);
            }
        }

        static void Lamp(Transform parent, Vector3 position, bool withLight)
        {
            Transform root = new GameObject("Lamp").transform;
            root.SetParent(parent);
            GameObject post = Art(root, "Lamp_Post", position, 0f, box: false);
            if (post == null) return;
            var pole = post.AddComponent<CapsuleCollider>();
            pole.radius = 0.12f;
            pole.height = 3.4f;
            pole.center = new Vector3(0f, 1.7f, 0f);
            if (!withLight) return;
            // Only a few real lights, to stay cheap on the headset.
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root);
            light.transform.position = post.transform.position + Vector3.up * 3.12f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.intensity = 1.8f;
            light.range = 9f;
            light.shadows = LightShadows.None;
        }

        // ---------------------------------------------------------------- Atmosphere

        /// <summary>
        /// Night in the plaza: a cold moon, a dark sky, blue-violet fog that swallows the streets and rooftops, four
        /// braziers with fire round the centre, drifting embers and a low creeping mist. Every effect is an authored
        /// prefab from Prefabs/Effects (Roguelike > Build Effect Prefabs); this only places them.
        /// </summary>
        static void BuildAtmosphere(Transform parent)
        {
            // Fog hides everything past the buildings, so the plaza feels closed in.
            SceneAtmosphere.Night(12f, 48f);

            // Braziers between the stations, lighting the centre with real fire.
            foreach (float degrees in new[] { 45f, 135f, 225f, 315f })
            {
                GameObject brazier = Art(parent, "Brazier", Around(degrees, CentreRadius - 1.2f), degrees);
                if (brazier != null) SceneAtmosphere.Effect("Ambient_Brazier", brazier.transform, brazier.transform.position + Vector3.up * 1.03f);
            }

            SceneAtmosphere.Effect("Ambient_Motes", parent, OnGround(Vector3.zero));
            SceneAtmosphere.Effect("Ambient_Mist", parent, OnGround(Vector3.zero));
        }

        /// <summary>
        /// Colonial facades around the square, fronts facing it: Casa de la Libertad on the west, the Gobernacion on
        /// the south, the Cathedral on the east, arcade rows on the north. Background only.
        /// </summary>
        static void BuildSurroundings(Transform parent)
        {
            Art(parent, "Facade_CasaDeLaLibertad", new Vector3(-FacadeLine, 0f, 0f), 90f);
            Art(parent, "Facade_Gobernacion", new Vector3(0f, 0f, -FacadeLine), 0f);
            Art(parent, "Facade_Cathedral", new Vector3(FacadeLine, 0f, -6f), 270f);
            Art(parent, "Facade_ArcadeRow", new Vector3(FacadeLine, 0f, 21.3f), 270f);
            // The two north rows overlap slightly so no sky shows through the seam behind the gate.
            foreach (float x in new[] { -12.1f, 12.1f }) Art(parent, "Facade_ArcadeRow", new Vector3(x, 0f, FacadeLine), 180f);
        }

        // ---------------------------------------------------------------- Stations

        static void BuildStations(Transform parent)
        {
            // Mentor: at the foot of the monument, facing the player as they come in.
            HubNpc mentor = Npc(parent, "Mentor", new Vector3(0f, 0f, -4.4f), 180f, Color.white);

            // Status board: just inside the centre, to the right of the arrival path, turned towards it.
            Vector3 statusSpot = Around(157.5f, 9f);
            Signboard status = Board(parent, "Status Board", OnGround(statusSpot) + Vector3.up * 1.45f,
                BoardYaw(statusSpot, Spawn + Vector3.forward * 4f), new Vector2(1.3f, 1.0f), "Level 0", 1.1f, posts: true);

            // Armory: one wall rack left of the arrival path, facing the path. Stored weapons hang on its four hooks.
            Vector3 rackSpot = Around(202.5f, StationRing + 0.6f);
            float rackYaw = Facing(rackSpot, new Vector3(0f, 0f, rackSpot.z + 1.5f));
            GameObject rack = Art(parent, "Weapon_Rack", rackSpot, rackYaw);
            var slots = new List<Holster>();
            float[] hooks = { -0.78f, -0.26f, 0.26f, 0.78f };
            for (int i = 0; i < hooks.Length; i++)
            {
                var slot = new GameObject("Armory Slot " + (i + 1)).transform;
                slot.SetParent(parent);
                slot.position = rack.transform.position + Quaternion.Euler(0f, rackYaw, 0f) * new Vector3(hooks[i], 1.75f, 0.13f);
                // Weapons hang point-down.
                slot.rotation = Quaternion.Euler(90f, 0f, 0f);
                slots.Add(slot.gameObject.AddComponent<Holster>());
            }
            Board(parent, "Armory Sign", new Vector3(rack.transform.position.x, Top(rack) + 0.2f, rack.transform.position.z), rackYaw + 180f,
                new Vector2(1.5f, 0.36f), "ARMORY\n<size=55%>what you leave here is safe</size>", 1.6f);

            // Vendor: a stall facing the monument. The stand's customer side is its +Z; the vendor stands behind.
            HubNpc vendor = Stall(parent, "Vendor", 67.5f, "VENDOR\n<size=55%>weapons and trinkets</size>", new Color(1f, 0.85f, 0.6f), out Transform vendorStand);
            var spawnPoints = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var spawn = new GameObject("Vendor Spawn " + (i + 1)).transform;
                spawn.SetParent(parent);
                spawn.SetPositionAndRotation(vendorStand.TransformPoint(new Vector3(0.8f - i * 0.8f, 0f, 0.1f)) + Vector3.up * 0.95f,
                    vendorStand.rotation * Quaternion.Euler(0f, -90f, 0f));
                spawnPoints.Add(spawn);
            }

            // Armorer: a stall like the vendor's, with a stand of armor beside it.
            HubNpc armorer = Stall(parent, "Armorer", 247.5f, "ARMORER\n<size=55%>armor and trinkets</size>", new Color(0.85f, 0.6f, 0.45f), out Transform forge);
            Vector3 standSpot = forge.TransformPoint(new Vector3(-2.5f, 0f, 0.2f));
            Block(parent, "Armor Stand", standSpot, forge.eulerAngles.y, new Vector3(0.08f, 1.6f, 0.08f), dark, false);
            Block(parent, "Armor Stand Torso", standSpot + Vector3.up * 1.1f, forge.eulerAngles.y, new Vector3(0.45f, 0.55f, 0.25f), bronze, false);

            // Quartermaster: keeps every weapon that does not fit on the rack, behind a counter.
            Vector3 counterSpot = Around(112.5f, StationRing);
            float counterYaw = Facing(counterSpot, Vector3.zero);
            Quaternion counterTurn = Quaternion.Euler(0f, counterYaw, 0f);
            Block(parent, "Quartermaster Counter", counterSpot, counterYaw, new Vector3(1.8f, 0.9f, 0.7f), dark);
            var handOver = new GameObject("Quartermaster Spawn").transform;
            handOver.SetParent(parent);
            handOver.SetPositionAndRotation(OnGround(counterSpot) + Vector3.up * 0.95f, counterTurn);
            HubNpc quartermaster = Npc(parent, "Quartermaster", counterSpot - counterTurn * Vector3.forward * 0.9f, counterYaw, new Color(0.8f, 0.8f, 0.65f));
            Vector3 qmSign = counterSpot + counterTurn * new Vector3(1.5f, 0f, -0.2f);
            Board(parent, "Quartermaster Sign", OnGround(qmSign) + Vector3.up * 1.85f, BoardYaw(qmSign, Vector3.zero), new Vector2(1.3f, 0.5f),
                "QUARTERMASTER\n<size=55%>stored weapons</size>", 1.3f, posts: true);

            // Mission gate: at the end of the north path, its platform (the circle to stand in) on the path.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Mission Gate";
            ring.transform.SetParent(parent);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.position = OnGround(GateCircle) + Vector3.up * 0.02f;
            ring.transform.localScale = new Vector3(3f, 0.02f, 3f);
            ring.GetComponent<MeshRenderer>().sharedMaterial = portal;
            // The gate model's round platform sits 1.9 m in front of its pivot.
            Art(parent, "Mission_Gate", GateCircle + Vector3.forward * 1.9f, 180f);
            Vector3 gateSignSpot = Around(337.5f, CentreRadius - 1.6f);
            Signboard gateSign = Board(parent, "Gate Sign", OnGround(gateSignSpot) + Vector3.up * 1.35f, BoardYaw(gateSignSpot, Vector3.zero),
                new Vector2(1.5f, 0.85f), "MISSION", 1.2f, posts: true);

            // Mission board: the scout beside a notice board, right of the gate path.
            Vector3 boardSpot = Around(22.5f, StationRing + 0.5f);
            float boardYaw = Facing(boardSpot, Vector3.zero);
            GameObject notice = Art(parent, "Notice_Board", boardSpot, boardYaw);
            Board(parent, "Mission Board Sign", new Vector3(boardSpot.x, Top(notice) + 0.2f, boardSpot.z), boardYaw + 180f,
                new Vector2(1.2f, 0.34f), "MISSIONS", 1.6f, twoSided: true);
            Vector3 scoutSpot = boardSpot + Quaternion.Euler(0f, boardYaw, 0f) * new Vector3(-1.3f, 0f, 0.8f);
            HubNpc scout = Npc(parent, "Scout", scoutSpot, Facing(scoutSpot, Vector3.zero), new Color(0.7f, 0.95f, 0.75f));

            // A fountain fills the last side of the centre.
            Art(parent, "Fountain", Around(292.5f, StationRing - 0.3f), 0f);

            var gate = ring.AddComponent<ExtractionZone>();
            var gateData = new SerializedObject(gate);
            gateData.FindProperty("ring").objectReferenceValue = ring.GetComponent<MeshRenderer>();
            gateData.FindProperty("openTime").floatValue = 4f;
            gateData.FindProperty("openingText").stringValue = "Entering mission...";
            gateData.FindProperty("cancelledText").stringValue = "Stayed in the plaza";
            gateData.ApplyModifiedPropertiesWithoutUndo();

            var overflow = new GameObject("Armory Overflow").transform;
            overflow.SetParent(parent);
            overflow.position = handOver.position;

            var manager = new GameObject("Hub Manager").AddComponent<HubManager>();
            var data = new SerializedObject(manager);
            var rackSlots = data.FindProperty("rackSlots");
            rackSlots.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++) rackSlots.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            data.FindProperty("overflowPoint").objectReferenceValue = overflow;
            data.FindProperty("vendor").objectReferenceValue = vendor;
            data.FindProperty("mentor").objectReferenceValue = mentor;
            data.FindProperty("missionGate").objectReferenceValue = gate;
            data.FindProperty("statusSign").objectReferenceValue = status;
            data.FindProperty("gateSign").objectReferenceValue = gateSign;
            data.FindProperty("armorer").objectReferenceValue = armorer;
            data.FindProperty("quartermaster").objectReferenceValue = quartermaster;
            data.FindProperty("quartermasterSpawnPoint").objectReferenceValue = handOver;
            data.FindProperty("missionBoard").objectReferenceValue = scout;
            var spawnList = data.FindProperty("vendorSpawnPoints");
            spawnList.arraySize = spawnPoints.Count;
            for (int i = 0; i < spawnPoints.Count; i++) spawnList.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A market stall on the station ring at an angle, facing the monument, with its keeper behind the counter and
        /// a name board hanging under the front of the awning.
        /// </summary>
        static HubNpc Stall(Transform parent, string keeper, float degrees, string sign, Color tint, out Transform stand)
        {
            Vector3 spot = Around(degrees, StationRing + 0.3f);
            float yaw = Facing(spot, Vector3.zero);
            GameObject stall = Art(parent, "Vendor_Stand", spot, yaw);
            stand = stall.transform;
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            Board(parent, keeper + " Sign", stall.transform.position + turn * new Vector3(0f, 2.05f, 0.88f), yaw + 180f,
                new Vector2(1.3f, 0.42f), sign, 1.4f, twoSided: true, ropes: 0.3f);
            return Npc(parent, keeper, spot - turn * Vector3.forward * 0.95f, yaw, tint);
        }

        /// <summary>
        /// A character to talk to. The Vendor and Mentor have their own models; the others borrow the vendor's body
        /// with a tinted copy of its material until they get models of their own (ASSETS_NEEDED.md).
        /// </summary>
        static HubNpc Npc(Transform parent, string name, Vector3 position, float yaw, Color tint)
        {
            string body = name == "Mentor" ? "Mentor" : "Vendor";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtImporter.NpcFolder + "/" + body + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(OnGround(position), Quaternion.Euler(0f, yaw, 0f));

            if (name != body)
            {
                string materialPath = MaterialFolder + "Npc_" + name + ".mat";
                var source = AssetDatabase.LoadAssetAtPath<Material>(ArtImporter.NpcFolder + "/" + body + ".mat");
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null && source != null)
                {
                    material = new Material(source);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                if (material != null)
                {
                    if (source != null) material.CopyPropertiesFromMaterial(source);
                    material.SetColor("_BaseColor", tint);
                    EditorUtility.SetDirty(material);
                    foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var materials = skin.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) materials[i] = material;
                        skin.sharedMaterials = materials;
                    }
                }
            }
            // The clips carry a one-triangle helper mesh; never show it.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("Clip_Anchor")) r.enabled = false;

            var animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = NpcController();
                animator.applyRootMotion = false;
            }

            // Solid, so the player cannot walk through them.
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 1.8f;

            var npc = go.AddComponent<HubNpc>();
            var data = new SerializedObject(npc);
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("animator").objectReferenceValue = animator;
            data.FindProperty("greetState").stringValue = "Npc_Greet";
            data.ApplyModifiedPropertiesWithoutUndo();
            return npc;
        }

        /// <summary>
        /// The hub characters' Animator: idle by default; greet, trade and bless play once and return to idle;
        /// talk loops while their menu is open (started and stopped from code).
        /// </summary>
        static UnityEditor.Animations.AnimatorController NpcController()
        {
            string path = ArtImporter.NpcFolder + "/Npc.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (controller != null) return controller;

            controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Npc_Idle");
            idle.motion = NpcClip("Npc_Idle");
            machine.defaultState = idle;
            var talk = machine.AddState("Npc_Talk");
            talk.motion = NpcClip("Npc_Talk");

            foreach (string once in new[] { "Npc_Greet", "Npc_Trade", "Npc_Bless" })
            {
                var state = machine.AddState(once);
                state.motion = NpcClip(once);
                var back = state.AddTransition(idle);
                back.hasExitTime = true;
                back.exitTime = 0.92f;
                back.duration = 0.2f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimationClip NpcClip(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ArtImporter.NpcFolder + "/Animations/Npc@" + name + ".fbx"))
            {
                var clip = asset as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__preview")) return clip;
            }
            Debug.LogWarning("[HubBuilder] Missing NPC clip " + name);
            return null;
        }

        // ---------------------------------------------------------------- Signs

        /// <summary>The yaw that turns a board's printed front (its -Z) towards someone standing at a point.</summary>
        static float BoardYaw(Vector3 board, Vector3 reader)
        {
            Vector3 d = reader - board;
            return Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// A painted wooden sign with a bronze trim: a real object in the world instead of text floating in the air.
        /// Its front faces the board's -Z. Two-sided boards print the text on the back too (each face reads the
        /// right way round). Posts stand it on the ground; ropes hang it from whatever is above.
        /// </summary>
        static Signboard Board(Transform parent, string name, Vector3 centre, float yaw, Vector2 size, string text,
            float maxFont, bool twoSided = false, bool posts = false, float ropes = 0f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent);
            root.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));

            BoardPart(root, PrimitiveType.Cube, "Trim", Vector3.zero, new Vector3(size.x + 0.06f, size.y + 0.06f, 0.04f), signTrim);
            BoardPart(root, PrimitiveType.Cube, "Board", Vector3.zero, new Vector3(size.x, size.y, 0.06f), signWood);
            float postX = size.x * 0.5f - 0.08f;
            if (posts)
            {
                float groundY = OnGround(centre).y;
                float height = centre.y - groundY + size.y * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                    BoardPart(root, PrimitiveType.Cube, "Post", new Vector3(side * postX, size.y * 0.5f - height * 0.5f, 0.05f), new Vector3(0.08f, height, 0.08f), signWood);
            }
            if (ropes > 0f)
            {
                foreach (float side in new[] { -1f, 1f })
                    BoardPart(root, PrimitiveType.Cylinder, "Rope", new Vector3(side * postX, size.y * 0.5f + ropes * 0.5f, 0f), new Vector3(0.025f, ropes * 0.5f, 0.025f), dark);
            }

            TMPro.TMP_Text front = BoardText(root, size, maxFont, text, false);
            TMPro.TMP_Text back = twoSided ? BoardText(root, size, maxFont, text, true) : null;
            var sign = root.gameObject.AddComponent<Signboard>();
            sign.Bind(front, back);
            foreach (Transform t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
            return sign;
        }

        static void BoardPart(Transform root, PrimitiveType type, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static TMPro.TMP_Text BoardText(Transform root, Vector2 size, float maxFont, string text, bool back)
        {
            var go = new GameObject(back ? "Text Back" : "Text Front");
            go.transform.SetParent(root, false);
            // TextMeshPro reads correctly when seen from its -Z side; the back copy is turned round to face +Z.
            go.transform.localPosition = new Vector3(0f, 0f, back ? 0.032f : -0.032f);
            go.transform.localRotation = back ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            var label = go.AddComponent<TMPro.TextMeshPro>();
            label.rectTransform.sizeDelta = new Vector2(size.x - 0.12f, size.y - 0.08f);
            label.text = text;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.3f;
            label.fontSizeMax = maxFont;
            label.color = new Color(1f, 0.92f, 0.72f);
            return label;
        }

        /// <summary>The highest point of a placed model, to set a sign on top of it.</summary>
        static float Top(GameObject go)
        {
            float top = go.transform.position.y;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.enabled) top = Mathf.Max(top, r.bounds.max.y);
            return top;
        }

        // ---------------------------------------------------------------- Materials

        static void CreateMaterials()
        {
            dark = Lit("Hub_Dark", new Color(0.2f, 0.2f, 0.22f));
            bronze = Lit("Hub_Bronze", new Color(0.3f, 0.34f, 0.3f));
            portal = Lit("Hub_Gate", new Color(0.1f, 0.5f, 0.6f));
            signWood = Lit("Hub_SignWood", new Color(0.2f, 0.13f, 0.08f));
            signTrim = Lit("Hub_SignTrim", new Color(0.55f, 0.42f, 0.2f));
        }

        static Material Lit(string name, Color color)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
