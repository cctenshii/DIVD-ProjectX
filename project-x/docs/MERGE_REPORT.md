# Lokaal merge-rapport — 13 november 2026

## Uitgangspunt

Onderzocht: `project-x`, lokale HEAD `03489d3` op `Merge-Branch-(Prologue-&-JadeDesignV2)`.
Vergeleken met `8b1e8ac` (`Prologue`, V4.0.0) en de twee squash-commits `9c69c6e` / `03489d3`.
Bij aanvang was de werkmap schoon. Er waren geen onafgehandelde Git-conflicten en geen conflictmarkers. De problemen zaten in **samengevoegde onderdelen die functioneel niet op elkaar aansluiten**.

De gewenste verdeling is toegepast: jouw oorspronkelijke C#-gameplay blijft leidend; Jade's kaartdesign, animatieframes en questpijl blijven behouden. Haar NPC-demo blijft afzonderlijk beschikbaar en blokkeert de echte bossfight niet.

## Bevindingen en herstel

| Ernst | Conflict / incompatibiliteit | Effect | Lokale oplossing |
| --- | --- | --- | --- |
| Kritiek | `project.godot` startte het designprototype en verloor C#-feature, assemblynaam en viewportinstellingen | F5 sloeg jouw intro en `World` over; projectconfiguratie sloot niet meer aan op de C#-game | Startscene, ProjectX-assembly en oorspronkelijke viewport hersteld; design-inputacties en QuestManager behouden |
| Kritiek | Git bevatte zowel `scenes/Player.tscn` als `scenes/player.tscn` | Windows kan deze niet als twee aparte bestanden behandelen; beide verwijzingen kwamen uit op prototype-inhoud | Beide botsende namen vervangen door `CombatPlayer.tscn` en `DesignPlayer.tscn`; sceneverwijzingen aangepast |
| Kritiek | Gameplay verwachtte een root `PXArenaPlayer`; het prototype had een `Node2D`-root met een GDScript-player eronder | Arena kon de player niet als C#-controller ophalen; prototype mist HP-, dash-, lock- en damagecontract | Speelbare scene heeft weer een `CharacterBody2D`-root met jouw C#-script en oorspronkelijke collision layers |
| Hoog | Prototype leverde `AnimatedSprite2D` en een eigen camera; C# verwachtte `Sprite2D Body` en de wereldcamera | Rechtstreeks mengen veroorzaakt type-/nodefouten of een concurrerende camera | Jade's SpriteFrames behouden als `Body`, C#-animatieaanroep aangepast, alleen de bestaande wereldcamera gebruikt |
| Hoog | Questpijl hing uitsluitend af van NPC-queststappen en een decoratieve bossmarker | Geen aanwijzing in jouw gameplay, of aanwijzing naar een marker in plaats van de werkelijke boss | Expliciet `target_path` naar de echte boss; zichtbaar tijdens verkennen, uit bij combat/menu/einde, terug na retry |
| Middel | Gecachete NPC-/bosstargets konden al vrijgegeven zijn | Pijl kon een verwijderde node gebruiken na een scene-/targetwissel | `is_instance_valid`, opnieuw zoeken en veilig verbergen; positieve schaal en expliciete rotatieoffset voor de pijlrichting |
| Middel | Twee identieke spritesheet-bestanden hadden dezelfde Godot-UID | Editor meldde een dubbele UID; resourceverwijzingen konden ambigu worden | Beide PNG's behouden, de oorspronkelijke kopie onder `assets/tilesets` heeft nu een unieke UID |
| Hoog | `main_map.tscn` had opnieuw een atlascel van 33×31 voor een 32×32-sheet | Godot gaf fouten voor de laatste atlaskolom | Uitsluitend de celafmeting gecorrigeerd naar 32×32; geschilderde tegels en designlagen ongewijzigd |
| Laag | `test_map.tscn` definieerde bush-tile `(0,1)` buiten de texture | Ook deze testscene gaf atlasfouten bij laden | Ongeldige, ongebruikte tiledefinitie verwijderd; test controleert dat er geen cellen naar verwijzen |
| Middel | NPC-signalen verwezen naar niet-bestaande methods op `NPC1`, terwijl `Area2D` ze al in code verbond | Verkeerde / overbodige signaalbedrading | Twee foutieve editorconnecties verwijderd; bestaande scriptverbindingen behouden |
| Laag | NPC-roots zaten in de groep `Player` | Onjuist scenecontract voor toekomstige groepsdetectie | Alleen de foutieve groepslabels verwijderd |
| Middel | Queststap ging verder zonder geldige dialoguebox | Demo kon een gesprek overslaan terwijl niets getoond werd | `start_dialogue()` retourneert succes; alleen dan gaat de quest verder |

