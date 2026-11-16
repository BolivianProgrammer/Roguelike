# Art brief: every asset still needed

**Status (2026-11-15):** section 5 (the forest set, including the totem, portal, chest, shrine and loot shard) has
been **delivered and is in the game**. Still needed: sections 1–4 and 6 (hub props, three NPC bodies, the player's
hands, enemy variant textures and shield, effect and UI textures).

This is the complete list of art the game still needs. Everything listed here is currently a grey Unity box,
cylinder or tinted copy in the game, and will be swapped for your model by name. Send back FBX files and textures
as described; nothing here needs to be rigged except the characters and the player hands.

The game: a first-person VR melee roguelike for **Meta Quest 3** (Unity 2022.3, URP, mobile VR budgets).
Style: **dark fantasy, stylised low-poly** — chunky readable shapes, flat-shaded or softly bevelled, colour from a
small palette texture rather than detailed painted textures. Same look as the Revenant, the Boss, the weapons and the
Plaza 25 de Mayo hub set you already made.

---

## 0. Conventions (apply to every file)

**Files and scale**
- FBX, **1 unit = 1 metre**, **Y up**, **+Z forward** in Unity (Blender: export with -Z forward / Y up, apply
  transforms). Check in Unity that the object's root rotation is (0,0,0) or at most the usual Blender -90° X.
- One FBX per asset, named exactly as the heading below (e.g. `Tree_Pine.fbx`). Variants: `Tree_Pine_A.fbx`,
  `Tree_Pine_B.fbx`, ...
- **No lights, cameras or empty helper objects** in the FBX, unless a named child is asked for below.

**Pivots**
- Props: pivot **on the ground at the centre of the base**, unless the asset says otherwise.
- Walls and fences: pivot at the centre of the base, length running along X.
- "Front" means the side facing **+Z** (the side the player normally sees or uses).

**Materials and textures**
- **One material per asset** wherever possible. Each set shares **one palette texture** (point filtered, e.g.
  256 × 256 colour swatches), like `Hub_Palette.png` and `Weapons_Palette.png` before:
  - Hub set: reuse `Hub_Palette.png` (or extend it) → material `Hub_Mat`.
  - Forest set: new `Forest_Palette.png` → material `Forest_Mat`.
  - Player set: new `Player_Palette.png` → material `Player_Mat`.
- Exceptions (separate material, named as given) only where stated: glowing parts, water, glass, cloth with alpha.
- Characters: one material, **2048² base colour + normal**.
- Tileable ground textures: **1024² base colour + normal**, seamless.

**Budgets (triangles)** — given per asset. Whole visible scene must stay under about 150k triangles, so trees,
rocks and wall pieces (used dozens of times) must stay light.

**Collision**
- Simple shapes: write "box collider is enough" in the README (the game adds a box around the model).
- Anything walkable, arched, hollow or irregular: include a low-poly mesh named `<Asset>_Collision` inside the same
  FBX (convex pieces preferred, under 100 triangles). It is hidden and used only for physics.
- Foliage (leaves, ferns, bushes) has **no** collision.

**Delivery**
- Folder per set: `Art/Hub/`, `Art/NPC/`, `Art/Player/`, `Art/Enemies/`, `Art/Forest/`, `Art/FX/`, `Art/UI/`,
  each with `Models/`, `Textures/`, `Source/` (your generator scripts) and a **README.md** listing every file, its
  triangle count, its real size (X × Y × Z in metres), its collision, and anything the game code needs to know
  (named children, anchor points).
- Characters and their clips: the animation FBX must have the **same hierarchy as the model, Armature > Hips > ...**
  (state it in the README).

**Palette guide (dark fantasy)**
- Stone: cool greys `#5E5F63`, `#3E3F44`, moss `#4E5E3A`. Wood: `#5B3E28`, dark `#33241A`. Iron: `#2E3033`.
- Bronze/brass trim: `#8C6A2E`, gold accent `#C9A227`. Cloth: deep red `#7A1F1F`, faded blue `#2F4560`, linen `#C9B99A`.
- Forest foliage: pine `#26382A`, oak `#3F5A2C`, dry leaves `#7A5A2A`. Corruption/magic: violet `#6B3FA0`, glow `#B07CFF`.
- Hub (Sucre, Bolivia): white walls `#E8E2D4`, terracotta roofs `#A0452E`, jacaranda purple `#7D5BA6`.

**Priority**: **P1** = seen all the time / looks broken now, **P2** = seen often, **P3** = polish.

---

