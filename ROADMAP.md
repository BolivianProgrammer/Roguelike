# Roadmap

What exists, what is left, and what needs a headset or new art. Keep this updated as things are finished.
Status: **done** (built and compiling; needs your feel test), **needs art**, **needs headset**, **open**.

## Fixed after measured playtests (2026-11-15)

- Enemies could never hit the player: they stopped just out of reach. Grunt now stops at 1.05 m and swings within
  1.2 m; Brute 1.3 / 1.5 m (measured blade reach 1.04–1.24 m and 1.24–1.95 m).
- The player's body ended at eye height, so head-high swings passed over it; it now reaches the top of the head.
- Neck hits counted as torso hits (a neck slash eventually cut the body under the ribs); the head's hit zone now
  covers the neck.
- Desktop: the wind-up of a swing could deal damage (often with the flat). Only the strike itself counts now.
- Desktop guard pose moved to the middle of the chest: blocked 10/10 Grunt and 8/8 Brute attacks (was 4/5).
- A weapon pushed slowly into an enemy, carried while walking into one, or knocked by an enemy running into it
  could deal damage. Now only the player's own arm motion counts, and stabs need the normal minimum speed.
- Mission01 had no boss prefab set (the guardian never appeared). Set. (Mission01 has since been removed.)

## A. Finish what was half done

