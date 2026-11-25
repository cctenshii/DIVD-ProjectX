# Codegids en pseudocode

F5: `project.godot` → `scenes/World.tscn` → market. De gameplay blijft C#; de oorspronkelijke minipuzzels, questpijl en bewaarde design-/NPC-code blijven GDScript.

## Verantwoordelijkheden

| Onderdeel | Bestand | Verantwoordelijkheid |
| --- | --- | --- |
| Wereld | `scenes/World.tscn` | Bestaande map, marktspawn, NPC's, questkamers en boss samenbrengen |
| Main quest / gevecht | `scripts/BossArena.cs` | Toegangsvoorwaarden, pauze, hacks, fasen, minions, overwinning en retry |
| Speler | `scripts/Player.cs`, `scenes/Player.tscn` | C#-beweging, dash (`TryDash`, Shift + richting), HP, glow en animatieframes |
| Queststatus | `scripts/QuestJournal.cs` | Main/side-voortgang, geselecteerde quest, chip terugbrengen |
| NPC's | `scripts/QuestNpc.cs`, `scripts/RoamingCat.cs` | Cat/robot-interactie en rondlopende ball cats |
| Interacties / journal | `scripts/QuestInteraction.cs` | Eén E-doel, spelervergrendeling, resultaat, Tab-selectie en quest-HUD |
| Route | `scripts/QuestNavigator.cs` | Vloer-/collisiongrid, route zoeken, zichtbaar bereikbaar tussenpunt |
| Pijl | `scripts/quest_arrow.gd` | Bestaande pijl richten op het door C# gekozen tussenpunt |
| Mapdesign | `scenes/MainMap.tscn` | Bestaande tegelkaart, gebouwen en decoraties |
| Vloerranden | `scripts/MapBoundary.cs` | Collision langs de gebruikte vloertegels |
| Bosskamer | `scripts/BossRoom.cs`, `scenes/BossRoom.tscn` | Radius, instapdiepte en fysieke poort |
| Boss / terrein | `scripts/Boss.cs`, `scripts/ArenaTerrain.cs` | Bestaande AI, aanvallen, grotere cover, verbonden arena en veilige spawns |
| Minions / lasers | `scripts/Minion.cs` + `scenes/Minion.tscn`, `scripts/Laser.cs` + `scenes/Laser.tscn` | Vijand- en projectielgedrag; collision staat in de scène |
| Puzzelkoppeling | `scripts/MiniPuzzleHost.cs` + `scenes/MiniPuzzleHost.tscn` | Originele minipuzzels laden, Cancel, voltooiing en layout van de instantie |
| Puzzelregels | `minipuzzles/` | Oorspronkelijke GDScript-logica, uitleg, Restart en countdown |
| Bossinterface | `scripts/BossHUD.cs`, `scripts/InstructionPopup.cs`, `scripts/RetroUI.cs` | Engelse HUD, gevechtsuitleg en gedeelde stijl |
| Intro | `scripts/StoryScene.cs` | Bestaande verhaalpagina's en scene-overgangen |

## Naamgeving en input

- C#-klassen en scènes heten naar wat ze zijn (`Player`, `Boss`, `BossArena`, `Minion`), zonder `PX`-voorvoegsel. Namespace: `ProjectX`. Scènes zijn PascalCase (`Doors.tscn`, `ComputerTerminal.tscn`); GDScript-bestanden volgen de Godot-stijl snake_case (`quest_1_npc.gd`, `scripts/chat_box.gd`).
- Elke scène met gedrag heeft haar script op de rootnode; objecten die tijdens het spel verschijnen worden via `<Klasse>.Scene.Instantiate()` gemaakt.
- Alle besturing staat in **Project Settings → Input Map**: `left/right/up/down` (WASD + pijltjes), `dash` (Shift), `interact` (E). Er worden geen acties meer in code geregistreerd.
- `scenes/QuestPlayer.tscn` + `player.gd` horen alleen bij de oude testscènes `NpcDemo.tscn` en `QuestMapTest.tscn`; de echte speler is `scenes/Player.tscn`.

## Pseudocode: quests en route

```text
on World start:
    spawn player in market
    keep both quests unaccepted
    track main quest -> guide to cat

on E near quest NPC:
    cat -> accept main quest, select it
    robot -> accept side quest, select it
    robot + collected chip -> complete side quest, consume carried-chip state

on Tab selection:
    change selected quest only
    keep progress in both quests
    invalidate cached route

navigation setup:
    collect original floor cells
    check a bounded batch against collision each physics frame

when selected objective or door state changes:
    find connected floor path
    choose a visible next point along that path, with player clearance
    expose only this point to the original quest arrow
```

De navigatie controleert uitsluitend de bestaande collisiongeometrie en vloerranden. Decoratieve tegels krijgen hierdoor niet automatisch nieuwe gebouwcollision. Objectposities en mapdesign zijn zoveel mogelijk behouden.

## Pseudocode: boss

```text
while exploring:
    require main quest accepted, incomplete and selected
    require no open quest modal
    require player at least 96 px inside room and within boss radius
    close gate, generate cover, enable boss, enter Combat

on successful boss hack:
    apply existing damage and stun
    at hack 5 / 10:
        rebuild cover, freeze boss, spawn 3 / 5 minions
        resume next phase after all minions are disabled
    at hack 15:
        mark main quest complete
        open gate, clear temporary combat cover and projectiles
        return to exploration with quest journal available

on death:
    open gate, show defeat
on retry:
    restore player at market, reset boss encounter and generated terrain
```

## C# en GDScript

`MiniPuzzleHost` leest de bestaande puzzelstatus en laat de originele scripts de invoer en regels uitvoeren. `QuestInteraction` levert via `GetNavigationTarget()` het routepunt aan de bestaande GDScript-pijl. De actieve World-player gebruikt C#; het bewaarde designprototype gebruikt `player.gd`, met de bestaande `locked`-property voor interacties.

`scenes/NpcDemo.tscn` en `QuestPlayer.tscn` bewaren de oorspronkelijke NPC-dialogen met `QuestManager.gd`, `npc.gd`, `quest_1_npc.gd` en `chat_box.gd`. Hun afzonderlijke queststappen zijn geen tweede voortgangssysteem voor de speelbare World-scene. De pijl houdt de oude NPC-fallback voor dit prototype.

## Verwerking en onderhoud

- Interactieobjecten registreren zichzelf; de spelerreferentie wordt hergebruikt.
- Nabijheid en quest-HUD verversen tien keer per seconde; E doet een actuele bereikbaarheidscontrole.
- Het navigatiegrid wordt in batches opgebouwd. Bestaande routes worden gecachet en bij voortgang/deurwijzigingen ongeldig gemaakt.
- De minipuzzelkoppeling controleert voltooiing twintig keer per seconde; regels en timer blijven bij de originele puzzel.
- Unieke deurbronnen voorkomen dubbele activaties.
- Quest- en boss-HUD krijgen om de beurt de invoer en spelervergrendeling.

Dit beschrijft concrete besparingen in verwerking; er is geen prestatiebenchmark op meerdere apparaten uitgevoerd. Vermijd bestanden waarvan alleen de hoofdletters verschillen. Houd bestaande `res://`-paden stabiel en voeg geen tweede actieve playercontroller of camera aan World toe.

Zie [de questgids](QUEST_CODE_GUIDE.md) voor Inspector-koppelingen en de testopdracht.