## 1. Hub — Plaza 25 de Mayo (deliver to `Art/Hub/Models/`)

The plaza itself is done (ground, monument, trees, benches, lamps, fountain, facades, vendor stand, weapon rack,
notice board, mission gate). The plaza is now **34 × 34 m** with a **22 m paved round centre**; every station stands
in that centre. These pieces are still boxes:

### Sign_Hanging — P1
- **Used for:** the name boards of the Vendor and the Armorer, hanging under the front edge of their stall awnings.
- **Size:** board 1.3 × 0.42 × 0.06 m, plus two 0.3 m chains (or ropes) above it. Total height 0.72 m.
- **Pivot:** at the **top of the chains** (the hanging point), centred. Board front faces +Z.
- **Look:** a thick plank board with a carved bevelled edge and a thin brass/iron rim, two iron chains with a ring at
  the top. Slightly worn, nicked corners. Both faces of the board must be **flat and plain** (no carved letters): the
  game prints the name on both faces.
- **Tris:** ≤ 150. Hub palette. **Collision:** none.

### Sign_Post — P1
- **Used for:** the status board at the spawn (level, essence, coins; several lines of text), the mission sign
  beside the gate, the quartermaster's sign.
- **Size:** 1.4 × 2.0 × 0.15 m overall. Board 1.3 × 1.0 m, board centre at 1.45 m above the ground, on two square
  posts (0.08 m) set in the ground.
- **Pivot:** ground, centre between the posts. Board front (plain face) on +Z.
- **Look:** dark stained wood board in a frame with a small pitched cap (little roof) on top, iron brackets where the
  board meets the posts. Front face **plain and flat** (text is printed by the game, so keep it free of details,
  1.2 × 0.9 m usable). Back can have planks/battens.
- **Also make `Sign_Post_Small`:** same style, board 1.1 × 0.45 m, centre at 1.85 m (for one or two words).
- **Tris:** ≤ 200 each. Hub palette. **Collision:** box collider is enough.

### Sign_Plank — P1
- **Used for:** the "ARMORY" name plank on top of the weapon rack, and "MISSIONS" on top of the notice board.
- **Size:** 1.5 × 0.36 × 0.06 m. **Pivot:** bottom centre (it sits on top of the rack). Front +Z.
- **Look:** a single carved plank with rounded ends and two small iron nails/brackets. Both faces plain.
- **Tris:** ≤ 60. Hub palette. **Collision:** none.

### Quartermaster_Counter — P1
- **Used for:** the quartermaster's station: he stands behind it and hands stored weapons over on top of it.
- **Size:** 1.8 × 0.9 × 0.7 m (counter), plus storage behind it up to 1.6 m deep in total.
- **Pivot:** ground, centre of the counter. The **customer side is +Z**; the quartermaster stands at z ≈ -0.9 m.
- **Look:** a heavy wooden counter with iron-bound corners. Behind it: a shelf with wrapped bundles, two barrels,
  a crate with sword hilts sticking out, a ledger and a lantern. **The counter top must be flat and clear**
  (a weapon is laid on it; it appears at 0.95 m height, centre).
- **Tris:** ≤ 700. Hub palette. **Collision:** `Quartermaster_Counter_Collision` (counter box + storage box).

### Armor_Mannequin — P1
- **Used for:** display beside the Armorer's stall.
- **Size:** 0.6 × 1.75 × 0.4 m. **Pivot:** ground centre. Front +Z.
- **Look:** wooden tailor's dummy on a cross-foot stand wearing a leather cuirass with mail skirt and one pauldron.
- **Tris:** ≤ 400. Hub palette. **Collision:** box collider is enough.

### Portal_Ring — P2
- **Used for:** the floor of the mission gate's platform (the circle the player stands in to leave). A portal effect
  is added in the engine.
- **Size:** 3.0 × 0.05 × 3.0 m disc. **Pivot:** centre, on the ground.
- **Look:** a ring of carved stone segments with rune grooves around a darker inner disc.
- **Two materials:** `Hub_Mat` for the stone, and **`Portal_Runes`** for the rune grooves only (a separate mesh part
  so the game can make it glow and pulse).
- **Tris:** ≤ 200. **Collision:** none (the platform under it already collides).

### Street_Paving (texture) and Curb_Section — P2
- **Used for:** the 70 × 70 m street area around the plaza (now a flat dark slab).
- **Textures:** `Street_BaseColor.png` + `Street_Normal.png`, 1024², seamless, one tile = 2 m: large worn cobbles,
  darker and rougher than the plaza paving.
