# Disable Steam Mods

Disable Steam Mods is a small RimWorld 1.6 utility mod that controls which subscribed Steam Workshop mods are visible to the game.

It exists for controlled local setups where the active mod list should come only from local folders, test fixtures, or a mod manager. 
By default, it blocks every subscribed Workshop mod except itself and Prepatcher.
The mod allows its own Workshop copy and its dependency to remain visible through the default package ID whitelist, so it can still be installed from Steam without immediately hiding itself or Prepatcher.

## Requirements

- RimWorld 1.6
- Prepatcher
- .NET SDK for building from source

## How It Works

The mod uses a Prepatcher assembly rewrite pass against RimWorld's main assembly.
It patches `Verse.Steam.WorkshopItems.AllSubscribedItems` so subscribed Workshop items are filtered before RimWorld builds its mod list.

Runtime Harmony patches are not used for the Workshop suppression path.

## Configuration

The mod has two optional filter systems in its mod settings:

- Whitelist: when enabled, only listed RimWorld package IDs are allowed.
- Blacklist: when enabled, listed RimWorld package IDs are blocked.

The default whitelist contains this mod and Prepatcher, so the mod does not hide itself or its dependency.

Default behavior is equivalent to the old behavior: whitelist enabled with only `nemuruyama.disablesteammods` and `zetrith.prepatcher`, blacklist disabled.
That means all other subscribed Workshop mods are hidden.

Package IDs can be entered as comma, space, or newline separated RimWorld mod package IDs.
Wildcards are supported, so `nemuruyama.*` matches every package ID under that prefix.
## Build

```powershell
dotnet build .\Source\DisableSteamMods\DisableSteamMods.csproj --configuration Release
```

## Workshop Packaging

The repository includes packaging scripts:

```powershell
.\workshop_bundler.ps1
```

```bash
./workshop_bundler.sh
```

The PowerShell script builds the mod, creates the Workshop staging folder, and generates a SteamCMD VDF.
Workshop upload can also be done through the Steamworks API uploader used locally for this item.

Workshop item:

https://steamcommunity.com/sharedfiles/filedetails/?id=3727126624

If both whitelist and blacklist are disabled, subscribed Workshop mods are blocked.

When "Only remove Workshop mods with a local version" is enabled, a Workshop mod is filtered only if a local mod with the same package ID exists in the local `Mods` folder.
