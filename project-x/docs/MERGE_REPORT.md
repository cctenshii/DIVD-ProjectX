# Lokaal conflictrapport na drie merges

Afgerond: 19 november 2026. Werkbasis: `Merge-2.0-(Puzzels,-Prologue,-Design-en-Quest)`, na de lokale merges met onder andere Quest-1-v3. Alle wijzigingen zijn lokaal in de bestanden uitgevoerd; geen commits, pushes of GitHub-wijzigingen.

## Behouden en samengebracht

De bestaande C#-gameplay, bossfasen, dash, schade, deurvoorwaarden en questkamerposities blijven de basis. De map en NPC-art van de teammate, de animatieframes, de GDScript-questpijl en de oorspronkelijke minipuzzels zijn behouden. De oude design-/NPC-scene en intro blijven afzonderlijk beschikbaar. F5 start nu de samengevoegde World-scene in de market.

De actieve World-player gebruikt C# met `AnimatedSprite2D` en de bestaande `all-position-V2.png`-frames. De GDScript-player blijft beschikbaar in de design-/questtestscenes. Er zijn geen algemene conversies van `.gd` naar `.cs` of omgekeerd uitgevoerd.

## Integratieproblemen en reparaties

| Probleem na samenvoegen | Gevolg | Lokale reparatie |
| --- | --- | --- |
| Startscene en questkamer stonden los van de bedoelde marktflow | Geen gezamenlijke start voor beide quests | `World.tscn` is F5-startscene, met marktspawn, cat en robot |
| Oud eigen hackpaneel was nog de gameplay-puzzel | Nieuwe minipuzzels werden niet gebruikt | `PXMiniPuzzleHost.cs` laadt de oorspronkelijke acht `minipuzzles`-scenes |
| Memory en Rhythm hebben geen definitieve native overwinning | Hack kon niet vanzelf worden voltooid | Externe eindvoorwaarden: 3 rondes / 500 punten, zoals bevestigd |
| Tijdens een puzzel ontbrak een terugweg naar de game | Alleen opnieuw beginnen was beschikbaar | Externe Cancel-knop en Escape beëindigen de poging zonder succes |
| Native puzzelpanelen hebben verschillende afmetingen en krappe sliders | Bediening kon te klein of onhandig geplaatst zijn | Layout van de geladen instantie aangepast; bronbestanden behouden |
| Gecombineerde player verwachtte een ander Body-nodetype | C# en playerscene sloten niet op elkaar aan | `CombatPlayer.tscn` gebruikt het verwachte AnimatedSprite2D met bestaande frames |
| Pijl kende geen gezamenlijke questselectie | Tegenstrijdige doelen of richting door leegte/muren | Journal kiest één quest; navigator levert één bereikbaar tussenpunt |
| Deurcollider wordt later in de physics-stap uitgeschakeld | Een lege route kon na het openen gecachet blijven | Collisiongrid na twee physics-frames verversen en daarna de route opnieuw berekenen, ook bij stilstaande speler |
| Tab tijdens combat kon een journal-verzoek achterlaten | Questvenster opende onverwacht na het gevecht | Questinvoer buiten verkennen afwijzen en achtergebleven verzoeken wissen |
| Pijlscript miste de property die de arena gebruikte | Boss kon de navigatie niet betrouwbaar uitschakelen | `navigation_enabled` toegevoegd aan het bestaande GDScript |
| Bosskamercoördinaten pasten niet bij de gemergde kaartvloer | Boss en kamer konden buiten hun bedoelde vloer liggen | Kamer, boss, ingang en terrainbounds uitgelijnd op de bestaande kaart |
| Boss kon onafhankelijk van de gekozen quest starten | Side quest en main quest konden elkaar onderbreken | Main quest moet aangenomen, onvoltooid en geselecteerd zijn |
| Een definitief overwinningsscherm blokkeerde vervolgverkenning | Geen side quest na main quest | Main voltooit, poort opent, tijdelijke cover wordt opgeruimd en verkennen hervat |
| Chip ophalen was het eindpunt | Geen terugbrengen naar robot | Apart carried-chip/turn-in-verloop; voltooiing pas bij robot, nog geen reward |
| Meerdere project-/playerscenevarianten | Onduidelijke build en dubbele resource-UID | Eén actief `ProjectX.csproj`; dubbele designplayerscene verwijderd |
| Oude documentatie wees naar eerdere starts en puzzels | Instructies pasten niet bij de gemergde game | Implementatie-, code- en questgids bijgewerkt |

