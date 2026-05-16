# Disable Steam Mods

Disable Steam Mods is a small RimWorld 1.6 utility mod that prevents subscribed Steam Workshop mods from being loaded by the game.

It exists for controlled local setups where the active mod list should come only from local folders, test fixtures, or a mod manager. 
The mod allows its own Workshop copy to remain visible, so it can still be installed from Steam without immediately hiding itself.

## Requirements

- RimWorld 1.6
- Prepatcher
- .NET SDK for building from source

## How It Works

The mod uses a Prepatcher assembly rewrite pass against RimWorld's main assembly. 
It patches `Verse.Steam.WorkshopItems.AllSubscribedItems` so subscribed Workshop items are filtered out before RimWorld builds its mod list.

## Build

```powershell
dotnet build .\Source\DisableSteamMods\DisableSteamMods.csproj --configuration Release
```

The compiled assembly is written to:

```text
1.6/Assemblies/DisableSteamMods.dll
```

## Workshop Packaging

The repository includes packaging scripts:

```powershell
.\workshop_bundler.ps1
```

```bash
./workshop_bundler.sh
```

The PowerShell script builds the mod, creates the Workshop staging folder, and
generates a SteamCMD VDF. Workshop upload can also be done through the Steamworks
API uploader used locally for this item.

Workshop item:

https://steamcommunity.com/sharedfiles/filedetails/?id=3727126624