# Quest-1-v3: lokaal conflictrapport

Datum: 18 november 2026. Werkbasis: lokale branch `Quest-1-v3`.
Er is niets gecommit, gepusht, opgehaald of op GitHub aangepast.

## Behouden

- De C#-gameplay, acht bestaande hackpuzzels en bossfasen blijven C#.
- De player, animaties, NPC-dialogen, QuestManager en questpijl van de teammate blijven GDScript.
- Het ontwerp van `main_map.tscn`, de questkamers, sprites, objectposities en de oorspronkelijke vier voorwaarden van deur 2 zijn behouden.
- De oorspronkelijke drie hits per enemy zijn nu drie **geslaagde puzzels**. Mislukken, annuleren of herhaald E drukken levert geen voortgang op.
- De zes importbestanden die vóór deze werkzaamheden al gewijzigd waren zijn niet teruggezet.

## Gevonden conflicten en reparaties

| Probleem | Gevolg | Lokale reparatie |
| --- | --- | --- |
| E werd gevraagd, maar `interact` stond op Q | Verkeerde bediening | Eén E-binding in `project.godot` |
| Enemies telden toetsdrukken als schade | Geen puzzel; E-spam versloeg enemies | `Enemy.cs` verwerkt uitsluitend succesvolle resultaten van `PXHackPanel` |
| Elk object las afzonderlijk dezelfde toets | Eén toets kon meerdere objecten activeren | `QuestInteraction.cs` kiest één nabijgelegen, zichtbare en beschikbare interactie |
| Deur telde losse activaties zonder bron | Dubbele callbacks of hergebruik konden voorwaarden omzeilen | `Doors.cs` telt unieke bronobjecten |
| Player kon doorlopen tijdens een puzzel | Spelerpositie en puzzeldoel liepen uiteen | C# zet de kleine `locked`-property in de bestaande GDScript-player; Continue geeft besturing terug |
| Quest had geen vloerrandcollision | Speler kon via lege ruimte langs deuren lopen | Bestaande `PXMapBoundary.cs` ook in de questscene gebruikt |
| Beloning en vervolginteracties hadden geen prerequisites | Quest kon buiten volgorde worden voltooid | Inspector-links `RequiredOpenDoors` bewaken de bestaande kamervolgorde |
| `Player.tscn` én `player.tscn` stonden in Git | Op Windows hetzelfde bestand; boss verwachtte een ander nodetype | Teammate-scene behouden als `QuestPlayer.tscn`; aparte `CombatPlayer.tscn` voor de bestaande C#-bossbesturing |
| Assembly-instelling wees naar tweede project; scriptgenerator waarschuwde over `GodotProjectDir` | Compile kon slagen zonder correcte scriptregistratie | `ProjectX.csproj` is de actieve build; projectmap expliciet ingesteld |
| Dubbele spritesheet-UID | Godot kon naar de andere kopie verwijzen | Kopie onder `assets/images/player` opnieuw van een unieke import-UID voorzien |
| Kleine bossarena plaatste vier rijen grote obstakels | Sommige random layouts verloren te veel cover bij herstel van routeconnectiviteit | Rij-/kolomafstand afgeleid van obstakelmaat en navigatieruimte; 20 vaste seeds × 3 fasen toegevoegd aan tests |
| Atlas gebruikte 33×31 waar 32×32 nodig is | Importfouten voor de laatste tilekolom | Alleen de onjuiste atlasmaat hersteld |
| Testmap bevatte ongeldige bush-tile `(0,1)` | Importfout | Ongeldige atlasdefinitie verwijderd; bestaande `(0,0)`-animatie behouden |
| NPC-scene verbond signalen met niet-bestaande parenthandlers | Runtimefouten bij contact | Oude editorverbindingen weg; bestaande GDScript-signaalverbindingen blijven |
| NPC-detectie gebruikte nog laag 1 | Nieuwe collisionlaag van de player werd niet gezien | NPC-Areas op detectielaag 2 gezet; GDScript-dialogen behouden en hun flow getest |
| NPC-roots zaten ook in groep `Player` | Verkeerde playerdetectie mogelijk | Onjuiste groepen verwijderd |
| Questpijl kende alleen NPC2/boss | Geen richting naar questconsole/enemy | Bestaande pijl leest het gecachte doel uit C#; NPC-flow blijft beschikbaar |
| Chip-popup gebruikte leeg FontFile en fout gespelde tekst | Onleesbare/onjuiste melding | Bestaande gamefont en Engelse tekst `Nano.O Chip acquired` |
| Optionele console in World deelt dezelfde interactiemanager | Quest-HUD kon boss-HUD bedekken | Quest-HUD verborgen buiten nabijgelegen console-interactie; bossgevecht krijgt voorrang |

Dit waren integratie- en runtimeconflicten; geen onafgehandelde `<<<<<<<`-mergeblokken.

## Speelvolgorde

1. F5 start `scenes/quest_map_test.tscn` op de bestaande questspawn.
2. Loop naar de eerste console en druk E: deur 1 opent.
3. Activeer beide consoles in de volgende kamer en versla beide enemies. De volgorde binnen deze kamer is vrij.
4. Iedere enemy vereist drie succesvolle hacks. E start een puzzel; Continue/Enter sluit het resultaat. Bij mislukking kun je opnieuw E gebruiken.
5. Deur 2 opent na vier unieke signalen: twee consoles en twee enemies.
6. Druk E bij de hologramterminal om de Nano.O Chip te verzamelen.

`World.tscn`, `BossArena.tscn` en de intro blijven afzonderlijk speelbaar. De World-spawn onderaan, proximity-trigger rechtsboven, grotere bossobstakels en drie fasen blijven intact. De oudere `test-open world.csproj/.sln` zijn nog aanwezig, maar zijn niet de actieve Godot-build; gebruik `ProjectX.csproj`.

## Verificatie

Laatste resultaten:

- C#-build: 0 fouten, 0 waarschuwingen.
- Godot-import: zonder resource- of scriptfouten.
- Quest: **77 checks geslaagd**.
- Boss/world/terrain: **538 checks geslaagd**, inclusief 60 reproduceerbare compacte terrainlayouts.
- Intro: Family -> Storage -> World geslaagd.
- **21 scenes** geladen en gestart; daarnaast oorspronkelijke NPC-dialogen, queststappen en pijl gecontroleerd.


De reproduceerbare testopdracht staat in `QUEST_CODE_GUIDE.md`. Logs staan lokaal onder `.godot/test-results/` en worden door het script ook op runtimefouten gecontroleerd. Een Godot-exitcode 0 alleen is onvoldoende; elke test moet zijn succesmarker produceren.

De suite controleert echte E-invoer, overlapdetectie, zichtlijn, player-lock tussen C# en GDScript, herhaald input, annuleren, timeout, alle acht puzzelregels, unieke deuractivaties, fysiek gesloten/open doorgangen, beloning en sceneherladen tijdens een puzzel. Daarnaast worden de bestaande boss-/introtests en alle scenes uitgevoerd.

Visueel bekeken: questspawn, kameroverzicht, puzzelvenster en resultaatvenster op 1280×720. De tests verplaatsen de speler tussen interacties; dit is geen volledige handmatige playthrough van alle looproutes. Er is geen formele prestatiebenchmark op meerdere apparaten uitgevoerd.
