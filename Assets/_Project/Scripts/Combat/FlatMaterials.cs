using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Unlit, colour-only materials for markers, menus, beams and effects made in code.
    /// The material comes from a real asset (Resources/FlatColor) so its shader is always included in a Quest
    /// build; looking a shader up by name at runtime can fail on device when nothing else uses that shader.
    /// </summary>
    public static class FlatMaterials
    {
        static Material shared;

        /// <summary>One material shared by everything that tints itself per object (with property blocks or vertex colour).</summary>
        public static Material Shared
        {
            get
            {
                if (shared != null) return shared;
                shared = Resources.Load<Material>("FlatColor");
                if (shared == null)
                {
                    // Fallback for the editor if the asset is missing.
                    var shader = Shader.Find("Sprites/Default");
                    if (shader != null) shared = new Material(shader);
                }
                return shared;
            }
        }

        /// <summary>A new material of one colour, for objects that need their own.</summary>
        public static Material Create(Color color)
        {
            var material = Shared != null ? new Material(Shared) : new Material(Shader.Find("Sprites/Default"));
            material.color = color;
            return material;
        }
    }
}
