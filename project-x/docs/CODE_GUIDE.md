# Codegids en pseudocode

## Speelbare flow

`project.godot` → `IntroFamily.tscn` → `IntroSpaceshipStorage.tscn` → `World.tscn`.

| Onderdeel | Bestand | Verantwoordelijkheid |
| --- | --- | --- |
| Intro | `scripts/PXStoryScene.cs` | Verhaalpagina's en scene-overgangen |
| Wereld en gevechtsflow | `scripts/PXBossArena.cs` | Statusovergangen, pauze, hacks, miniongolven, retry |
| Speler | `scripts/PXArenaPlayer.cs` | Jouw beweging, dash, HP, schade en bescherming |
| Spelerdesign | `scenes/CombatPlayer.tscn` | Jade's animatieframes en pijl; oorspronkelijke collision layers |
| Questpijl | `scripts/quest_arrow.gd` | Visuele richting naar de echte boss tijdens verkennen |
| Mapdesign | `scenes/main_map.tscn` | Jade's tegelkaart, gebouwen en decoraties |
| Kaartgrenzen | `scripts/PXMapBoundary.cs` | Colliders langs de vloertegels |
| Bosskamer | `scripts/PXBossRoom.cs` | Radius + instapdiepte en fysieke poort |
| Boss | `scripts/PXArenaBoss.cs` | Bestaande AI, schade, stun en aanvallen |
| Terrein | `scripts/PXArenaTerrain.cs` | Grotere obstakels, verbonden routes, veilige minion-spawns |
| Minions / lasers | `scripts/PXArenaMinion.cs`, `scripts/PXArenaLaser.cs` | Bestaand vijand- en projectielgedrag |
| Interface / hacks | `scripts/PXBossHUD.cs`, `scripts/PXHackPanel.cs`, `scripts/PXInstructionPopup.cs`, `scripts/PXRetroUI.cs` | Engelse UI, uitleg en acht minigames |

Er is geen grote bestandsverhuizing uitgevoerd: bestaande `res://`-paden blijven zoveel mogelijk intact. In de controller zijn secties toegevoegd; comments beschrijven de belangrijkste uitvoervolgorde.

## Pseudocode: wereld

```text
on start or retry:
    cancel open hacks and instructions
    remove lasers and minions
    restore the player at the bottom spawn
    restore the dormant boss in the upper-right room
    clear terrain, open the gate, show the quest arrow
    enter Exploring

each physics tick:
    process queued UI / input / combat events
    if Exploring AND inside room by 96 px AND within boss radius:
        hide the arrow
        close the entrance and generate cover
        activate the boss and enter Combat

on a successful boss hack:
    apply the existing hack damage and stun
    after hack 5 / 10:
        freeze the boss, rebuild cover, spawn 3 / 5 minions
        after the last minion: resume the next boss phase
    after hack 15: show victory and open the gate

on death: show defeat and open the gate
on retry: perform the full reset above
```

## Pseudocode: speler en pijl

```text
player tick:
    if locked or dead: stop movement and pause animation
    otherwise: update timers, read movement, choose a SpriteFrames animation
    apply the original dash or walking velocity
    move using the original collision rules

arrow tick:
    if navigation is disabled: hide
    otherwise use target_path to find the actual boss
    only in the separate design demo: fall back to NPC quest-step targets
    if the target was freed: reacquire it or hide safely
    rotate the visible arrow tip towards the target
```

## Bewaard designprototype

`scenes/main.tscn` + `scenes/DesignPlayer.tscn` gebruiken `scripts/player.gd`, `scripts/npc.gd`, `scripts/quest1-npc.gd`, `scripts/QuestManager.gd` en `chat_box.gd`.
Deze code blijft beschikbaar voor designwerk. De NPC-quest met Q is **geen voorwaarde** voor de echte bossfight; die werkt zoals voorheen met nabijheid. De pijl gebruikt in de speelbare game een expliciet pad naar de echte boss, zodat het prototype geen gameplaystatus kan overschrijven.

## Starten en testen

- F5: de complete game vanaf de intro.
- F6 met `World.tscn`: direct verkennen vanaf de onderste spawn.
- F6 met `BossArena.tscn`: bestaande compacte gevechtstest.
- F6 met `main.tscn`: afzonderlijk design-/NPC-prototype.
- `tests/Run-Checks.ps1 -GodotPath "<Godot .NET console-executable>"`: build, imports, alle scenes, gameplay en intro. Logs staan standaard in `%TEMP%/ProjectX-checks`.

Gebruik geen bestandsnamen die uitsluitend in hoofdletters verschillen. Houd gameplaylogica in C# en de presentatie in scenes/SpriteFrames; voorkom twee actieve playercontrollers of camera's in één speelbare scene.