- **Curb_Section:** 2.0 × 0.2 × 0.3 m stone curb, tiles every 2 m along X; pivot at the base centre. ≤ 40 tris.
  `Curb_Corner` for the four plaza corners. Box collider is enough.

---

## 2. Hub characters (deliver to `Art/NPC/`)

Same skeleton and bone names as the Revenant/Vendor/Mentor (Armature > Hips > ...), so the existing Npc_* clips
(Npc_Idle, Npc_Greet, Npc_Talk, Npc_Trade, Npc_Bless) play on them. T-pose, facing +Z, feet at the origin.
No weapons in hand, no wound caps. **8,000–12,000 tris, one material, 2048² base colour + normal.**
Right now these three are tinted copies of the Vendor.

### Armorer — P1
Blacksmith, about 1.8 m, broad. Heavy leather apron over a rolled-sleeve shirt, thick forearms, leather gloves
tucked in the belt, a smith's hammer hanging from the belt (part of the mesh, on the left hip), soot on the face,
short beard. Earth tones with a warm leather brown.

### Quartermaster — P1
Storekeeper and keeper of weapons, about 1.72 m, older, a little stooped. Long dark-blue coat with brass buttons,
scarf, a ring of keys and a small ledger hanging from the belt, spectacles. Tidy and stern.

### Scout — P1
Ranger who hands out the missions, about 1.75 m, lean. Hooded green-grey cloak (hood down), leather jerkin, a short
bow and quiver **on the back** (not held), bracers, map case on the hip. Weathered, alert.

---

## 3. Player (deliver to `Art/Player/`)

### Player_Hand_L / Player_Hand_R — P1 (the hands are cubes right now)
- **Size:** life size, about 0.19 m long. **Pivot:** at the **wrist**, fingers pointing along **+Z**, palm facing
  **-X** for the right hand and **+X** for the left (palms facing each other with the arms held forward).
- **Rig:** wrist root + 3 bones per finger + 3 for the thumb. Bone names `Hand`, `Index1..3`, `Middle1..3`,
  `Ring1..3`, `Pinky1..3`, `Thumb1..3`.
- **Look:** leather gloves with a metal plate on the back of the hand and riveted finger plates (dark fantasy
  gauntlet-glove). Must read well up close in VR.
- **Poses (as clips, one frame each, in one FBX per hand or separate files `Player_Hand_R@Open.fbx` etc.):**
  `Open` (relaxed), `Fist`, `Grip_Sword` (fingers wrapped round a 3.5 cm diameter handle running along the hand's
  X axis), `Point` (index out).
- **Tris:** 2,000–3,000 each. Player palette.

### Gauntlet_L / Gauntlet_R — P2
- Forearm bracer worn over the glove cuff. 0.12 × 0.12 × 0.22 m. **Pivot:** at the wrist end, extending along -Z
  (up the forearm). Leather with a metal plate and two straps. ≤ 300 tris.
- The **left** one carries the wrist display: leave a flat 0.12 × 0.08 m area on the **inside** of the wrist.

### Wrist_Display — P2
- A brass-framed plate mounted on the left gauntlet. Face 0.12 × 0.08 m, **plain** (the game draws the health,
  stamina and loot on it). Pivot at the centre of the face, face looking +Z. ≤ 150 tris. Player palette.

### Sheath_Hip / Scabbard_Back — P3
- Where weapons go on the body (now small spheres). `Sheath_Hip`: a leather frog/loop on a belt piece,
  0.15 × 0.25 × 0.08 m. `Scabbard_Back`: a back strap with a ring holder, 0.2 × 0.35 × 0.08 m.
