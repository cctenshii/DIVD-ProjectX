# Quest-code en controles

## Speelvolgorde

1. F5 start in de market. E bij de cat neemt de main quest aan; E bij de robot neemt de side quest aan.
2. Open het journal met Tab en kies één te volgen quest. Selecteren bepaalt de enige pijl; het wist geen voortgang.
3. Side quest: eerste console → deur 1 → twee consoles en twee enemies → deur 2 → chipterminal → terug naar de robot.
4. Iedere enemy vereist drie geslaagde puzzels. Continue/Enter sluit het resultaat. Mislukken of Cancel geeft geen succesvolle hack.
5. Chip ophalen verandert het doel naar de robot. E bij de robot voltooit de side quest zonder reward.

## Verantwoordelijkheden en koppelingen

| Bestand | Verantwoordelijkheid |
| --- | --- |
| `QuestJournal.cs` | Onafhankelijke queststatus, geselecteerde quest en turn-in |
| `QuestNpc.cs` | Bestaande bereikcontrole gebruiken voor cat/robot-interactie |
| `QuestNavigator.cs` | Bereikbaar tussenpunt op het vloernetwerk leveren |
| `QuestInteraction.cs` | E-doel, modal, Tab-journal, resultaat en spelervergrendeling |
| `QuestInteractable.cs` | Bereik, zichtlijn, registratie en vereiste deuren |
| `Enemy.cs` | Drie succesvolle hacks, één defeat en één deursignaal |
| `DoorController.cs` | Bestaande Inspector-links activeren |
| `Doors.cs` | Unieke bronnen tellen, sprite/collision uitschakelen, route verversen |
| `ComputerTerminal.cs` | Chip één keer verzamelen en melding tonen |
| `PXMiniPuzzleHost.cs` | GDScript-puzzels in de game plaatsen en één resultaat teruggeven |
| `quest_arrow.gd` | Bestaand artwork richten via `GetNavigationTarget()` |

Deze scripts staan onder `scripts/`. De map `minipuzzles/` blijft de bron van alle acht minigames. Er is geen duplicaat van hun spelregels in C# gemaakt.

## Pseudocode: interactie

```text
on E:
    if puzzle, result or journal is open: do not start another interaction
    find nearest available object within interaction area and clear line of sight
    NPC -> accept quest or return chip
    console -> register one source at linked door
    enemy -> lock player, open one native puzzle
    terminal -> collect chip only when required doors are open

on native puzzle completion:
    Memory: require 3 completed rounds
    Rhythm: require score >= 500
    other puzzles: use their puzzle_solved flag
    emit exactly one success

on Cancel / Escape / native timeout:
    stop and remove the current puzzle instance
    emit exactly one unsuccessful result

on result:
    success -> add one enemy hit
    third hit -> mark enemy disabled, register its door source
    show result; keep player locked until Continue / Enter

on chip pickup:
    retain collection history, set carried-chip state
    guide selected side quest back to robot
on robot turn-in:
    complete side quest, clear carried-chip state, grant no reward
```

`HasChip` betekent dat de terminalchip verzameld is en de side quest nog niet is voltooid. `ChipCollected` blijft als verzamelde voortgang bestaan, zodat opnieuw ophalen of teruggeven geen dubbele voortgang oplevert.

## Puzzels behouden

De externe host laadt `minipuzzles/shared/puzzle_flow.tscn`. De originele **How it works**, **Start**, **Restart** en **60-seconden-countdown** blijven werken. De host biedt daarnaast **CANCEL / ESC**, ook tijdens de puzzel. Restart voert de oorspronkelijke reset uit en reset niet zelfstandig de gedeelde countdown.

Alleen de geladen instantie krijgt layoutaanpassingen: positie/schaling, leesbare equalizerkolommen en bruikbare horizontale schuifbalken. De bronbestanden, tolerantiegrenzen, randomisatie en puzzelinvoer blijven oorspronkelijk. De random puzzelkeuze gebruikt een zak met acht keuzes, zodat alle puzzels voorbij kunnen komen zonder directe herhaling.

## Aanpassen in de Inspector

- `QuestNpc.StartsMainQuest`: cat = aan, robot = uit.
- `Enemy.HitsToDefeat`: benodigde succesvolle puzzels; standaard 3.
- `Enemy.UnlockDoorsOnDefeat`: deur die één signaal ontvangt.
- `DoorController.TargetDoors`: deuren die één consolesignaal ontvangen.
- `Doors.RequiredActivations`: deur 1 = 1; deur 2 = 4 unieke bronnen.
- `QuestInteractable.RequiredOpenDoors`: eerdere deuren die open moeten zijn.
- `RoamingCat.Speed` / `RoamRadius`: beweging van de ball cats.
- `BossRoom.EntryDepth` / `ActivationRadius`: instapdiepte en bossradius.

Player-bodies gebruiken collisionlaag 2; vaste muren en deuren laag 1. Houd interactiegebieden op bereikbare vloer. De consoles gebruiken een verschoven interactiegebied; het artwork-middelpunt is niet altijd de plek waar de speler moet staan.

## Reproduceerbare controles

Vanuit de projectmap, met een Godot **.NET** console-executable:

```powershell
./tests/Run-Checks.ps1 -GodotPath 'C:/pad/naar/Godot_mono_console.exe'
```

Of stel `GODOT_PATH` in en voer `./tests/Run-Checks.ps1` uit. De runner bouwt `ProjectX.csproj`, importeert resources en voert uit:

| Test | Controle |
| --- | --- |
| `QuestFlowTests.tscn` | Marktspawn, NPC's, Tab, questprogressie, volledige routegeometrie, deuren, Cancel/Restart en chip teruggeven |
| `MiniPuzzleTests.gd` | Alle acht originele puzzels, uitleg/Start, layout, succes, timeout en annuleren |
| `WorldSmokeTests.tscn` | Main-questvoorwaarden, bossradius, poort, 15 hacks, minions, grote cover, 60 vaste compacte terrainlayouts en verder spelen na overwinning |
| `StoryFlowTests.gd` | Family → Storage → World |
| `SceneResourceTests.gd` | Scenes laden/starten/opruimen en de oorspronkelijke NPC-dialogen/pijl |

Logs: `.godot/test-results/*.log`. Een exitcode 0 zonder succesmarker, een timeout of een scriptfout laat de runner falen.

De tests gebruiken echte E-invoer voor interacties, maar verplaatsen de speler tussen routepunten en sturen puzzelbediening deels programmatisch aan. Ze vervangen geen volledige handmatige playthrough. Gerenderde vensters worden apart visueel bekeken; er is geen formele prestatiemeting op meerdere apparaten. De actuele verificatiestatus staat in [het conflictrapport](MERGE_REPORT.md).
