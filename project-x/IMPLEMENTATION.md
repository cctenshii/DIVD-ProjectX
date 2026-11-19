# Lokale Godot-flow

F5 start `scenes/World.tscn`. De speler verschijnt in de market op `(3552, 3520)`, op de bestaande `main_map.tscn`. De game-interface en questteksten zijn Engels. Er zijn geen commits, pushes of GitHub-wijzigingen uitgevoerd.

## Spelen

- WASD / pijltjes: bewegen. Shift: dash. E: praten, console activeren of enemy hacken.
- Praat met de **cat NPC** om de main quest te starten; praat met de **robot NPC** om de side quest te starten.
- **Tab** of **QUESTS / TAB** opent het questjournal. Kies welke quest de enige questpijl volgt. Beide quests bewaren hun voortgang tijdens het wisselen.
- De drie ball cats lopen rond in de market met de bestaande placeholderbeelden.
- De pijl volgt tussenpunten over de vloer en door beschikbare doorgangen. Gesloten deuren blijven obstakels.

De side quest gebruikt de bestaande kameropzet: eerste console opent deur 1; daarna leveren twee consoles en twee verslagen enemies samen de vier signalen voor deur 2. Iedere enemy vraagt drie geslaagde hacks. Na een hack sluit **Continue / Enter** het resultaat. Verzamel de Nano.O Chip achter deur 2 en breng hem met E terug naar de robot. Pas dan is de side quest voltooid. Er wordt nog geen reward toegekend.

## Originele minipuzzels

Alle hacks laden nu de acht scenes uit `minipuzzles/` via `scripts/PXMiniPuzzleHost.cs`. De oude eigen puzzelimplementatie is verwijderd. De bestanden en spelregels in `minipuzzles/` blijven behouden.

De C#-koppeling voegt een altijd beschikbare **CANCEL / ESC**-knop toe. De originele uitleg, Start, Restart en countdown blijven actief. Restart gebruikt de oorspronkelijke puzzelreset; de bestaande countdown blijft doorlopen. Cancel beëindigt de poging. In de side quest levert dat geen hackvoortgang op; tijdens het bossgevecht blijven de oorspronkelijke kosten voor een mislukte/geannuleerde hack gelden.

De bevestigde eindvoorwaarden zijn **3 voltooide Memory-rondes** en **500 Rhythm-punten**, binnen de oorspronkelijke **60 seconden**. De andere zes puzzels gebruiken hun bestaande `puzzle_solved`-uitkomst. Deze voltooiingscontrole zit buiten de minipuzzelsmap. Layoutcorrecties, grotere schuifbalken en schaling gelden alleen voor de geladen instanties.

## Bosskamer

De boss staat rechtsboven op `(8256, 1920)`. De kamer begint op `(6208, 1664)` en sluit pas wanneer de main quest is aangenomen en geselecteerd, de speler minstens 96 pixels binnen de kamer staat en de boss binnen een radius van 1000 pixels is.

Het bestaande gevecht behoudt 15 bosshacks, drie fasen, miniongolven van 3 en 5, lasers en wisselend terrein. De cover in World is 180–234 pixels groot; de losse arena houdt 120–156 pixels. Na overwinning opent de poort, wordt tijdelijke gevechtscover opgeruimd en kun je verder verkennen of de side quest doen.

## Bewaarde scenes en code

- `CombatPlayer.tscn`: bestaande C#-besturing met de SpriteFrames van de teammate en `all-position-V2.png`.
- `QuestPlayer.tscn`, `main.tscn` en de GDScript-NPC/dialogcode: bewaard designprototype.
- `quest_map_test.tscn`: losse oorspronkelijke questkamer voor F6-tests.
- `BossArena.tscn`: compacte, afzonderlijke gevechtstest.
- `IntroFamily.tscn` → `IntroSpaceshipStorage.tscn` → `World.tscn`: intro blijft via F6 beschikbaar; F5 begint volgens de nieuwe opdracht direct in de market.

Gebruik `ProjectX.csproj` voor de C#-build. De dubbele projectbestanden, dubbele designplayerscene, oude eigen hackscene/-code en ongebruikte low-fidelity-controller zijn opgeruimd. Originele map, art, minipuzzels en relevante prototypes zijn behouden. De backlog is niet aangepast.

## Controle en documentatie

Vanuit de projectmap:

```powershell
./tests/Run-Checks.ps1 -GodotPath 'C:/pad/naar/Godot_mono_console.exe'
```

De runner controleert build, import, quests, de acht minipuzzels, boss/terrain, intro en scenes. Logs staan onder `.godot/test-results/`. Zie [het conflictrapport](docs/MERGE_REPORT.md) voor bevindingen en verificatiestatus, [de codegids](docs/CODE_GUIDE.md) voor verantwoordelijkheden en [de questgids](docs/QUEST_CODE_GUIDE.md) voor de koppelingen en pseudocode.