- Pivot at the holding point (where the weapon's grip sits). ≤ 200 tris each. Player palette.

---

## 4. Enemies (deliver to `Art/Enemies/`)

All enemies use the Revenant model and skeleton. Four of them are only recoloured with flat grey materials today.

### Revenant variant textures — P2
Paint new **2048² base colour (+ normal if the shapes differ)** textures on the **existing Revenant UV layout**:
- `Revenant_Caster` — tattered dark robes over the bones, violet glowing runes on the chest and hands (spellcaster).
- `Revenant_Runner` — lighter build look: rags, bare ribs, red head wrap (fast and aggressive).
- `Revenant_Shieldbearer` — scraps of mail and a rusted helmet painted on, heavier colours.
- `Revenant_Elite` — blackened plate pieces with gold trim, a torn red tabard (mini-boss).
Keep the wound-cap flesh colours as on the Revenant.

### Shield_Tower — P2 (the Shieldbearer's shield is a box right now)
- **Size:** 0.45 × 0.65 × 0.06 m, slightly curved, rectangular with rounded top. **Pivot:** at the **handle**
  (centre of the back), shield face towards +Z.
- **Look:** banded wooden planks, iron rim and boss, a faded skull sigil, battle damage.
- **Tris:** ≤ 400. Weapons palette (`Weapons_Palette.png`). **Collision:** box collider is enough.

---

## 5. Main level — Mission_Forest (deliver to `Art/Forest/Models/`)

A medieval forest road to a fallen castle, about 110 × 110 m. The whole level is still a graybox; every object in
the scene is named exactly like the asset that replaces it. Layout: start and extraction portal in the south,
a woodcutters' camp in the west, a ruined chapel in the east, the castle in the north, a dirt path linking them and
a stream crossing the middle. Forest palette (`Forest_Palette.png`, material `Forest_Mat`) unless noted.

**Biggest visual change first:** Tree_Pine, Tree_Oak, Rock, Bush/Fern, the ground textures, then the castle set,
then the camp and chapel.

### Ground and terrain

#### Forest_Ground (textures) — P1
`Grass_BaseColor/Normal.png` (forest floor: short grass, moss, needles; one tile = 4 m) and
`Dirt_BaseColor/Normal.png` (trodden earth with small stones and roots; one tile = 2 m), 1024², seamless.

#### Ground_Mound — P1 (14 placed; now squashed spheres)
Gentle grassy hillocks, 3 variants `Ground_Mound_A/B/C`, 6–12 m wide × 1.1–1.4 m high, irregular outline, soft
slopes (walkable, under 25°). Pivot at the centre at ground level (the base sits about 0.3 m below ground).
≤ 150 tris each. **Collision:** `Ground_Mound_X_Collision` (simplified shape, walkable).

#### Path_Dirt — P2 (9 straight pieces now)
Dirt road pieces 3.5 m wide that lie 2 cm above the ground: `Path_Dirt_Straight` (6 m long along Z) and
`Path_Dirt_Bend` (30° bend). Soft irregular edges blending into grass (vertex colour or alpha-cut edge), a few
pebbles and ruts. Dirt material. ≤ 80 tris each. No collision.

#### Water_Stream — P2 (9 pieces now)
`Stream_Straight` (6 m long, 3 m wide) and `Stream_Bend` (30°): sunken bed 0.4 m deep with muddy banks and a few
stones; the **water surface is a separate mesh** with its own material **`Forest_Water`** (dark, slightly
transparent). Pivot at the centre of the bed at ground level. ≤ 250 tris each. Collision: the bed as
`_Collision` (walkable); the water has none.

#### Bridge_Wooden — P1
3.2 × 1.2 × 9 m including ramps at both ends along Z. Walkable deck at 0.35 m. Planks, side rails, posts.
≤ 600 tris. **Collision:** `Bridge_Wooden_Collision` (deck, ramps and rails).

### Forest

#### Tree_Pine — P1 (99 placed)
Conifer 4 × 9 × 4 m. Chunky stacked-cone foliage (**opaque**, no alpha cards), visible trunk at the bottom. 3
variants (A tall and narrow, B full, C leaning/dead with some bare branches). ≤ 600 tris each.
**Collision:** trunk only — `Tree_Pine_Collision`, a cylinder r 0.3 m up to 3 m.

#### Tree_Oak — P1 (71 placed)
Broadleaf 6 × 8 × 6 m with a round lumpy crown and a thick twisted trunk with roots. 2 variants. ≤ 800 tris.
**Collision:** trunk only, r 0.4 m up to 3 m (`Tree_Oak_Collision`).

#### Bush / Fern — P2
`Bush` 1.5 × 0.8 × 1.5 m round shrub, 2 variants. `Fern` 1.2 × 0.6 × 1.2 m fronds (low-poly blades, opaque).
≤ 150 tris each. **No collision.**

#### Rock — P1
3 variants, 1.5 × 0.8 × 1.5 m average (A flat, B tall 1.2 m, C cluster of three). Mossy tops. ≤ 120 tris each.
**Collision:** box is enough for A, `_Collision` for B and C.

#### Log_Fallen — P2
3 × 0.7 × 0.7 m, lying along X, broken end, moss and a fungus shelf. ≤ 150 tris. Collision: capsule-like
`Log_Fallen_Collision`.

### Woodcutters' camp (west)

#### Palisade_Section — P1 (19 placed)
3.0 × 2.6 × 0.4 m: a row of sharpened stakes lashed to two cross-beams, tiles every 3 m along X. ≤ 200 tris.
Box collider is enough. Also `Palisade_Gate` (3 m opening with a lintel, same height) if you can.

#### Tent — P2
3 × 2.2 × 3.2 m canvas A-frame, opening on +Z, guy ropes and pegs. **Two materials:** `Forest_Mat` for poles and
pegs, **`Forest_Cloth`** for the canvas (worn linen). ≤ 300 tris. `Tent_Collision` (box for the tent body).

#### Log_Pile — P3
2 × 1.4 × 1.4 m stacked cut logs with end grain. ≤ 250 tris. Box collider is enough.

#### Chopping_Block — P3
0.7 × 1.2 × 0.7 m tree stump with an axe stuck in it (axe is part of the mesh). ≤ 150 tris. Box is enough.

#### Campfire — P2
1.4 × 0.4 × 1.4 m ring of stones around crossed logs and ash. **Fire centre = the origin** (the game adds the
flames and light). ≤ 200 tris. No collision.

### Ruined chapel (east)

#### Chapel_Ruin — P1
10 × 4 × 15 m roofless stone nave: broken side walls 2–4 m high with two pointed window openings, a fallen arch,
the west wall (the -X end) open/collapsed, an altar block at local (2.5, 0, 4.5), rubble, ivy. ≤ 1,500 tris.
**Collision:** `Chapel_Ruin_Collision` for walls, altar and big rubble (keep the doorways walkable).

#### Standing_Stone — P2 (used ×9 in a circle, r 6.5 m)
1 × 2.8 × 0.6 m weathered menhir with faint carved spirals. 3 variants (straight, leaning, broken top). ≤ 80 tris.
Box collider is enough.

### Castle (north)

#### Castle_Wall — P1 (5 placed)
28 × 7.5 × 1.6 m curtain wall, length along X: battlements (merlons) along the top, a walkway behind them, a few
arrow slits, cracked and patched stones, moss at the foot. Also **`Castle_Wall_Short`** (12 m). ≤ 600 / 300 tris.
Box collider is enough.

#### Castle_Tower — P1 (4 placed)
Round corner tower 5.6 × 11 × 5.6 m with a crenellated top and corbels below the battlements, arrow slits, a
doorway on +Z. ≤ 900 tris. `Castle_Tower_Collision` (cylinder, 12 sides).

#### Castle_Gatehouse — P1
9 × 8 × 3.4 m: two square towers and an arch between them. **Opening 4 m wide × 5 m high centred on X**, with a
**raised portcullis** in the arch (part of the mesh, high enough to walk under). ≤ 900 tris.
`Castle_Gatehouse_Collision` (the two towers and the lintel; the opening must stay walkable).

#### Drawbridge — P2
4 × 0.3 × 6 m planks with iron bands and two chains rising towards the gatehouse. Pivot at the hinge end
(the -Z end). ≤ 150 tris. Box collider is enough (walkable).

#### Castle_Keep — P1
10 × 13 × 8 m square keep: buttresses, a few windows, crenellated top, **door on -Z, 2 × 2.8 m**. ≤ 1,200 tris.
`Castle_Keep_Collision`.

#### Castle_Well — P3
1.6 × 1.5 × 1.6 m stone well with a little wooden roof and a bucket. ≤ 250 tris. `Castle_Well_Collision`
(the stone ring only, 0.9 m high).

#### Weapon_Stand — P3
2 × 1.8 × 0.4 m decorative rack with a few spears and a shield (meshes, not real weapons). ≤ 250 tris. Box is enough.

#### Banner — P3
1.4 × 3 × 0.05 m red cloth with a black sigil, hanging from a pole at its **top edge (pivot at the top centre)**.
Material **`Forest_Cloth`**. ≤ 40 tris. No collision.

### Objectives and gameplay objects (used in every mission)

#### Totem and Totem_Base — P1 (the mission's objectives; 3 in the level)
- `Totem_Base`: 1.6 × 0.4 × 1.6 m cracked stone plinth. Stays after the totem breaks. Box is enough.
- `Totem`: 1.0 × 2.1 × 1.0 m corrupted carved wooden/bone pillar standing on the base (pivot at its own bottom,
  0.4 m up), with skulls, chains and violet crystals. **Two materials:** `Forest_Mat` and **`Totem_Glow`** for the
  crystals (the game makes them pulse). It must read clearly as "the thing to destroy" from 20 m away. ≤ 400 tris.
- `Totem_Broken`: the totem's stump (0.6 m) plus 3–4 chunks as **separate child objects** named `Chunk_1`...
  (the game throws them with physics). ≤ 300 tris total.

#### Extraction_Portal — P1
5 × 3 × 1 m: two tall rough standing stones 3 m apart with a rune-carved lintel stone across the top, and a ground
ring (r 1.5 m) between them. The space between the stones must be **empty** (portal effect in engine). Rune grooves
as a separate part with material **`Portal_Runes`** (shared with the hub's Portal_Ring). ≤ 400 tris.
`Extraction_Portal_Collision` (the two stones only).

#### Treasure_Chest — P1 (2 in the level; built from two boxes now)
- 0.9 × 0.6 × 0.55 m iron-bound wooden chest, front (lock) facing +Z.
- The lid must be a **separate child named `Lid`** whose **pivot is on the back hinge line** (top back edge,
  local -Z side): the game opens it by rotating `Lid` -110° around its X axis.
- ≤ 300 tris. Box collider is enough.

#### Challenge_Shrine — P1 (built from a cylinder and a cube now)
0.8 × 2.2 × 0.8 m dark stone pillar on a small stepped base, with a **glyph on the front (+Z) as a separate child
named `Glyph`** using material **`Shrine_Glow`** (red; the game lights it while a challenge runs). ≤ 300 tris.
Box collider is enough.

#### Loot_Shard — P2 (a small cube now)
0.15 × 0.25 × 0.15 m faceted crystal shard that flies to the player (essence and coins). Pivot at the centre.
Material **`Shard_Glow`**, white/greyscale so the game can tint it (violet for essence, gold for coins).
≤ 60 tris. No collision.

---

## 6. Effects and interface textures (deliver to `Art/FX/` and `Art/UI/`)

### Blood_Decals.png — P2
1024², 2 × 2 grid of dark-red splats (one big splash, one drip trail, two spatters), alpha in the texture, slightly
glossy look. Replaces the flat red squares on the floor.

### FX_Atlas.png — P2
1024², 4 × 4 grid (256 px cells), white/greyscale with alpha so the game can tint: 1 spark streak, 2 slash arc,
3 soft smoke puff, 4 ember dot, 5 magic orb (soft core + rim), 6 shockwave ring, 7 portal swirl, 8 rune circle,
9 blood droplet, 10 dust puff, 11 star flash, 12 crack/impact, 13–16 free. Used by the pooled effect prefabs.

### UI_Frame.png — P3
256², 9-slice frame for menus and cards: dark wood centre with a bronze trim border (corners 48 px). Plus
`UI_Frame_Highlight.png` (same frame with a gold border) for the selected row.

### UI_Icons.png — P3
512², 8 × 8 grid of 64 px white icons on transparent: coin, essence (violet drop), level (star), sword, hammer,
dagger, axe, armor, trinket (amulet), heart, stamina (bolt), skull, chest, map, gate, gear.

---

## Summary checklist

| Set | Assets | Priority |
| --- | --- | --- |
| Hub | Sign_Hanging, Sign_Post (+Small), Sign_Plank, Quartermaster_Counter, Armor_Mannequin, Portal_Ring, Street textures, Curb_Section/Corner | P1–P2 |
| NPC | Armorer, Quartermaster, Scout | P1 |
| Player | Player_Hand_L/R (+4 poses), Gauntlet_L/R, Wrist_Display, Sheath_Hip, Scabbard_Back | P1–P3 |
| Enemies | 4 Revenant variant textures, Shield_Tower | P2 |
| Forest | Ground textures, Ground_Mound ×3, Path_Dirt ×2, Stream ×2, Bridge_Wooden, Tree_Pine ×3, Tree_Oak ×2, Bush ×2, Fern, Rock ×3, Log_Fallen, Palisade_Section (+Gate), Tent, Log_Pile, Chopping_Block, Campfire, Chapel_Ruin, Standing_Stone ×3, Castle_Wall (+Short), Castle_Tower, Castle_Gatehouse, Drawbridge, Castle_Keep, Castle_Well, Weapon_Stand, Banner | P1–P3 |
| Gameplay | Totem, Totem_Base, Totem_Broken, Extraction_Portal, Treasure_Chest, Challenge_Shrine, Loot_Shard | P1–P2 |
| FX / UI | Blood_Decals, FX_Atlas, UI_Frame (+Highlight), UI_Icons | P2–P3 |
