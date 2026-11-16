# Hub: Plaza 25 de Mayo, Sucre (stylised, about 60% scale)

Dark fantasy, stylised low-poly. Same conventions as the Revenant and Weapons sets:
- FBX, 1 unit = 1 m, Y up, +Z forward in Unity.
- No lights, cameras or empties.
- Props have their pivot on the ground at the centre of the base. Facades have their pivot at front-centre, at ground level, and their front faces +Z.

## Materials and textures

| Material | Used by | Texture |
| --- | --- | --- |
| `Hub_Mat` | Every prop and facade (one material each) | `Textures/Hub_Palette.png`, a shared colour palette like `Weapons_Palette.png`. Use Point filtering, no mipmaps, and no compression (or High Quality). |
| `Hub_Water` | `Fountain_Water` only | No texture. Transparent URP/Lit, dark teal, smoothness about 0.9, alpha 0.75 |
| `Hub_Glass` | `Lamp_Globe` only | No texture. Warm yellow. Turn on Emission to make it glow. |
| `Hub_Paving` | `Plaza_Ground`, slot 0 (paths and round centre) | `Paving_BaseColor.png` + `Paving_Normal.png`, 1024², tileable. One tile = 2 m. |
| `Hub_Grass` | `Plaza_Ground`, slot 1 (raised beds) | `Grass_BaseColor.png` + `Grass_Normal.png`, 1024², tileable. One tile = 4 m. |

`Plaza_Ground` is UV-mapped in world metres divided by the tile size, so the URP tiling stays at 1,1.

## Files (`Models/`)

Sizes are Unity X × Y × Z in metres. Tri counts are for the visible mesh. `*_Collision` meshes are extra objects in the same FBX; turn off their renderer and add a MeshCollider.

| # | File | Tris | Size (m) | Collision / notes |
| --- | --- | --- | --- | --- |
| 1 | `Monument_Sucre.fbx` | 988 | 7.0 × 7.0 × 7.0 | `Monument_Sucre_Collision` (step cylinders, pedestal and statue boxes). Two steps, 7 m and 5.2 m across, then a white pedestal (plaques and medallions flush on all four faces) and a bronze Sucre. |
| 3 | `Fountain.fbx` | 568 + 40 water | 3.12 × 1.95 × 3.12 | `Fountain_Collision` (basin cylinder, 0.62 m high). Water surface `Fountain_Water` uses its own material. |
| 6 | `Palm.fbx` | 164 trunk + 240 foliage | 6.7 × 10.4 × 6.5 | `Palm_Collision` (trunk, r 0.25 m, 2.5 m high). Foliage is **solid folded fronds** (no alpha). Use double-sided or Render Face = Both. |
| 7 | `Jacaranda.fbx` | 142 trunk + 560 foliage | 7.5 × 6.7 × 7.5 | `Jacaranda_Collision` (trunk, r 0.22 m). Foliage is **chunky low-poly purple clumps** (opaque, no alpha). |
| 8 | `Bench.fbx` | 300 | 1.6 × 0.89 × 0.63 | Box collider is enough. Seat top is at 0.46 m. |
| 9 | `Lamp_Post.fbx` | 298 + 100 globe | 0.44 × 3.4 × 0.42 | Capsule collider (r 0.12 m, 3.4 m). `Lamp_Globe` (separate material) is centred at **(0, 3.12, 0)** with r 0.22 m; put the point light there. |
| 9b | `Brazier.fbx` | 276 | 0.72 × 1.06 × 0.68 | Box collider is enough. Iron fire bowl on three legs; the fire centre is **(0, 1.03, 0)**, where the game puts the `Ambient_Brazier` effect (flames, embers, smoke, flickering light). |
| 12 | `Vendor_Stand.fbx` | 300 | 4.0 × 2.64 × 2.0 | `Vendor_Stand_Collision` (counter and crates). See below. |
| 13 | `Weapon_Rack.fbx` | 256 | 2.3 × 1.9 × 0.4 | Box collider is enough. See below. |
| 14 | `Notice_Board.fbx` | 72 | 1.6 × 2.2 × 0.3 | Box collider is enough. See below. |
| 15 | `Mission_Gate.fbx` | 460 | 5.66 × 3.38 × 4.18 | `Mission_Gate_Collision` (two pillars and the walkable platform). See below. |
| 16 | `Facade_CasaDeLaLibertad.fbx` | 1420 | 44.4 × 11.6 × 8.9 | Background. No collider, or one box. Central doorway. |
| 16 | `Facade_Cathedral.fbx` | 970 | 32.4 × 26.0 × 12.9 | Background. Bell tower is 26 m. |
| 16 | `Facade_Gobernacion.fbx` | 1924 | 48.4 × 14.3 × 11.7 | Background. Column portico. |
| 16 | `Facade_ArcadeRow.fbx` | 1040 | 24.4 × 9.8 × 10.1 | Background. Two-storey arcade that **tiles end to end every 24 m along X**. |
| 11 | `Plaza_Ground.fbx` | 202 | 34 × 0.35 × 34 | The walkable floor: give it a MeshCollider on itself. Paved round centre r 11 m (22 m across) where every station stands, eight 3.6 m paths, eight beds raised 0.35 m out to 14.5 m from the centre, 2.5 m walkway round the edge. Materials: slot 0 Hub_Paving, slot 1 Hub_Grass. |

Facade backs are flat or simple; only the fronts are detailed.

## Gameplay stations: what the game code needs

Unity coordinates, measured from the asset pivot.

- **Vendor_Stand:** the counter top is flat and clear at **y = 0.90 m**, with a 2.6 × 0.78 m area centred on x = 0. Put bought weapons there. There is a shelf under the counter at y = 0.30 m. The vendor stands behind the counter on the −Z side, and customers approach from +Z.
- **Weapon_Rack:** four hooks at **(±0.78, 1.75, 0.13)** and **(±0.26, 1.75, 0.13)**. Weapons hang point-down from them. Place it against a wall.
- **Notice_Board:** a plain board face centred at **(x 0, y 1.30)**, 1.2 × 1.0 m. Draw text there.
- **Mission_Gate:** the opening is empty, **3.0 m wide and 2.9 m high** at the centre (elliptical top, springline at 1.98 m). The flat round platform in front is centred at **z = +1.9**, r 1.5 m, 0.12 m high.

## Rebuild

`blender -b --factory-startup --python Art/Hub/Source/build_hub.py -- [--only A,B] [--preview DIR]`

- `hub_lib.py`: mesh helpers, palette and tileable textures.
- `preview_hub.py`: preview renders.
- `hub_report.json`: tri counts and sizes written by each build.


Removed from the game (no longer delivered): Lion_Statue, the three busts, Km0_Marker and the fence pieces. The plaza keeps only the Sucre monument.
