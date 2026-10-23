# Custom Modes for Cooking Simulator

Two BepInEx plugins for **Cooking Simulator**:

| Plugin | File | What it does |
|---|---|---|
| **Custom Modes API** | `CustomModes.API.dll` | Adds a **Custom Game Modes** button to the Sandbox screen and a popup listing every mode that other mods register. It contains no game mode itself. |
| **Dinner Rush** | `DinnerRush.dll` | A survival game mode. You pick a 3-dish menu, prep for 10 minutes, then keep up with orders that arrive faster and faster. |

Other mods can add their own modes to the same popup through the API (see [For mod authors](#for-mod-authors)).

---

## Requirements

- Cooking Simulator (Steam, PC)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (tested with **5.4.23.5**, Mono x64)

## Installation

1. Install BepInEx 5 into the game folder and run the game once, so BepInEx creates its folders.
2. Copy `CustomModes.API.dll` and `DinnerRush.dll` into
   `...\steamapps\common\CookingSimulator\BepInEx\plugins\CustomModes\`
   (any folder under `BepInEx\plugins` works).
3. Start the game. Both plugins are listed as loaded in `BepInEx\LogOutput.log`.

Dinner Rush needs the Custom Modes API. If the API DLL is missing, BepInEx does not load Dinner Rush.

To uninstall, delete the two DLLs. The mods never write to the game's own save files.

---

## Custom Modes API

### Using it

1. Main menu → **New game** → **Sandbox**.
2. Choose a **base-game kitchen**. A **Custom Game Modes** button appears above **Play**.
3. Click it to open the popup. Each installed mode has a name, a description and a **Play** button.
4. Press **Play** on a mode. The sandbox kitchen loads and the mode takes over once loading finishes.

Notes:
- The button only appears while Sandbox is the selected game mode. It is hidden for DLC kitchens (Baking, Pizza, Shelter, Sushi, BBQ), which use separate game systems and aren't supported yet.
- If no installed mode can be played in the selected kitchen, the button is greyed out and shows "(none available)".
- **Esc** / **B** closes the popup and returns you to the Sandbox screen. The popup supports gamepad navigation.

### For mod authors

Reference `CustomModes.API.dll`, declare a hard dependency, and register your mode in `Awake`:

```csharp
using BepInEx;
using CustomModes.API;

[BepInPlugin("com.you.mymode", "My Mode", "1.0.0")]
[BepInDependency(CustomModesApi.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
public class MyModePlugin : BaseUnityPlugin
{
    private void Awake()
    {
        CustomModesApi.RegisterMode(new CustomModeDefinition
        {
            Id = "mymode",                          // unique, stable
            DisplayName = "My Mode",
            Description = "One or two sentences shown in the popup.",
            SortOrder = 10,                         // lower = higher in the list
            IsAvailable = kitchen => true,          // optional; null = always
            OnSelected = kitchen => Logger.LogInfo($"Started in {kitchen.KitchenId}"),
        });
    }
}
```

A mod should only call `RegisterMode`. It doesn't need to add any UI of its own.

| Member | Description |
|---|---|
| `CustomModesApi.PluginGuid` | `"com.viper.custommodes.api"`. Use it in `[BepInDependency]`. |
| `RegisterMode(CustomModeDefinition)` | Adds a mode. Registration order doesn't matter; the popup re-reads the list each time it opens. A duplicate `Id` logs a warning and returns `false`. |
| `Modes` | All registered modes, sorted by `SortOrder`, then name. |
| `GetMode(string id)` | Finds a mode by id. |
| `ActiveModeId` / `ActiveMode` / `ActiveContext` | The running custom mode, or `null` outside one. Set before the kitchen starts loading, so your Harmony patches can check it early. |
| `IsModeActive(string id)` | Shortcut for `ActiveModeId == id`. |
| `StartMode(string id, KitchenContext)` | Starts a mode as if it were picked from the popup (for example, a "play again" button). Call it from the main menu, for example in `GUIKitchen.QuitToMenu`'s `onComplete`. |
| `OnModeStarted(def, kitchen)` | Raised after `OnSelected` has run. |
| `OnModeEnded(def)` | Raised when the player leaves the mode, for example by returning to the main menu. Clean up here. |

`OnSelected` runs after the kitchen has fully loaded: the scene is shown, products are unlocked and `SandboxManager.Initialize()` has run. The mode ends when the game state changes from Sandbox, which normally means a return to the main menu.

Config: `BepInEx\config\cookingsim_custommodes_api.cfg`
- `[Debug] LogUiHierarchy` (false): logs the Sandbox screen's UI layout the first time it opens.

---

## Dinner Rush

How long can you keep up? Pick a menu, prep the kitchen, then serve orders that arrive faster and faster until one expires or your dishes get too sloppy.

### How a run works

**1. Choose your menu.** The game pauses and shows **10 random dishes**, a new set every run.
- Each card shows the dish's difficulty (Easy / Medium / Hard) and its order time.
- **Right-click** a dish to see its full recipe (ingredients and every step), using the computer's own recipe view.
- Pick **exactly 3** dishes. The bottom bar shows the menu difficulty and its score multiplier as you pick.
- **Start prep** begins the run. **Quit to menu** leaves.

Dishes come from the base game, plus Food Network dishes if you own that DLC. Seasonal event dishes (Halloween, Christmas, Valentine's, Oktoberfest) are never offered.

**2. Prep: 10 minutes.** No orders arrive yet. Chop, pre-cook and set up stations.
- Your 3 recipes are pinned to the kitchen TVs.
- The TVs' bottom bar shows the prep countdown.
- **Skip preparation phase** on a TV asks for confirmation, then starts service early.

**3. Service.** Orders start coming in:

| Order | Arrives |
|---|---|
| 1st | 30 s into service |
| 2nd | 90 s after the 1st |
| Each next | the previous gap minus 5%, but never less than 15 s apart |

- Each order is a random dish from your menu. The same dish never comes up more than twice in a row.
- Order times are the same as in career mode. The TVs and the serving tablet show each order's countdown, so you can serve the oldest first. A notification appears when an order has 60 s left.
- The TVs' bottom bar shows elapsed service time, your **star total**, and **three X's** that turn red as you get strikes.

**4. Game over.** A run ends when either:
- **an order's timer reaches 0:00** (there is no overtime grace), or
- **you get your 3rd strike.** A strike is any served dish rated **3 stars or lower** (half stars count, so 3.5★ is safe and 3★ is a strike). Two strikes are allowed; the third ends the run.

A dish served on the same frame its timer runs out counts as served.

### Scoring

- **Stars:** the sum of every served dish's star rating, using the game's normal rating (taste and timing), including half stars.
- **Difficulty bonus:** each dish has a difficulty from 1 to 3, so a 3-dish menu scores 3 to 9.

  | Menu difficulty | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
  |---|---|---|---|---|---|---|---|
  | Multiplier | 1x | 1.25x | 1.5x | 1.75x | 2x | 2.25x | 2.5x |

- **Final score:** stars × multiplier.

### Results and high scores

When the run ends, a results window shows:
- why the run ended, how long you survived, orders received, dishes served
- total stars, difficulty bonus and **final score**
- your average dish rating and your strikes
- your menu and its difficulty
- your best score for this exact menu and for this kitchen, with **NEW BEST!** when you beat one

**Play again** starts a new run in the same kitchen with 10 new dishes. **Main menu** quits to the main menu.

High scores are saved to `BepInEx\config\DinnerRush.highscores.json`, one entry per kitchen + menu combination. A higher final score wins; on a tie, surviving longer wins. If the file is damaged, it is renamed to `.bad` and a new one is started, so a bad file never breaks the game.

### Rules during a run

- **Pausing** (Esc menu, dish summary window) freezes every clock: prep, service and order timers.
- **Orders can't be cancelled.** The cancel ✕ on TV orders is hidden.
- **Only Dinner Rush starts orders.** The computer's **Order** buttons are greyed out. You can still browse recipes there.
- **No saving or loading.** Autosave, quicksave and the pause menu's Save and Load are disabled for the whole run. If you quit mid-run, the run is lost. A run can't be saved and resumed.
- Money, fame and achievements work the same as in normal sandbox.

### Configuration

The file is `BepInEx\config\cookingsim_dinnerrush.cfg`, created on first launch. Edit it with the game closed.

| Section | Setting | Default | Meaning |
|---|---|---|---|
| Timing | `PrepSeconds` | 600 | Length of the prep phase. |
| Orders | `FirstOrderDelaySeconds` | 30 | Delay before the first order. |
| Orders | `SecondOrderDelaySeconds` | 90 | Gap between the 1st and 2nd order. |
| Orders | `GapDecayPerOrder` | 0.05 | Each following gap shrinks by this fraction. |
| Orders | `MinGapSeconds` | 15 | Smallest gap between orders. |
| Orders | `MaxActiveOrders` | 0 | Hold new orders while this many are open (0 = no limit). |
| Orders | `MaxSameDishInARow` | 2 | Longest run of the same dish (0 = no rule). |
| Scoring | `StrikesToLose` | 3 | The strike that ends the run (0 = never). |
| Scoring | `StrikeAtOrBelowStars` | 3 | Dishes rated this or lower are strikes (half stars allowed). |
| Scoring | `MultiplierPerDifficultyPoint` | 0.25 | Score bonus per menu difficulty point above 3 (0 = no bonus). |
| Menu | `MenuChoices` | 10 | Dishes offered in the picker. |
| Menu | `MenuSize` | 3 | Dishes you must pick. |
| Menu | `UseWorkloadDifficulty` | false | Also add each dish's workload to the difficulty score. |
| Debug | `DebugMode` | false | Turns on the testing aids below. |
| Debug | `ShowDebugHud` | false | With DebugMode: an on-screen line of internal state. |
| Debug | `SpawnOrderKey` | F6 | With DebugMode: start an extra order now. |
| Debug | `RushOrderKey` | F7 | With DebugMode: set the most urgent order to 10 s left. |
| Debug | `ForceGameOverKey` | F8 | With DebugMode: end the run now. |

Runs where a debug key was used are not saved as high scores.

### Limitations

- Base-game sandbox kitchens only. DLC kitchens use separate order systems and are planned for a later version.
- Single player only. The multiplayer mod (CookingSimMP) hasn't been tested with Dinner Rush.
- New text (button labels, popups) is English only.

---

## Building from source

Requirements: .NET SDK 6 or newer (tested with .NET 10) and an installed copy of the game with BepInEx.

```bash
dotnet build -c Release
```

- The projects target `net472` and reference the game's DLLs from the default Steam path. If the game is installed elsewhere, pass `-p:GameDir="D:\Path\To\CookingSimulator"` or create a `Directory.Build.user.props` that sets `GameDir`.
- Each build copies the plugin DLLs into `BepInEx\plugins\CustomModes`. **Close the game first**, because a running game locks the files. Add `-p:DeployToGame=false` to only build.

Repository layout:

```
src/CustomModes.API/   shared plugin: registry, Sandbox-screen button, mode popup
src/DinnerRush/        the Dinner Rush mode
  Core/                state machine, scoring, order schedule, menu rating (no Unity dependencies)
  MenuPicker/          recipe pool and 3-of-10 picker, recipe details view
  Tv/                  TV bottom-bar clock, skip button, score and strikes
  Patches/             Harmony patches (all no-ops outside a Dinner Rush run)
  Ui/                  shared UI helpers, borrowed game styling
```