De bestaande bescherming tegen E-spam en dubbele deursignalen blijft gelden: uitsluitend geslaagde puzzels tellen als enemyhit; een deur telt unieke bronobjecten. De tweede deur heeft nog steeds de oorspronkelijke vier bijdragen: twee consoles en twee enemies. Beschikbaarheid, interactieafstand en zichtlijn blijven voorwaarden.

## Opruiming

Verwijderd omdat ze vervangen, dubbel of zonder referenties waren:

- `scripts/PXHackPanel.cs`, bijbehorende UID en `scenes/HackPanel.tscn`: oude eigen minipuzzels.
- `scenes/DesignPlayer.tscn`: dubbele scene; het originele designprototype gebruikt `QuestPlayer.tscn`.
- `test-open world.csproj` en `test-open world.sln`: dubbele projectconfiguratie; `ProjectX.csproj/.sln` blijven.
- `ProjectXLowFidelityPlayer.cs` en bijbehorende UID: ongebruikte tijdelijke controller.
- Verouderde tutorialvoorkeuren uit de bosscontroller: de originele puzzelintro verzorgt nu de uitleg.

`minipuzzles/`, `main_map.tscn`, originele art, intro, compacte arena, designprototype en bruikbare testscenes blijven behouden. De backlog is niet aangepast.

## Verificatie

**Status: volledige lokale suite geslaagd op 19 november 2026.** De wisselende routefout na het openen van een deur is hersteld; de complete routecontrole slaagt.

| Controle | Resultaat |
| --- | --- |
| C#-build | 0 fouten, 0 waarschuwingen |
| Godot-import | Geslaagd, geen script-/resourcefouten |
| Questflow | 46 checks, inclusief routes, questselectie, annuleren en chip terugbrengen |
| Acht originele minipuzzels | 37 checks, inclusief uitleg/Start, layout, native overwinning, timeout en Cancel |
| Boss/world/terrain | 538 checks, inclusief beide questvolgordes, terugkeer naar verkennen en 60 vaste compacte terreinlayouts |
| Intro | Family → Storage → World geslaagd |
| Scenes | 20 scenes geladen/gestart; originele NPC-dialogen en pijl geslaagd |
| Bronbehoud minipuzzels | Alle 35 bestanden SHA-256-identiek aan de beginsituatie; geen toevoegingen of verwijderingen |
| Lokale diff | Geen whitespacefouten; geen wijzigingen onder `minipuzzles/` |

De bosstest simuleert succesvolle hackresultaten om alle fasen te doorlopen; de aparte minipuzzeltest controleert de daadwerkelijke regels van alle acht originele spellen. Routecontroles volgen veilige segmenten vanaf de market, door geopende deuren naar de chip, terug naar de robot en naar de boss.

De runner is `tests/Run-Checks.ps1`; de opdracht staat in [de questgids](QUEST_CODE_GUIDE.md). Hij controleert build, Godot-import, quests, minipuzzels, boss/terrain, intro en scenes. Logs staan in `.godot/test-results/`. Engine-/scriptfouten en ontbrekende succesmarkers laten de run falen, ook als het proces exitcode 0 teruggeeft.

Visueel bekeken zijn de marketspawn, het questjournal en de acht puzzelvensters op 1280×720. De tests volgen routepunten en gebruiken echte E-invoer, maar zijn geen volledige handmatige playthrough. Er is geen prestatiebenchmark op meerdere apparaten uitgevoerd. De genoemde optimalisaties zijn beperkte caches, registraties, verversfrequenties en batches; geen claim dat de gehele game maximaal is geoptimaliseerd.
