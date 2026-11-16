# Mission Forest — first art pass

46 assets from ASSETS_NEEDED.md section 5. Asset creation only; no scenes, prefabs, gameplay or Unity import settings changed.

## Delivery
One FBX per asset. Units metres; FBX -Z forward / Y up. Coordinates below are Unity X × Y × Z. Props use ground-centre pivots except Banner (top), Drawbridge (hinge), Loot_Shard (centre), and named articulated children.

Shared Forest_Mat uses Textures/Forest_Palette.png: Point filtering recommended. Forest_Cloth uses the same palette. Grass and Dirt base colour/normal pairs are 1024² seamless textures, OpenGL tangent normals, tiles 4 m and 2 m respectively. Normal textures must be imported as Normal Map.

Forest_Water is a separate surface/material; configure URP transparency when integrating. Totem_Glow, Portal_Runes, Shrine_Glow and Shard_Glow are distinct emission materials. FBX cannot configure URP emission/transparency automatically. Path edge alpha is stored in vertex Color; the engine material must read it for soft edges.

Collision meshes are named *_Collision, excluded from render previews. Disable their renderers and attach appropriate MeshColliders during integration. Multiple disconnected convex pieces can be split for physics if required. Budgets below count visible triangles separately.

| File | Visible tris / budget | Collision tris | Size X × Y × Z (m) | Collision / contract |
|---|---:|---:|---|---|
| Tree_Pine_A.fbx | 92 / 600 | 28 | 3.259 × 9.0 × 3.309 | Tree_Pine_Collision: trunk only, r 0.3 m, height 3 m. |
| Tree_Pine_B.fbx | 92 / 600 | 28 | 3.879 × 9.0 × 3.939 | Tree_Pine_Collision: trunk only, r 0.3 m, height 3 m. |
| Tree_Pine_C.fbx | 124 / 600 | 28 | 3.19 × 9.036 × 2.482 | Tree_Pine_Collision: trunk only, r 0.3 m, height 3 m. |
| Tree_Oak_A.fbx | 396 / 800 | 28 | 5.651 × 8.05 × 6.06 | Tree_Oak_Collision: trunk only, r 0.4 m, height 3 m. |
| Tree_Oak_B.fbx | 396 / 800 | 28 | 5.716 × 8.05 × 5.921 | Tree_Oak_Collision: trunk only, r 0.4 m, height 3 m. |
| Bush_A.fbx | 72 / 150 | 0 | 1.456 × 0.8 × 1.252 | None (foliage). |
| Bush_B.fbx | 72 / 150 | 0 | 1.438 × 0.8 × 1.265 | None (foliage). |
| Fern.fbx | 48 / 150 | 0 | 1.16 × 0.24 × 1.16 | None (foliage). |
| Ground_Mound_A.fbx | 36 / 150 | 36 | 8.0 × 1.4 × 7.2 | Simplified walkable mound; slopes below 25 degrees, base -0.3 m. |
| Ground_Mound_B.fbx | 36 / 150 | 36 | 9.6 × 1.55 × 8.64 | Simplified walkable mound; slopes below 25 degrees, base -0.3 m. |
| Ground_Mound_C.fbx | 36 / 150 | 36 | 12.0 × 1.7 × 10.8 | Simplified walkable mound; slopes below 25 degrees, base -0.3 m. |
| Rock_A.fbx | 28 / 120 | 0 | 1.457 × 0.8 × 1.273 | Box collider is enough. |
| Rock_B.fbx | 28 / 120 | 24 | 1.293 × 1.2 × 1.265 | Simplified rock collision mesh. |
| Rock_C.fbx | 72 / 120 | 24 | 1.381 × 0.8 × 1.156 | Simplified rock collision mesh. |
| Path_Dirt_Straight.fbx | 36 / 80 | 0 | 3.5 × 0.0 × 6.0 | None. Edge vertex alpha in color layer; use vertex-alpha material to feather. |
| Stream_Straight.fbx | 98 / 250 | 36 | 3.0 × 0.505 × 6.0 | Bed and banks collision; separate Forest_Water surface has no collision. |
| Path_Dirt_Bend.fbx | 36 / 80 | 0 | 4.801 × 0.0 × 6.605 | None. Edge vertex alpha in color layer; use vertex-alpha material to feather. |
| Stream_Bend.fbx | 98 / 250 | 36 | 4.334 × 0.505 × 6.48 | Bed and banks collision; separate Forest_Water surface has no collision. |
| Log_Fallen.fbx | 80 / 150 | 28 | 3.0 × 0.766 × 0.756 | Log_Fallen_Collision: low-poly capsule-like trunk. |
| Palisade_Section.fbx | 172 / 200 | 0 | 3.0 × 2.6 × 0.445 | Box collider is enough. |
| Palisade_Gate.fbx | 44 / 200 | 24 | 3.7 × 2.6 × 0.362 | Two post collision boxes and lintel; 3 m clear opening. |
| Tent.fbx | 163 / 300 | 12 | 3.08 × 2.288 × 3.2 | Tent_Collision: box around tent body. |
| Log_Pile.fbx | 144 / 250 | 0 | 2.0 × 1.268 × 1.357 | Box collider is enough. |
| Chopping_Block.fbx | 60 / 150 | 0 | 0.689 × 1.209 × 0.679 | Box collider is enough. |
| Campfire.fbx | 120 / 200 | 0 | 1.368 × 0.3 × 1.374 | None. Fire VFX centre at origin. |
| Bridge_Wooden.fbx | 400 / 600 | 40 | 3.1 × 1.2 × 9.0 | Bridge_Wooden_Collision: deck, end ramps and rails. |
| Chapel_Ruin.fbx | 416 / 1500 | 84 | 10.0 × 4.0 × 15.0 | Simplified walls, altar and rubble collision; west (-X) side open. Two pointed windows in east wall. |
| Standing_Stone_A.fbx | 48 / 80 | 0 | 0.951 × 2.8 × 0.924 | Box collider is enough. |
| Standing_Stone_B.fbx | 48 / 80 | 0 | 0.997 × 2.857 × 0.924 | Box collider is enough. |
| Standing_Stone_C.fbx | 48 / 80 | 0 | 0.951 × 2.1 × 0.924 | Box collider is enough. |
| Castle_Wall.fbx | 588 / 600 | 0 | 28.0 × 7.5 × 1.675 | Box collider is enough. |
| Castle_Wall_Short.fbx | 276 / 300 | 0 | 12.0 × 7.5 × 1.675 | Box collider is enough. |
| Castle_Tower.fbx | 536 / 900 | 44 | 5.6 × 11.0 × 5.6 | Castle_Tower_Collision: 12-sided cylinder. Decorative doorway; tower interior not playable. |
| Castle_Gatehouse.fbx | 324 / 900 | 36 | 9.0 × 8.0 × 3.44 | Two towers + lintel collision; clear opening 4 m wide, 5 m high. |
| Drawbridge.fbx | 144 / 150 | 0 | 4.0 × 2.723 × 6.0 | Box collider is enough; walkable. Pivot at hinge, -Z end of deck. |
| Castle_Keep.fbx | 396 / 1200 | 84 | 10.0 × 13.0 × 8.04 | Walls + roof collision; 2 m × 2.8 m open entrance on -Z. Empty interior. |
| Castle_Well.fbx | 114 / 250 | 64 | 1.6 × 1.5 × 1.6 | Castle_Well_Collision: stone ring only, height 0.9 m. |
| Weapon_Stand.fbx | 184 / 250 | 0 | 2.0 × 1.8 × 0.4 | Box collider is enough. |
| Banner.fbx | 5 / 40 | 0 | 1.4 × 3.0 × 0.035 | None. Pivot top centre. Cloth extends downward 3 m. |
| Totem_Base.fbx | 28 / 150 | 0 | 1.6 × 0.4 × 1.6 | Box collider is enough. |
| Totem.fbx | 254 / 400 | 0 | 0.9 × 2.1 × 0.645 | Box collider is enough. Pivot own bottom; place at y=0.4 on base. Totem_Glow crystals separate. |
| Totem_Broken.fbx | 104 / 300 | 0 | 1.292 × 0.61 × 1.333 | Stump root with separate Chunk_1..Chunk_4 mesh children; add chunk physics later. |
| Extraction_Portal.fbx | 188 / 400 | 24 | 5.0 × 3.0 × 3.0 | Two pillar collision meshes combined; empty portal opening. Portal_Runes separate. |
| Treasure_Chest.fbx | 208 / 300 | 0 | 0.9 × 0.614 × 0.599 | Box collider is enough. Lid child hinge at (0,0.4,-0.275), rotates Unity local X -110 degrees. |
| Challenge_Shrine.fbx | 98 / 300 | 0 | 0.8 × 2.2 × 0.8 | Box collider is enough. Separate Glyph child uses Shrine_Glow, front +Z. |
| Loot_Shard.fbx | 10 / 60 | 0 | 0.129 × 0.25 × 0.137 | None. Pivot at centre; white material for runtime tint. |

## Sources and review
Source/build_forest.py rebuilds this set in Blender (no external generator or paid service). Source/Forest_Assets.blend is the editable asset gallery. Each asset is spaced out for review in that file; the exported FBXs are individually at their documented origin.

Source/forest_report.json records actual triangle counts, bounds, and named children. This is a practical first pass: facade cracks, cloth wear, chain links and some small ornamental details are simplified. No Unity runtime validation or scene integration was performed.
