# Weapons

Procedural low-poly weapons, built by `Source/build_weapons.py`:

```
blender -b --factory-startup --python Art\Weapons\Source\build_weapons.py -- <previewDir>
```

| File | Tris | Notes |
|---|---|---|
| `Sword.fbx` | 1574 | double-edged, edges on ±X |
| `Dagger.fbx` | 1182 | double-edged |
| `Hammer.fbx` | 1596 | striking face on +X, spike on −X |
| `Enemy_Cleaver.fbx` | 1414 | rusty, single edge on +X (Revenant default) |
| `Enemy_Axe.fbx` | 1252 | rusty bearded axe, edge on +X |

- In Unity: grip centre at the origin, weapon extends along **+Z**, cutting edge faces **+X**.
- One material per weapon (`M_<Name>`), all sharing `Textures/Weapons_Palette.png`
  (256×32 colour swatches; set the texture to *Point* filtering, no mipmaps needed).
- No rig, no animation.
