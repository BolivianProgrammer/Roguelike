# Hub NPCs: Vendor and Mentor

Same skeleton and bone names as the Revenant (Unity `HumanBodyBones` names, three spine bones), so every Revenant clip plays on them too. They have no cut lines and no wound caps. T-pose, facing +Z, feet at the origin, 1 unit = 1 m.

| File | Tris | Height | What it is |
| --- | --- | --- | --- |
| `Vendor.fbx` | 10,432 | 1.75 m | Merchant: linen shirt with rolled cuffs, long leather apron, belt and pouch, trousers, boots, short dark hair and a moustache. No weapon or helmet. |
| `Mentor.fbx` | 11,676 | 1.78 m | Old veteran-priest: long grey robe with flared sleeves, red stole, one worn iron pauldron (left), belt, pendant, bald head with a white fringe and beard. No weapon. |
| `Vendor.blend`, `Mentor.blend` | | | Source scenes |
| `Textures/<Char>_BaseColor.png`, `<Char>_Normal.png` | | | 2048², with AO baked into base colour |
| `Animations/Npc@<Clip>.fbx` | | | NPC clips (below), skeleton only |

- Each model is one skinned mesh (`<Char>_Body`) with one material (`M_<Char>`).
- The Mentor has no leg geometry under the robe. The robe skirt is weighted to the thighs and shins, so it follows the legs.

## Unity import

- **Model:** Scale Factor 1, Convert Units on. **Rig:** Humanoid, Create From This Model. Every bone maps automatically.
- **Material:** URP/Lit, with Base Map = BaseColor and Normal Map = Normal (mark it as a normal map). Smoothness about 0.2.
- **Hierarchy:** the model and the clips use the same tree, `<file root> > Armature > Hips > ...`. Clips also contain a 1-triangle `Clip_Anchor` mesh that keeps the Armature node; ignore it or hide it.
- **Clips:** Rig = Humanoid, Avatar = Copy From Other Avatar (the Vendor avatar; it works for the Mentor too). Tick *Loop Time* on looping clips. Bake root rotation and Y into the pose, based on Original.
- The NPCs are slightly smaller than the Revenant (rest scale 0.956 for the Vendor, 0.983 for the Mentor). Humanoid retargeting handles this.

## Clips (30 fps, in place)

| # | Clip | Length | Loop | Notes |
| --- | --- | --- | --- | --- |
| 19 | Npc_Idle | 4.0 s | ✔ | Relaxed stance. Weight shifts right, glances aside, then shifts left. |
| 20 | Npc_Greet | 1.2 s | | Looks up, nods and gives a small raised-hand gesture. Starts and ends on the Npc_Idle pose. |
| 21 | Npc_Talk | 3.0 s | ✔ | Alternating small right- and left-hand gestures. |
| 22 | Npc_Trade | 2.0 s | | Crouches and reaches under the counter (0.45 s), grabs (0.70 s), rises and places the item on the counter (**release at about 1.15–1.40 s**), then back to idle. Spawn the item in the right hand at 0.70 s and drop it onto the counter at about 1.3 s. |
| 23 | Npc_Bless | 2.0 s | | Mentor raises the right hand up and forward over the player (held 0.5–1.4 s), then lowers it. Trigger the level-up effect at about 0.95 s. |

## Rebuild

- Models: `blender -b --factory-startup --python Art/NPC/Source/build_npc.py -- --char Vendor|Mentor [--preview DIR]`. This reuses the body builder from `Art/Revenant/Source/build_revenant.py`.
- Clips: `blender -b Art/NPC/Vendor.blend --python Art/Revenant/Source/export_anims.py -- --set npc [--clips A,B] [--preview DIR]`. Definitions are in `Art/Revenant/Source/clips_npc.py`.
