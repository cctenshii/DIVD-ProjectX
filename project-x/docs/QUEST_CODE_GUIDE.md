# Quest-code en controles

## Taalgrens

| Bestanden | Verantwoordelijkheid |
| --- | --- |
| `scripts/QuestInteraction.cs` | Eén E-interactie kiezen, puzzel tonen, resultaat verwerken, player-lock en HUD beheren |
| `scripts/QuestInteractable.cs` | Gezamenlijke bereik-, zichtlijn- en prerequisitecontrole; registratie/afmelding |
| `scripts/Enemy.cs` | Drie succesvolle hacks, één defeat en één deursignaal |
| `scripts/DoorController.cs` | Bestaande Inspector-links activeren; één consolebijdrage per deur |
| `scripts/Doors.cs` | Unieke bronnen tellen; sprite én collision uitschakelen bij openen |
| `scripts/ComputerTerminal.cs` | Eenmalige beloning en tijdelijke popup |
| `scripts/PXHackPanel.cs` | De acht oorspronkelijke puzzels; ongewijzigde regels |
| `scripts/player.gd` | Oorspronkelijke beweging/animaties, met `locked` als kleine C#-koppeling |
| `scripts/quest_arrow.gd` | Oorspronkelijke pijl; leest questdoel via `GetNavigationTarget()` |
| `scripts/QuestManager.gd`, NPC-scripts, `chat_box.gd` | Bestaande NPC-quest/dialogen; blijven GDScript |

## Pseudocode

```text
Wanneer interactieobject de scene betreedt: registreer bij QuestInteraction.
Wanneer E wordt ingedrukt:
    als puzzel/resultaat open is: geen nieuwe interactie
    kies het dichtstbijzijnde beschikbare object binnen overlap en met vrije zichtlijn
    console -> registreer unieke bron bij gekoppelde deur
    enemy -> zet player.locked; start één bestaande hackpuzzel
    terminal -> verzamel chip, mits vereiste deur open is
Wanneer puzzel eindigt:
    succes -> voeg één hit toe
    derde hit -> versla enemy; registreer één unieke bron bij deur
    toon resultaat; houd player vergrendeld
Wanneer Continue/Enter wordt gebruikt: geef playerbesturing terug
Wanneer scene verdwijnt: wis doel en modal; geef player-lock vrij
```

## Verwerking

- Objecten registreren één keer en melden zich bij scene-exit af; geen volledige scenezoektocht per frame.
- Playerreferentie wordt hergebruikt en alleen opnieuw gevonden wanneer de oude speler verdwijnt.
- Nabijheid, navigatiedoel en labels worden tien keer per seconde ververst; bij E volgt een actuele controle.
- De pijl leest een gecacht doel. Er wordt geen nieuw pathfinding-systeem voor deze kleine quest toegevoegd.
- Deurpogingen zijn idempotent via een `HashSet` van bron-ID's.
- Eén puzzelpaneel wordt hergebruikt. Popup-tijd gebruikt een bestaande processcallback, zonder timers per toetsdruk.
- Spelwijzigingen na UI-input gebeuren op een physics-tick; collision wordt deferred aangepast.
- Boss-HUD en quest-HUD onderbreken elkaar niet.

Dit zijn concrete maatregelen tegen overbodige verwerking; geen claim dat de hele game maximaal geoptimaliseerd is.

## Testen

Gebruik de **Godot .NET console executable**. Vanuit de projectmap:

```powershell
./tests/Run-Checks.ps1 -GodotPath 'C:/pad/naar/Godot_mono_console.exe'
```

Of zet `GODOT_PATH` naar dat bestand en voer `./tests/Run-Checks.ps1` uit.

De runner bouwt `ProjectX.csproj`, importeert Godot-resources en voert achtereenvolgens uit:

- `QuestFlowTests.tscn`: volledige questprogressie, C#/GDScript-interactie, negatieve scenario's en acht puzzels.
- `WorldSmokeTests.tscn`: spawn, proximity-trigger, gate, 15 boss-hacks, miniongolven, grotere terrainlayouts en restart.
- `StoryFlowTests.gd`: Family → Storage → World.
- `SceneResourceTests.gd`: alle scenes laden, instantiëren, physics- en processframes draaien en opruimen; daarna de originele NPC-dialogen en pijl controleren.

Logs: `.godot/test-results/*.log`. Een timeout zonder succesmarker of een Godot-scriptfout laat de runner falen.

## Aanpassen in de Inspector

- `HitsToDefeat`: benodigde geslaagde puzzels, standaard 3.
- `UnlockDoorsOnDefeat`: deuren die één signaal van deze enemy krijgen.
- `TargetDoors`: deuren die één signaal van deze console krijgen.
- `RequiredActivations`: unieke signalen die een deur nodig heeft, standaard 1; deur 2 heeft 4.
- `RequiredOpenDoors`: eerdere deuren die open moeten zijn om dit object beschikbaar te maken.

Houd alle interactieve player-bodies op collisionlaag 2, vaste muren/deuren op laag 1. De originele consoles hebben een interactiegebied dat richting de vloer is verschoven; zet de speler bij tests binnen dat gebied, niet buiten de vloerrand op het artwork-middelpunt.
