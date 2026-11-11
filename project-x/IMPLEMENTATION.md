# Lokale Godot-flow

Alle zichtbare gameteksten zijn Engels, inclusief intro, menu's, HUD en de acht hack-minigames.

Start met **F5**: `IntroFamily.tscn` → `IntroSpaceshipStorage.tscn` → `World.tscn`.
De intro bestaat uit twee korte verhaal-scenes met Verder en Intro overslaan.
Open `scenes/World.tscn` en druk **F6** om direct op de map te beginnen.
`scenes/BossArena.tscn` blijft beschikbaar als losse arena-test.

## Map en speler

- `World.tscn` instantieert de bestaande `main_map.tscn`.
- `PlayerSpawn` staat onderaan op `(3744, 6656)`. De camera volgt de speler tijdens het verkennen.
- `Player.tscn` gebruikt `assets/tilesets/all-position-V2.png`, met stilstaande en lopende frames voor voor-, achter- en zijaanzicht. Links wordt gespiegeld.
- WASD / pijltjes: lopen; Shift: dash; E: hack tijdens het gevecht; Escape: pauze; F11: fullscreen buiten het embedded gamevenster.
- `PXMapBoundary` maakt botsingsranden langs de gebruikte vloertegels van `groundbase`. Gebouwen blijven de bestaande decoratieve tilemap-lagen.

## Bosskamer

`BossRoom.tscn` is een aparte scene, rechtsboven op de map op `(5632, 992)`.
De boss begint op `(8256, 1344)` en beweegt of valt niet aan tijdens het verkennen.

Beide voorwaarden moeten kloppen voordat het gevecht start:

1. De speler bevindt zich minstens `EntryDepth = 96` pixels (3 vloertegels) binnen de kamergrenzen.
2. De speler bevindt zich binnen `ActivationRadius = 1200` pixels van de boss.

Deze waarden zijn instelbaar op de root van `BossRoom.tscn` in de Inspector.
De grootte van gegenereerde obstakels is instelbaar via `Terrain > ObstacleSizeRange`:
180–234 pixels in `World.tscn` (3× de oorspronkelijke breedte en hoogte),
120–156 pixels in de losse `BossArena.tscn` (2×). Beeld en botsingsvorm gebruiken dezelfde afmetingen.
De generator controleert verbonden routes en houdt ruimte rond de speler en boss vrij.
De ingang sluit met een fysieke collider. De camera toont vervolgens de hele kamer.
Na elke 5 bosshacks verandert het terrein binnen de kamer. De miniongolven bevatten 3 en 5 minions; fase 2 en 3 behouden de laser- en ringaanvallen.
Na 15 bosshacks volgt het overwinningsscherm. Bij overwinning, doodgaan of terugkeer naar het menu opent de poort. Opnieuw starten zet de speler weer beneden en reset de boss, obstakels en minions.

## Herstelde projectproblemen

- De zeven ontbrekende kaarttextures zijn teruggezet uit de lokale `.godot/imported`-cache naar `assets/images/tilemaps/nenon-tilemap/`. De herstelde PNG-bestanden staan nu in het project; de cache is niet meer nodig om ze te laden.
- Verouderde texture-UID-verwijzingen zijn uit de mapresources verwijderd; de bestaande bestandspaden worden gebruikt.
- Een afwijkende atlascel van 33 × 31 is gecorrigeerd naar 32 × 32, zodat de laatste atlas-kolom geldig is.
- Het startscene-pad en het pad naar het bestaande PixelifySans-lettertype zijn hersteld.

## Controle

Voer `dotnet build --no-restore` uit vanuit de projectmap.
Voer daarna met de Godot .NET-console executable uit:

```text
--headless --path . res://tests/WorldSmokeTests.tscn --quit-after 1800
```

De integratietest controleert de startpositie, sprite, activatievoorwaarden, fysieke poort, pauze, doodgaan, retry, alle 15 bosshacks, beide miniongolven, terreinverbindingen en lasers op wereldcoördinaten. Tutorialvoorkeuren worden na de test hersteld.

Controleer de echte intro-overgangen met `--headless --path . --script res://tests/StoryFlowTests.gd`.

De backlog is niet aangepast. Er zijn geen commits, pushes of GitHub-wijzigingen gemaakt.
