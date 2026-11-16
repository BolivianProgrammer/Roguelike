# Revenant — dismemberable enemy (dark fantasy, stylised low-poly)

| File | What it is |
| --- | --- |
| `Revenant.fbx` | Armature + `Revenant_Body` + 22 `Cap_*` wound caps, T-pose, no animation |
| `Revenant.blend` | Same scene, for editing and for animating later |
| `Textures/Revenant_BaseColor.png` | 2048², sRGB, ambient occlusion already multiplied in |
| `Textures/Revenant_Normal.png` | 2048², tangent space, OpenGL (+Y), which is what Unity expects |
| `Source/` | Procedural generator. Rebuild everything with:<br>`blender -b --factory-startup --python Art/Revenant/Source/export_revenant.py` |

## Specs

- About 1.80 m tall (to the horn tips about 1.91 m), feet on the ground at the origin, T-pose.
- Blender: faces -Y. The FBX is exported with forward -Z, up Y, so Unity gets **+Z forward**, Y up, 1 unit = 1 m.
- `Revenant_Body`: **11,876 triangles**, one skinned mesh, one material (`M_Revenant`).
  - The armour pieces (helm, horns, pauldrons, bracers, gloves, boots, belt, collar, knee cops) are separate parts of the same mesh.
- Caps: 22 meshes. They use the same material and the same texture atlas as the body.

## Skeleton (Unity `HumanBodyBones` names, auto-maps as Humanoid)

```
Hips
├─ Spine ─ Chest ─ UpperChest
│                  ├─ Neck ─ Head
│                  ├─ LeftShoulder ─ LeftUpperArm ─ LeftLowerArm ─ LeftHand
│                  └─ RightShoulder ─ RightUpperArm ─ RightLowerArm ─ RightHand
├─ LeftUpperLeg ─ LeftLowerLeg ─ LeftFoot ─ LeftToes
└─ RightUpperLeg ─ RightLowerLeg ─ RightFoot ─ RightToes
```

There are no finger bones: the hands are closed fists, and the grip channel runs front-to-back.

## Cut lines and wound caps

Every cut is a closed edge loop, and no triangle crosses it. Each cap is skinned 100% to the bone on its side of the cut.

| Cut | Height / place | Stump bone → removed bone | Stump-side cap | Removed-side cap |
| --- | --- | --- | --- | --- |
| Neck | z 1.56 | Neck → Head | `Cap_Neck_Body` | `Cap_Neck_Head` |
| Chest | z 1.22 (under ribs) | Spine → Chest | `Cap_Chest_Lower` | `Cap_Chest_Upper` |
| Waist | z 1.08 | Hips → Spine | `Cap_Waist_Lower` | `Cap_Waist_Upper` |
| Shoulder_L/R | arm root | LeftShoulder → LeftUpperArm | `Cap_Shoulder_L_Body` | `Cap_Shoulder_L_Arm` |
| Elbow_L/R | elbow | LeftUpperArm → LeftLowerArm | `Cap_Elbow_L_UpperArm` | `Cap_Elbow_L_Forearm` |
| Hip_L/R | groin / hip crease | Hips → LeftUpperLeg | `Cap_Hip_L_Body` | `Cap_Hip_L_Leg` |
| Knee_L/R | knee | LeftUpperLeg → LeftLowerLeg | `Cap_Knee_L_Thigh` | `Cap_Knee_L_Shin` |

The `_R` caps mirror the `_L` ones and use the Right* bones.

**The caps are visible when imported.** Disable their renderers on spawn and enable the right pair when a cut happens.

The cap texture shows flesh, a fat/skin rim and bone with marrow:
- Limb caps have the bone in the centre.
- Neck, chest and waist caps have the spine toward the back and the windpipe or gut toward the front.

### Weighting convention

- The cut loop itself is 100% the **stump** bone.
- The next loop toward the removed part blends 80% removed bone and 20% stump bone.
- Everything else is about 100% its own bone.
- Torso segments (Hips, Spine, Chest, UpperChest) follow the same rule.

What this means for the game code:

- **The body that keeps living:** collapse the removed bone (scale it to 0). Every vertex of that limb collapses to the joint. The stump loop stays exactly where it is, and `Cap_<cut>_<stump>` covers it cleanly.
- **The piece that flies off** is a copy of the skinned mesh that keeps only the removed branch:
  1. Collapse the bones that are not in that branch.
  2. Collapse the stump bone **at the joint**: move the copy's stump-bone transform to the removed bone's position and then scale it to 0. If you scale it in place, the cut loop gets pulled back to the stump bone's origin and makes a spike.
  3. `Cap_<cut>_<removed>` then closes the end.
- The pauldrons are weighted to `Left/RightShoulder`, so they stay on the body when an arm comes off.
- The belt is weighted to Hips, below the waist cut.
- The collar stops below the neck cut.
- No geometry crosses a cut line.

The body object also carries custom properties `cut_<Name>`, which list the vertex indices of each loop in Blender. Unity re-splits vertices at UV seams, so treat these as reference data only.

