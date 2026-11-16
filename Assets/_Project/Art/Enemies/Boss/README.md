# Boss: a larger Revenant

| File | What it is |
| --- | --- |
| `Boss.fbx` | The Revenant scaled 1.3× (about 2.35 m, 2.5 m to the horn tips). Darker texture with a blood-red tint |
| `Boss.blend` | The same scene |
| `Textures/Boss_BaseColor.png`, `Boss_Normal.png` | 2048² |
| `Animations/Boss@<Clip>.fbx` | Boss clips, 30 fps, in place, skeleton only |

**Hierarchy:** the model and the clips use the same tree, `<file root> > Armature > Hips > ...`, so *Copy From Other Avatar* works. Each clip also contains a 1-triangle `Clip_Anchor` mesh, which keeps the Armature node on import. It has no weights; ignore it or hide it.

The Boss keeps the Revenant's skeleton, bone names, cut lines and `Cap_*` wound caps. Dismemberment and every Revenant clip work on it without changes. Import it the same way as the Revenant: Humanoid, with its own avatar. The clips are authored on the Revenant rig, and Humanoid retargeting scales them up.

Rebuild:
- Model: `blender -b Art/Revenant/Revenant.blend --python Art/Boss/Source/build_boss.py`
- Clips: `blender -b Art/Revenant/Revenant.blend --python Art/Revenant/Source/export_anims.py -- --set boss`. Definitions are in `Art/Revenant/Source/clips_boss.py`.

| # | Clip | Length | Loop | Strike (s) | Notes |
| --- | --- | --- | --- | --- | --- |
| 32 | Boss_Idle | 2.60 | ✔ | | Wide, heavy stance |
| 32 | Boss_Walk | 1.30 | ✔ | | 0.71 m/s on the Revenant scale, about 0.92 m/s on the Boss |
| 33 | Boss_Attack_Sweep | 1.90 | | 0.80–1.06 | Wide horizontal swing |
| 34 | Boss_Attack_Slam | 2.10 | | 1.05–1.27 | Two-handed overhead slam, about 1 s wind-up |
| 35 | Boss_Attack_Combo | 2.40 | | 0.60–0.80, 1.00–1.20, 1.48–1.68 | Right slash, then backhand, then overhead |
| 36 | Boss_Attack_Charge | 1.80 | | 0.75–0.92 | Lunge, done in place |
| 37 | Boss_Roar | 2.70 | | | |
| 38 | Boss_Stunned | 4.00 | ✔ | | |
| 39 | Boss_Death | 3.00 | | | Staggers, drops to one knee, falls forward |
