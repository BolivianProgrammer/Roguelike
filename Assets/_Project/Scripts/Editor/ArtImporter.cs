using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Sets up how the character and weapon art is imported: Humanoid rigs, which clips loop, and materials
    /// that use the supplied textures. Run from the menu (Roguelike > Configure Art Imports) whenever the
    /// model or animation files are replaced with new versions.
    /// </summary>
    public static class ArtImporter
    {
        public const string RevenantFolder = "Assets/_Project/Art/Enemies/Revenant";
        public const string BossFolder = "Assets/_Project/Art/Enemies/Boss";
        public const string WeaponFolder = "Assets/_Project/Art/Weapons";
        public const string NpcFolder = "Assets/_Project/Art/NPC";
        public const string HubArtFolder = "Assets/_Project/Art/Hub";
        public const string ForestArtFolder = "Assets/_Project/Art/Forest";

        static readonly HashSet<string> Looping = new HashSet<string>
        {
            "Idle", "Walk", "Run", "Stunned", "Unarmed_Idle", "Crawl", "Strafe_Left", "Strafe_Right", "Walk_Back",
            "Turn_Left", "Turn_Right", "Bisected_Crawl", "Idle_Unaware", "Boss_Idle", "Boss_Walk", "Boss_Stunned",
            "Npc_Idle", "Npc_Talk",
        };

        [MenuItem("Roguelike/Configure Art Imports")]
        public static void Configure()
        {
            Material revenant = CharacterMaterial(RevenantFolder, "Revenant");
            Material boss = CharacterMaterial(BossFolder, "Boss");

            Avatar revenantAvatar = ConfigureCharacter(RevenantFolder + "/Revenant.fbx", revenant);
            ConfigureCharacter(BossFolder + "/Boss.fbx", boss);

            // Every clip, including the boss's, was animated on the Revenant's skeleton.
            ConfigureClips(RevenantFolder + "/Animations", revenantAvatar);
            ConfigureClips(BossFolder + "/Animations", revenantAvatar);

            ConfigureWeapons();

            // Hub characters: their own textures, the shared humanoid skeleton.
            Avatar npcAvatar = ConfigureCharacter(NpcFolder + "/Vendor.fbx", CharacterMaterial(NpcFolder, "Vendor"));
            ConfigureCharacter(NpcFolder + "/Mentor.fbx", CharacterMaterial(NpcFolder, "Mentor"));
            ConfigureClips(NpcFolder + "/Animations", npcAvatar);

            ConfigureHubArt();
            ConfigureForestArt();
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtImporter] Art imports configured.");
        }

        static Avatar ConfigureCharacter(string path, Material material)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[ArtImporter] Missing model: " + path);
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // Dismemberment reads mesh data and moves bones at runtime.
            importer.optimizeGameObjects = false;
            RemapMaterials(importer, material);
            importer.SaveAndReimport();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar) return avatar;
            return null;
        }

        static void ConfigureClips(string folder, Avatar avatar)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                int at = file.IndexOf('@');
                if (at < 0) continue;
                string clipName = file.Substring(at + 1);

                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                // The clip files share the model's node tree (Armature > Hips > ...), so they use the model's own
                // avatar. (Older clip files lacked the Armature node and had to build their own.)
                if (avatar != null)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = avatar;
                }
                else
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.sourceAvatar = null;
                }
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.clipAnimations = new ModelImporterClipAnimation[0];
                importer.SaveAndReimport();

                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = clipName;
                    clip.loopTime = Looping.Contains(clipName);
                    // In place: the game code moves the character, the clip only poses it.
                    clip.lockRootRotation = true;
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static Material CharacterMaterial(string folder, string name)
        {
            string normalPath = folder + "/Textures/" + name + "_Normal.png";
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Textures/" + name + "_BaseColor.png"));
            material.SetColor("_BaseColor", Color.white);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureWeapons()
        {
            string palettePath = WeaponFolder + "/Textures/Weapons_Palette.png";
            var paletteImporter = AssetImporter.GetAtPath(palettePath) as TextureImporter;
            if (paletteImporter != null)
            {
                // Flat colour swatches: no blending between neighbours.
                paletteImporter.filterMode = FilterMode.Point;
                paletteImporter.mipmapEnabled = false;
                paletteImporter.SaveAndReimport();
            }

            string materialPath = WeaponFolder + "/Weapons.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(palettePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { WeaponFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                RemapMaterials(importer, material);
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// The plaza set: every prop shares one palette material; the ground has tiled paving and grass; the
        /// fountain water and lamp glass have their own. Materials live in Art/Hub/Materials.
        /// </summary>
        static void ConfigureHubArt()
        {
            string textures = HubArtFolder + "/Textures/";
            var palette = AssetImporter.GetAtPath(textures + "Hub_Palette.png") as TextureImporter;
            if (palette != null)
            {
                palette.filterMode = FilterMode.Point;
                palette.mipmapEnabled = false;
                palette.textureCompression = TextureImporterCompression.Uncompressed;
                palette.SaveAndReimport();
            }
            foreach (string normal in new[] { "Paving_Normal.png", "Grass_Normal.png" })
            {
                var importer = AssetImporter.GetAtPath(textures + normal) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }

            string folder = HubArtFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(HubArtFolder, "Materials");
            var map = new Dictionary<string, Material>
            {
                { "Hub_Mat", LitMaterial(folder + "/Hub_Mat.mat", textures + "Hub_Palette.png", null, 0.15f) },
                { "Hub_Paving", LitMaterial(folder + "/Hub_Paving.mat", textures + "Paving_BaseColor.png", textures + "Paving_Normal.png", 0.25f) },
                { "Hub_Grass", LitMaterial(folder + "/Hub_Grass.mat", textures + "Grass_BaseColor.png", textures + "Grass_Normal.png", 0.05f) },
            };

            var water = LitMaterial(folder + "/Hub_Water.mat", null, null, 0.9f);
            water.SetColor("_BaseColor", new Color(0.05f, 0.22f, 0.25f, 0.75f));
            MakeTransparent(water);
            map["Hub_Water"] = water;

            var glass = LitMaterial(folder + "/Hub_Glass.mat", null, null, 0.5f);
            glass.SetColor("_BaseColor", new Color(1f, 0.85f, 0.5f));
            glass.EnableKeyword("_EMISSION");
            glass.SetColor("_EmissionColor", new Color(1f, 0.75f, 0.4f) * 2f);
            glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            map["Hub_Glass"] = glass;

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { HubArtFolder + "/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is Material embedded)) continue;
                    // Anything not named in the art notes uses the palette.
                    Material target = map.TryGetValue(embedded.name, out Material named) ? named : map["Hub_Mat"];
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), target);
                }
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// The forest set (Art/Forest): one palette material for almost everything, double-sided cloth, tiled grass,
        /// soft-edged dirt paths (they fade out through their vertex alpha), see-through water, and unlit glowing parts
        /// (totem crystals, portal runes, shrine glyph) that the game tints at runtime. Materials live in Art/Forest/Materials.
        /// </summary>
        public static void ConfigureForestArt()
        {
            string textures = ForestArtFolder + "/Textures/";
            var palette = AssetImporter.GetAtPath(textures + "Forest_Palette.png") as TextureImporter;
            if (palette != null)
            {
                palette.filterMode = FilterMode.Point;
                palette.mipmapEnabled = false;
                palette.textureCompression = TextureImporterCompression.Uncompressed;
                palette.SaveAndReimport();
            }
            foreach (string normal in new[] { "Grass_Normal.png", "Dirt_Normal.png" })
            {
                var importer = AssetImporter.GetAtPath(textures + normal) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }

            string folder = ForestArtFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(ForestArtFolder, "Materials");
            var map = new Dictionary<string, Material>();
            map["Forest_Mat"] = LitMaterial(folder + "/Forest_Mat.mat", textures + "Forest_Palette.png", null, 0.1f);

            var cloth = LitMaterial(folder + "/Forest_Cloth.mat", textures + "Forest_Palette.png", null, 0.05f);
            cloth.SetFloat("_Cull", 0f); // banners and tent canvas are single sheets
            cloth.doubleSidedGI = true;
            map["Forest_Cloth"] = cloth;

            map["Forest_Grass"] = LitMaterial(folder + "/Forest_Grass.mat", textures + "Grass_BaseColor.png", textures + "Grass_Normal.png", 0.05f);

            // Paths: URP's particle shader is the one that reads vertex alpha, which feathers the edges into the grass.
            var dirt = MaterialAt(folder + "/Forest_Dirt.mat", "Universal Render Pipeline/Particles/Simple Lit");
            dirt.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "Dirt_BaseColor.png"));
            dirt.SetColor("_BaseColor", Color.white);
            MakeTransparent(dirt);
            dirt.SetFloat("_ColorMode", 0f);
            dirt.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;
            map["Forest_Dirt"] = dirt;

            var water = LitMaterial(folder + "/Forest_Water.mat", null, null, 0.92f);
            water.SetColor("_BaseColor", new Color(0.05f, 0.12f, 0.16f, 0.8f));
            MakeTransparent(water);
            map["Forest_Water"] = water;

            map["Totem_Glow"] = Glow(folder + "/Totem_Glow.mat", new Color(0.7f, 0.35f, 1f));
            map["Portal_Runes"] = Glow(folder + "/Portal_Runes.mat", new Color(0.3f, 0.9f, 1f));
            map["Shrine_Glow"] = Glow(folder + "/Shrine_Glow.mat", new Color(1f, 0.3f, 0.2f));
            map["Shard_Glow"] = Glow(folder + "/Shard_Glow.mat", Color.white);

            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ForestArtFolder + "/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is Material embedded)) continue;
                    // Blender numbers repeated names ("Forest_Mat.002"); the name before the dot picks the material.
                    string key = embedded.name.Split('.')[0];
                    Material target = map.TryGetValue(key, out Material named) ? named : map["Forest_Mat"];
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), target);
                }
                importer.SaveAndReimport();
            }
        }

        /// <summary>An unlit colour: reads as glowing in the dark, and the game can tint it with a property block.</summary>
        static Material Glow(string path, Color color)
        {
            var material = MaterialAt(path, "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialAt(string path, string shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader.name != shader) material.shader = Shader.Find(shader);
            return material;
        }

        static Material LitMaterial(string path, string baseMap, string normalMap, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (baseMap != null) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMap));
            material.SetColor("_BaseColor", Color.white);
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalMap));
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP Lit set to Transparent (alpha blended), as the Inspector would.</summary>
        static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
        }

        /// <summary>Points every material slot of a model at one of our own materials.</summary>
        static void RemapMaterials(ModelImporter importer, Material material)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(importer.assetPath))
            {
                if (!(asset is Material embedded)) continue;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }
        }
    }
}
