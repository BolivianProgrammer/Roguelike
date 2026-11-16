using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// Repairs the forest art set (Art/Forest/Models) as it is imported. The Blender script that generated it wrote
    /// every box and cone inside-out (cylinders came out right), and the mounds and stream water facing downwards.
    /// Inside-out faces are culled from the outside, so tree canopies looked see-through and walls showed holes,
    /// and the matching *_Collision meshes pushed the player and enemies into geometry instead of out of it.
    ///
    /// Each mesh is split into its connected pieces and every piece is checked on its own:
    /// - a ground-like piece (open, mostly flat) must face up;
    /// - any other piece must enclose positive volume (its faces point away from its middle).
    /// Pieces that fail are flipped (winding reversed, normals negated). This runs only at import, so it costs the
    /// game nothing. Bump Version to force a reimport after changing the rules.
    /// </summary>
    public class ForestMeshFixer : AssetPostprocessor
    {
        const string Folder = ArtImporter.ForestArtFolder + "/Models/";
        const uint Version = 1;

        public override uint GetVersion() => Version;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            int flipped = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) flipped += Fix(filter.sharedMesh);
            if (flipped > 0) Debug.Log($"[ForestMeshFixer] {System.IO.Path.GetFileName(assetPath)}: turned {flipped} inside-out piece(s) the right way out.");
        }

        /// <summary>Flips every inside-out piece of the mesh. Returns how many pieces were flipped.</summary>
        static int Fix(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int flipped = 0;
            var flipVertex = new bool[vertices.Length];
            int[] weld = Weld(vertices);

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = mesh.GetTriangles(sub);
                bool changed = false;
                foreach (List<int> piece in Pieces(triangles, weld))
                {
                    if (!InsideOut(piece, triangles, vertices, weld)) continue;
                    foreach (int t in piece)
                    {
                        (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
                        flipVertex[triangles[t]] = flipVertex[triangles[t + 1]] = flipVertex[triangles[t + 2]] = true;
                    }
                    changed = true;
                    flipped++;
                }
                if (changed) mesh.SetTriangles(triangles, sub, false);
            }
            if (flipped == 0) return 0;

            if (normals.Length == vertices.Length)
            {
                for (int i = 0; i < normals.Length; i++)
                    if (flipVertex[i]) normals[i] = -normals[i];
                mesh.normals = normals;
            }
            else mesh.RecalculateNormals();
            if (mesh.uv.Length == vertices.Length) mesh.RecalculateTangents();
            return flipped;
        }

        /// <summary>Gives vertices at the same position (to a millimetre) the same id, so seams do not split pieces.</summary>
        static int[] Weld(Vector3[] vertices)
        {
            var ids = new Dictionary<Vector3Int, int>();
            var weld = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i] * 1000f;
                var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (!ids.TryGetValue(key, out int id)) ids[key] = id = ids.Count;
                weld[i] = id;
            }
            return weld;
        }

        /// <summary>Groups triangles (by their first index) into connected pieces.</summary>
        static IEnumerable<List<int>> Pieces(int[] triangles, int[] weld)
        {
            int count = 0;
            foreach (int w in weld) count = Mathf.Max(count, w + 1);
            var parent = new int[count];
            for (int i = 0; i < count; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) x = parent[x] = parent[parent[x]];
                return x;
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = Find(weld[triangles[t]]);
                parent[Find(weld[triangles[t + 1]])] = a;
                parent[Find(weld[triangles[t + 2]])] = a;
            }
            var pieces = new Dictionary<int, List<int>>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int root = Find(weld[triangles[t]]);
                if (!pieces.TryGetValue(root, out List<int> list)) pieces[root] = list = new List<int>();
                list.Add(t);
            }
            return pieces.Values;
        }

        static bool InsideOut(List<int> piece, int[] triangles, Vector3[] vertices, int[] weld)
        {
            // Centre, bounds and whether the piece is closed (every edge shared by exactly two triangles).
            Vector3 centre = Vector3.zero;
            var bounds = new Bounds(vertices[triangles[piece[0]]], Vector3.zero);
            var edges = new Dictionary<long, int>();
            foreach (int t in piece)
                for (int e = 0; e < 3; e++)
                {
                    Vector3 v = vertices[triangles[t + e]];
                    centre += v;
                    bounds.Encapsulate(v);
                    int a = weld[triangles[t + e]], b = weld[triangles[t + (e + 1) % 3]];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    edges.TryGetValue(key, out int n);
                    edges[key] = n + 1;
                }
            centre /= piece.Count * 3;
            bool closed = true;
            foreach (int n in edges.Values)
                if (n != 2) { closed = false; break; }

            float volume = 0f, area = 0f;
            Vector3 facing = Vector3.zero;
            foreach (int t in piece)
            {
                Vector3 a = vertices[triangles[t]] - centre, b = vertices[triangles[t + 1]] - centre, c = vertices[triangles[t + 2]] - centre;
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                facing += cross * 0.5f;
                area += cross.magnitude * 0.5f;
            }
            if (area < 1e-8f) return false;

            // Open, mostly level pieces (ground, mounds, water, paths) must face the sky.
            float up = facing.y / area;
            if (!closed && Mathf.Abs(up) > 0.5f) return up < 0f;

            // Everything else must enclose its volume. Flat cards (banners, decals) enclose none and are left alone.
            Vector3 size = bounds.size;
            float boxVolume = size.x * size.y * size.z;
            return boxVolume > 1e-9f && volume / boxVolume < -0.05f;
        }
    }
}