Daarnaast is de eerder gevraagde Engelse decimale timeropmaak hersteld. Deze ontbrak in de aangeleverde versie. Dit is een herstel ten opzichte van de vorige lokale werksessie; niet alle verschillen ten opzichte van die sessie zijn uitsluitend aan deze squash-merge toe te schrijven.

## Bewust behouden

- Jade's volledige kaartindeling en decoratielagen. De mapdiff bevat slechts de atlascelcorrectie.
- Haar animatieframes, timing, PNG-assets en questpijl-afbeelding.
- Jouw player-snelheid, dash, gezondheid, schade, bescherming en physics layers.
- Onderste spawn `(3744, 6656)`, boss-spawn `(8256, 1344)`, radius 1200 en kamerinstapdiepte 96.
- Engelse intro, menu's, HUD en acht hacks.
- 15 bosshacks, drie fases, twee golven van 3 en 5 minions, lasers, grotere terreinobjecten, pauze en retry.
- De eigen demo van Jade in `main.tscn` met `DesignPlayer.tscn`. Die is geen tweede gameplayroute onder F5.

## Ordening en uitleg

Geen brede architectuurherschrijving of massale bestandsverhuizing. De noodzakelijke naambotsing is opgelost, de playercontroller heeft duidelijke code-secties, en comments beschrijven de uitvoervolgorde van player, wereldreset, encountertrigger, terreingeneratie en questpijl.
Zie [CODE_GUIDE.md](CODE_GUIDE.md) voor de verantwoordelijkheden, scene-flow en pseudocode. [IMPLEMENTATION.md](../IMPLEMENTATION.md) is bijgewerkt naar de nieuwe bestandsnamen.

## Tests en resultaten

De controle wordt uitgevoerd met `tests/Run-Checks.ps1`. Deze controleert zowel exitcodes als enginefouten/-waarschuwingen in de logs; Godot kan bij een laadfout namelijk toch exitcode 0 retourneren.

| Controle | Resultaat |
| --- | --- |
| C#-build | Geslaagd; 0 compileerfouten en 0 waarschuwingen |
| Godot .NET headless editor/import | Geslaagd; geen duplicate-UID-, parse- of atlasfouten in de eindcontrole |
| Alle 16 `.tscn`-scenes laden én instantiëren | Geslaagd, inclusief de bewaarde design-/NPC-demo |
| NPC-dialogen en demo-pijl naar NPC2 en bossmarker | Geslaagd; ook vrijgegeven target getest |
| Kaartbereikbaarheid na het nieuwe design | Doorlopende vloerroute met ruimte rond de route van spawn naar boss gevonden |
| Player-integratie | Juiste SpriteFrames, één camera, echte bewegings- en Shift-dash-input, goede animatierichting |
| Questpijl in gameplay | Naar echte boss, correcte zichtbare puntoriëntatie, verborgen in combat en hersteld na retry |
| Boss-regressie | Activatieradius, kamerdiepte, fysieke poort, pauze, doodgaan, retry, 15 hacks, beide miniongolven, terreinverbindingen en lasers geslaagd |
| Intro-overgangen via Enter | `IntroFamily` → `IntroSpaceshipStorage` → `World` geslaagd |
| Gerenderde controle | Spawn met pijl, bosskamer met behouden design en terreinwissel naar fase 2 bekeken |

De gameplaytest simuleert geslaagde hackresultaten om alle gevechtsovergangen te controleren. Dit is geen volledige menselijke playthrough van elk puzzeltype. Het aantal assertions varieert doordat terreingeneratie willekeurig is. Decoratieve gebouwen blijven, net als voorheen, decoratief; vloergrenzen en arena-obstakels hebben botsing. De pijl wijst direct naar het doel, niet langs een berekende route.

Een eerste dash-test stuurde input binnen een handmatig aangeroepen tick en miste daardoor `IsActionJustPressed`. De test gebruikt nu echte engineframes en Shift-events; de gameplay-dashcode hoefde niet aangepast te worden. De controle van alle scenes vond ook de bovengenoemde losse testmapfout, die daarna is hersteld.

## Lokaal gebruik

Gebruik Godot **.NET**, open `project.godot` en start met F5. Voor direct spelen: open `scenes/World.tscn` en druk F6.

```powershell
./tests/Run-Checks.ps1 -GodotPath "C:\pad\naar\Godot_mono_console.exe"
```

Alles is lokaal uitgevoerd. Geen commits, staging, pushes of wijzigingen op GitHub. De backlog en het naastgelegen afzonderlijke project zijn niet aangepast.