- **done** Blessing cards freeze the enemies instead of pausing the game, so VR hands keep working while choosing.
- **done** Quitting mid-mission counts as dying (the run's gains are lost; shown on the next visit to the hub).
- **done** Boss Charge lunges 2.2 m forward and is started from up to 3.4 m. Its hit window now matches the clip
  (0.84-1.10 s, when the axe is out at 1.3-2.2 m) and the lunge stops 1.65 m from the player, where the axe lands.
- **done** Casters back away when rushed (Walk_Back), if there is room behind them.
- **done** Guardian loot is its own items: Revenant Cleaver (Bloodletting special), Guardian Axe, Guardian's Carapace.
- **needs art** Hub NPCs still use the enemy model.

## B. Animations that had no gameplay

- **done** Legless enemies fall (Fall_ToCrawl), crawl and attack from the ground (Crawl, Crawl_Attack), bleeding out.
- **done** Cut in half while alive: the upper body keeps fighting (Bisected_Fall/Crawl/Attack/Death); the legs fall as a ragdoll.
- **done** Disarmed enemies fight on with their fists (Unarmed_Idle, Unarmed_Attack).
- **done** Strafing between attacks, Walk_Back, turning on the spot, Idle_Unaware → Notice → Taunt.
- **done** Death_Front / Death_Back as an alternative to ragdolls (40% of deaths).
- **needs art** Vertical head-to-groin split (needs a pre-split torso mesh).

## C. Armor and trinkets

- **done** ArmorDefinition / TrinketDefinition assets through the shared stat system. New stats: stamina cost,
  damage taken, lifesteal, thorns, extra blessing choices, essence/coin gain, stagger, special cooldown.
- **done** 4 armor sets (heavy, medium, light, legendary) and 8 trinkets, in the item database and the vendor.
- **done** Worn at the Armorer (armor + 2 trinkets), saved, applied everywhere. Dying never takes them, nor the
  weapons brought in: only the run's essence, coins and finds are lost.
- **done** Hands take the armor's colour, with a cuff (stand-in for real gauntlets). **needs art** gloved hand models.

## D. Hub

- **done** Mission board (Scout NPC): pick from the missions you have unlocked; the gate leads there.
- **done** Vendor sells back, carries more stock, and part of it rotates after every run.
- **done** Quartermaster keeps any number of weapons beyond the 4 rack slots; gear is kept by the Armorer.
- **done** Wrist display (turn the off-hand wrist to your face): health, stamina, special, objective, loot; hub: level, currencies, gear.
- **done** Compact plaza (34 × 34 m, was 52 × 52): every station stands in the 22 m paved centre round the Sucre
  monument; one armory rack; only the main monument kept (lions, busts, Km 0 removed; its plaques no longer stick
  out). NPCs and props are snapped to the ground. Rebuild: Roguelike > Rebuild Hub Plaza And Stations.
- **done** No floating text: names are on physical signboards (`Signboard`, two-sided where seen from both sides,
  so never mirrored); you talk to a character only when close AND looking at them (within 30°).
- **done** The armory starts empty (no free sword). A player with no weapon who cannot afford one is topped up to
  the cheapest weapon's price, so a death never locks them out.
- **removed** Training grounds, tutorial and Drillmaster (the old test scene), and the Mission01 arena.
- **done** Night atmosphere: moonlight, dark sky, fog that hides everything past the buildings, four braziers
  (`Brazier.fbx` + `Ambient_Brazier` prefab: flames, embers, smoke, flickering light), drifting embers and ground
  mist (`Ambient_Motes`, `Ambient_Mist`). All are authored prefabs from Roguelike > Build Effect Prefabs.
- **needs art** Signs, quartermaster counter, armor mannequin, portal ring, street paving and three NPC bodies:
  see `ASSETS_NEEDED.md`.

## E. Mission content

- **done** MISSIONS.md and Roguelike > Mission Scene tools (check, add missing pieces, add chest / shrine).
- **done** Runner, Shieldbearer (shield stops frontal blows), Elite (axe, tougher, triple loot); spawners mix them in.
- **done** Boss second phase: at half health it roars and gets faster, attacks more often and hits harder.
- **done** Treasure chests and timed kill challenges (placed in Mission_Forest).
- **done** Essence and coin shards fly to the player from fallen enemies, chests and challenges.
- **done** 16 blessings in three rarities (rarer ones more likely on higher tiers).
- **done** New weapons: Fine Sword, Fine Dagger, Revenant Cleaver (new Bloodletting special), Guardian Axe.
- **done** Difficulty scales with the mission's tier and the character's level.
- **open** More missions need more level scenes (yours).
- **done** Mission_Forest dressed with the forest art set (Art/Forest): trees, mounds, paths, a stream under a wooden
  bridge, the camp, chapel ruin and stone circle, the castle, and art for the totems (they shatter into chunks), the
  portal, chests (the lid opens), the challenge shrine and loot shards. Same night look as the hub (`SceneAtmosphere`),
  fires at the camp, chapel, gate and courtyard. The edge is a dense tree line. Rebuild: Roguelike > Build Forest Mission.
- **done** Dagger: held weapons could only spin at 7 rad/s (Unity's default) while the hand allows 50, so wrist flicks
  made the dagger lag and stutter and read as too slow; its blade collider was also only 1.2 cm thick (now 2.5 cm).
- **done** Blessing cards: the title band is always a dark tint of the blessing's colour, so pale rare/legendary
  colours no longer wash out the white title; rarity is written under it.

## F. Interface

- **done** Wrist HUD (VR) and an on-screen bar HUD (desktop).
- **done** Pause menu: Esc / left Menu button. Resume, settings, abandon run, back to the plaza, quit.
- **done** Settings (saved separately from progress): volumes, comfort vignette, dominant hand, seated height,
  mouse sensitivity, key hints. Also on the title screen.
- **done** Death / victory / abandoned summary when back in the hub.
- **done** Menu restyle for readability: opaque panel, white-on-slate rows, gold hover with dark text, unaffordable
  prices in red, bigger text, header/title wrap instead of overlapping. Colours live in `UiTheme.cs`; rebuild with
  Roguelike > Build UI Prefabs.

## G. Audio

- **done** Sound system with every effect placed in 3D: swings (by speed), hits by type, blocks, parries, deflects,
  cuts, enemy voices and footsteps, player hurt/death, UI, totems, portal, pickups, chests, challenges.
- **done** Music per scene (menu, hub, mission) and a boss mood.
- **done** Real CC0 sounds (Kenney + OpenGameArt "RPG Sound Pack") in `Audio/Real/<Kind>/`, several variations per
  action, never the same clip twice in a row. Credits in `Audio/Real/CREDITS.txt`. Re-apply after adding clips with
  Roguelike > Audio > Use Sounds From Folders. Music is still procedural (drop tracks in `Audio/Real/Music/<Mood>`).
- **done** Enemy bolt: the 5 s engine-hum charge clips were replaced with ~1 s ghostly "shade" clips, release with
  "magic1"; both quieter. Tuned volumes survive re-applying the folders.
- **done** More variety: 226 clips (was 161), e.g. 20 footsteps, 15 enemy attack voices, 11-12 slash/sever/parry
  sounds; swings, steps and hits also vary their pitch more. Whooshes are measured relative to the player's rig,
  so sprinting with a weapon no longer whooshes.

## H. Visual polish

- **done** Hit sparks, weapon trails on real swings, blood decals (pooled), totem, portal and boss-spawn effects, bolt trail.
- **done** Every runtime effect (incl. shockwave, execute marker, enemy bolt) is an authored prefab spawned from the
  `FxLibrary` pool and tinted with a MaterialPropertyBlock: no CreatePrimitive or new materials at runtime.
  Rebuild with the FX builder menu. Dismemberment no longer uses DestroyImmediate during physics contacts.
- **needs art** Proper blood and effect art; post-processing (kept off for the Quest's frame budget).

## I. Quest performance and build

- **done** Runtime shader lookups replaced by a material asset (`Resources/FlatColor`), so they survive Android builds.
- **done** Roguelike > Quest Build: settings check (all match Meta's recommendations), Build APK, Build And Run.
- **done** Placeholder app icon. Pooled damage numbers, sparks and blood decals.
- **needs headset** Profile on the device. Known risks: each severed limb is a full character copy (capped at 8);
  no pooling for enemies; no LODs or occlusion culling.

## J. VR tuning (needs headset)

Holster positions, distance grab, two-handed grip, block and parry feel, bolt marker, wrist display placement and
size, menu distance and text size, comfort vignette strength, seated height steps.

## K. Housekeeping

- **done** Removed SampleScene, the URP template readme and the XRI demo scene. APKs, builds and Burst debug
  symbols are git-ignored.
- **open** The game still has no real title (title screen says ROGUELIKE / WORKING TITLE).