## Unity import

1. **Model tab:** Scale Factor 1, Convert Units on.
2. **Rig tab:** Animation Type = Humanoid, Avatar Definition = Create From This Model. All bones should map automatically, including UpperChest.
3. **Materials:** extract or remap to a URP/Lit material with Base Map = BaseColor and Normal Map = Normal (mark the normal map as *Normal map* in its importer). Set Smoothness to about 0.2.
4. **Weapon socket:** parent the weapon to `RightHand`. With the model in T-pose, put the weapon at world position **(0.80, 1.418, 0.002)** (the centre of the right fist) with **identity rotation**. The weapon files have the grip at the origin, the blade along +Z and the edge on +X (see `Art/Weapons`), so the blade points forward and the edge faces outward. Every clip keeps this wrist convention.

## Animations (`Animations/Revenant@<Clip>.fbx`)

These are skeleton-only FBX files, one clip per file, 30 fps, all in place.
- **Hierarchy:** the model and every clip use the same node tree, `<file root> > Armature > Hips > ...`, so *Copy From Other Avatar* works. Each clip also contains a 1-triangle mesh named `Clip_Anchor`, which makes Unity keep the `Armature` node. It has no skin weights, so you can ignore it or leave it hidden.
- **Import:** use Rig = Humanoid, Avatar = Copy From Other Avatar (the Revenant avatar). On looping clips, tick *Loop Time*. Root Transform settings: bake Rotation and Y into the pose, and set *Based Upon* to Original.
- **Locomotion:** the planted foot moves backward at a constant speed, so the feet don't slide when game code moves the character at the speed listed below.
- **Upper-body clips (`Bisected_*`):** they animate only Hips→Spine→arms→head. The legs stay at rest pose, ready to be collapsed or driven by physics.
- **Rebuild:** `blender -b Art/Revenant/Revenant.blend --python Art/Revenant/Source/export_anims.py -- [--clips A,B] [--preview <dir>]`. Clip definitions are in `Source/clips_revenant.py`.

| # | Clip | Length | Loop | Strike (s) | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | Idle | 2.00 | ✔ | | Combat stance |
| 2 | Walk | 1.00 | ✔ | | 1.00 m/s |
| 3 | Run | 0.70 | ✔ | | 2.50 m/s |
| 4 | Attack_RightSlash | 1.47 | | 0.60–0.80 | Wind-up 0–0.60 |
| 5 | Attack_LeftSlash | 1.47 | | 0.60–0.80 | Backhand |
| 6 | Attack_Overhead | 1.60 | | 0.70–0.88 | |
| 7 | Attack_Heavy | 1.80 | | 0.95–1.20 | Two-handed, wind-up 0.95 s |
| 8 | Hit_Front | 0.85 | | | Thrown back, catch step |
| 9 | Hit_Left | 0.85 | | | Blow on its left side |
| 10 | Hit_Right | 0.85 | | | Blow on its right side |
| 11 | Stunned | 3.20 | ✔ | | Hunched on bent knees, big lurching sway, knee buckle, sword tip down |
| 12 | Hit_Back | 0.85 | | | |
| 13 | Blocked_Recoil | 0.70 | | | |
| 14 | Attack_Thrust | 1.47 | | 0.65–0.78 | |
| 15 | Unarmed_Idle | 2.00 | ✔ | | Fists up |
| 16 | Unarmed_Attack | 1.40 | | 0.60–0.75 | Left-arm lunging grab or punch |
| 17 | Crawl | 1.60 | ✔ | | Legs dragged and limp |
| 18 | Crawl_Attack | 1.40 | | 0.60–0.78 | Right-arm swipe |
| 19 | Fall_ToCrawl | 1.20 | | | Ends on Crawl frame 0 |
| 20 | Strafe_Left / Strafe_Right | 1.00 | ✔ | | 0.60 m/s |
| 21 | Walk_Back | 1.10 | ✔ | | 0.76 m/s |
| 22 | Turn_Left / Turn_Right | 0.80 | ✔ | | Stepping on the spot. Rotate at about 100°/s in code |
| 23 | Bisected_Fall | 1.00 | | | Upper body only. Ends on Bisected_Crawl frame 0 |
| 24 | Bisected_Crawl | 1.60 | ✔ | | Upper body only |
| 25 | Bisected_Attack | 1.40 | | 0.60–0.78 | Upper body only |
| 26 | Bisected_Death | 1.20 | | | Upper body only |
| 27 | Idle_Unaware | 3.00 | ✔ | | Sword lowered |
| 28 | Notice | 1.00 | | | Ends in Idle |
| 29 | Taunt | 2.00 | | | Roar with the sword raised |
| 30 | Spawn | 2.00 | | | Rises from a crouch |
| 31 | Death_Front / Death_Back | 1.60 | | | Hit from the front falls backward; hit from the back falls forward |

Boss clips 32–39 are in `Art/Boss`.

## Not done yet

- The optional vertically pre-split torso, for a head-to-groin cut. It was skipped; ask if you want it.
