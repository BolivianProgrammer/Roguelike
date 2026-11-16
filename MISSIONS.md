# Making a mission from your own level

A mission is any scene with the pieces below. Build the level however you like (ProBuilder, imported models,
anything with colliders), then add the gameplay pieces. Most of them can be added for you.

## Quick way

1. Open your level scene.
2. **Roguelike > Mission Scene > Add Missing Pieces.** It adds only what is missing and never moves or deletes
   anything: the Player, the Level NavMesh, the Mission object, an extraction portal, a boss spawn point, and it
   points the Mission at every totem in the scene.
3. Place totems and enemy spawners (below), move the player, portal and boss spawn point where you want them.
4. **Roguelike > Mission Scene > Check Open Scene** lists anything still missing.
5. Add the scene to **File > Build Settings**.
6. Create a mission entry: **Create > Roguelike > Mission** in `Assets/_Project/Data/Missions`, type the scene name,
   pick a tier (0 = easiest), optionally the mission that unlocks it, then add it to
   `Assets/_Project/Resources/MissionCatalog`. It now appears on the hub's mission board.

## What a mission scene needs

| Piece | How | Notes |
| --- | --- | --- |
| **Player** | `Assets/_Project/Prefabs/Player.prefab` | Where the run starts. Works in VR and desktop. |
| **Level NavMesh** | Empty object + `LevelNavMesh` | Works out where enemies can walk when the scene loads. Every collider that is part of the level (no Rigidbody) counts as ground or wall. |
| **Mission** | Empty object + `MissionManager` | Lists the Totems, the Extraction zone, the Boss prefab (`Prefabs/Enemies/Brute`) and the Boss Spawn Point. Leave Buff Pool empty to use every blessing. |
| **Totems** | Any object with a collider + `Totem` | Destroyed by hits, or by holding a hand on it. Set its Body renderer so it lights up. |
| **Extraction zone** | A flat ring + `ExtractionZone` | Standing in it opens the portal out. Set its Ring renderer. |
| **Boss spawn point** | Empty object | The guardian appears here when the last totem falls. |
| **Enemy spawners** | Empty object + `EnemySpawner` | Prefab: `Grunt` (mixes in Runners, Shieldbearers and Elites, more on harder tiers) or `Caster`. Add spawn points as children. *Activation Range* keeps them asleep until the player comes close. |

## Optional side content

| Piece | How | What it does |
| --- | --- | --- |
| **Treasure chest** | **Roguelike > Mission Scene > Add Chest Here** (where the Scene view looks) | Walk up, press E / A-X: essence and coins fly out, sometimes an item. Hide them. |
| **Challenge shrine** | **Add Challenge Shrine Here** | Starts a timed fight: kill N enemies in time for a big reward. Needs open ground around it (Spawn Radius). |

Both build a placeholder look when the game starts; put your own model as a child to replace it (a chest's lid
goes in its Lid field).

## Tiers and difficulty

A mission's tier sets enemy health (+25% per tier), damage (+15% per tier) and loot (+30% per tier), plus a little
more for each character level. Rarer blessings and guardian loot also come up more often on higher tiers.
The three missions shipped all use `Mission_Forest` at tiers 0, 1 and 2; each unlocks the next.

## Rules every mission follows (handled by the code)

- Whatever the player carries and wears through the hub's gate always comes back. Dying, abandoning from the pause
  menu or closing the game mid-mission loses only what the run gave: its essence and coins, gear found there and
  weapons picked up there.
- Leaving through the portal keeps everything carried, plus anything found (gear found in chests or from the
  guardian is only kept this way).
- Killing the guardian marks the mission complete (unlocking the next one) once the player extracts.
