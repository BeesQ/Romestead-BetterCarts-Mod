# Better Carts

[![Release](https://img.shields.io/github/v/release/BeesQ/Romestead-BetterCarts-Mod?style=for-the-badge&logo=github&logoColor=white&color=blue "Latest release version&#10;Click to view GitHub Releases")](https://github.com/BeesQ/Romestead-BetterCarts-Mod/releases)
[![Romestead](https://img.shields.io/badge/Romestead-0.26.1-blue?style=for-the-badge&logo=steam&logoColor=white "Currently supported Romestead version&#10;Click to view Romestead on Steam")](https://store.steampowered.com/app/1805320/Romestead)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/BeesQ/BetterCarts?style=for-the-badge&logo=thunderstore&logoColor=white&color=brightgreen "Total downloads from Thunderstore&#10;Click to view Better Carts on Thunderstore")](https://thunderstore.io/c/romestead/p/BeesQ/BetterCarts)
[![License](https://img.shields.io/github/license/BeesQ/Romestead-BetterCarts-Mod?style=for-the-badge&logo=github&logoColor=white&color=orange "Project license&#10;Click to view LICENSE.txt")](https://raw.githubusercontent.com/BeesQ/Romestead-BetterCarts-Mod/939b0f4339ef7f592d6611eab6e476e13f23dcda/LICENSE.txt)

<p align="center">
  <img src="https://raw.githubusercontent.com/BeesQ/Romestead-BetterCarts-Mod/939b0f4339ef7f592d6611eab6e476e13f23dcda/packaging/assets/banner.gif" width="100%" alt="Better Carts banner">
</p>

A [BepInEx 6 CoreCLR](https://www.nexusmods.com/romestead/mods/1) mod for game **[Romestead](https://store.steampowered.com/app/1805320/Romestead)** that makes hauling with Carts more pleasant with quality-of-life features, all configurable in-game

## Features

- **Chain Overflow** - when a full Cart picks up an item, it is passed to the next Cart in the chain with a free slot. Nothing is left behind until every chained Cart is full
- **Grab Priority** - when unloading a Cart by hand, a Massive Pot is grabbed first, then an empty Bucket
- **Cart Release Fix** - releasing a pulled Cart never grabs a different Cart on the same press
- **Cart Capacity** - set how many items the vanilla Carts can carry (0-64, default 4). The Mercury cart-capacity blessing adds a configurable bonus on top (0-64, default 1), and Eject Overflow drops anything above the limit beside the Cart when a world loads. Modded Cart types are not covered
- **Cart Overlays** - the item count is shown above a Cart carrying more than 5 items, and a Cart that comes loose from the chain by itself says so. Counts for ordinary capacity and for empty Carts can be turned on too
- **Collect Range** - Carts automatically pick up loose items within a configurable range (0-10 tiles, default 2). 0 = vanilla
- **Deposit Range** - Carts deposit matching cargo into Material Storages within range (0-10 tiles, default 2). 0 = vanilla
- **Connect Range** - free Carts are pulled toward a Cart the player is pulling once in range (0-10 tiles, default 2). 0 = vanilla
- **Stockpile Range** - Carts take resources from building output stockpiles within range, into free slots and empty Buckets on the Cart (0-10 tiles, default 2). 0 = vanilla

## Configuration

All settings (master toggle, per-feature toggles etc.) are configured from the **Mod Settings** button in the main menu (added by **Mod Settings Menu**), or in **BepInEx/config/com.beesq.romestead.bettercarts.cfg**

Every feature loads by default. **Troubleshooting Mode** (in the Troubleshooting section, off by default) is only for bug reports: after turning it on and restarting the game, it shows diagnostic log options and a Load switch for each feature, so features can be tested one at a time

| Section | Key | Default | Meaning |
| --- | --- | --- | --- |
| General | Enabled | true | Master on/off for every gameplay feature |
| Chain Overflow | Enabled | true | Pass overflow to the next chained Cart |
| Grab Priority | Enabled | true | Grab a Massive Pot first, then an empty Bucket, when unloading a Cart |
| Cart Release Fix | Enabled | true | Releasing a pulled Cart never grabs a different Cart |
| Cart Capacity | Enabled / Eject Overflow / Blessing Bonus / Wooden Cart / Bronze Cart | true / true / 1 / 4 / 4 | Per-Cart-type carry capacity for the vanilla Carts (0-64) |
| Cart Overlays | Enabled / Show Above Vanilla Capacity / Show For Vanilla Capacity / Show For Empty Carts / Disconnect Message | true / true / false / false / true | Cargo count above a Cart, and a message when a Cart disconnects |
| Collect Range | Enabled / Range | true / 2 | Ranged pickup of loose items (0-10 tiles, 0 = vanilla) |
| Deposit Range | Enabled / Range | true / 2 | Ranged deposit into Material Storages (0-10 tiles, 0 = vanilla) |
| Connect Range | Enabled / Range | true / 2 | Ranged Cart pulling (0-10 tiles, 0 = vanilla) |
| Stockpile Range | Enabled / Range / While Pulled / While Parked | true / 2 / true / false | Take from output stockpiles into Carts (0-10 tiles, 0 = vanilla) |
| Troubleshooting | Enabled | false | Troubleshooting Mode for bug reports. After a restart it shows the rows below |
| Troubleshooting | Diagnostic Logs / Save Watch / Memory Watch / World Census / Cart Capacity / Cart Pickup / Cart Chain / Cart State Dump / Log File | true / true / false / true / true / true / true / false / false | Diagnostic logging, shown only in Troubleshooting Mode |
| General | `Load <feature name>`, one per feature | true | Shown only in Troubleshooting Mode, where turning one off stops that feature from loading after a restart |

In the .cfg file, Wooden Cart and Bronze Cart use their type ids as keys: `5af0ea10-21de-404a-a869-b0079653ee0b` and `4f26d74b-6fea-4ce9-b369-3bd4507dfff6`

## Requirements

- [BepinEx 6 For Romestead (Mod Loader)](https://www.nexusmods.com/romestead/mods/1) by Ice Box Studio
- [Mod Settings Menu (Settings Menu)](https://www.nexusmods.com/romestead/mods/8) by Ice Box Studio

## Multiplayer

The host's settings decide how the server-side features work for everyone. Every player should install the mod: without it, a joining player still gets the server-side features, but sees cargo beyond the vanilla slots lying on the ground and has none of the client-side features

**Server-side** - run by the host, with the host's settings

- Chain Overflow
- Cart Capacity - the host's values apply to everyone
- Collect Range
- Deposit Range
- Connect Range
- Stockpile Range

**Client-side** - applies to each player that has the mod installed

- Grab Priority
- Cart Release Fix
- Cart Overlays - the cargo count appears for every player with the mod, and the disconnect message is shown to whoever was pulling the Cart

Troubleshooting Mode's Load switches only affect the game they are set in - a player whose game does not load Cart Capacity also sees extra cargo on the ground

## Compatibility

- [Iron Cart](https://www.nexusmods.com/romestead/mods/92) by burdock12 - compatible but not supported by the Cart Capacity feature
- [Cart Capacity](https://www.nexusmods.com/romestead/mods/34) by Specsfo/Encordeo - not compatible
- [CartEnhancements](https://www.nexusmods.com/romestead/mods/62) by chuxiaaaa - not compatible (it does not currently load, with or without Better Carts)

## Install

Recommended: grab it from a mod site, which also lists the Requirements above for you

- **Nexus Mods**: https://www.nexusmods.com/romestead/mods/91
- **Thunderstore**: https://thunderstore.io/c/romestead/p/BeesQ/BetterCarts (supports Install with Mod Manager)

Manual, from this repo's [Releases](https://github.com/BeesQ/Romestead-BetterCarts-Mod/releases):

1. Install the [Romestead BepInEx Mod Loader](https://www.nexusmods.com/romestead/mods/1) and [Mod Settings Menu](https://www.nexusmods.com/romestead/mods/8).
2. Open the zip and drop the `BetterCarts` folder, found under `BepInEx/plugins/`, into your own `Romestead/BepInEx/plugins/`.
3. Launch the game through Steam.

## Build (developers)

Requires the .NET 8 SDK and a local Romestead install with the BepInEx 6 CoreCLR loader already set up (the project references DLLs from the game folder)

1. Set the game path once, then restart your terminal/IDE:
   ```
   setx ROMESTEAD_PATH "C:\Path\To\steamapps\common\romestead"
   ```
2. Build:
   ```
   dotnet build -c Release
   ```

`BetterCarts.dll` is produced in `bin/Release/` and, by default, auto-copied into `BepInEx/plugins/BetterCarts` together with `icon.png`. Pass `-p:CopyToGamePlugins=false` to skip the copy, or `-p:GamePath="..."` to override the path for a single build

No game or loader assemblies are redistributed - they are referenced from your local install at compile time only

## Notes

- The mod adds data to your save only for Carts that have carried more than the game allows on its own - more than 4 items, or more than 5 with the Mercury blessing
- With Eject Overflow on, a Cart over its capacity drops the surplus beside itself once per world load, the first time it is near a player
- High capacity values can cause stutter and stack cargo into a tall tower above the Cart
- Before removing the mod, set every Cart type's capacity back to 4 and Blessing Bonus back to 1, keep Eject Overflow on, then load each world and visit every Cart carrying extra cargo so it drops the surplus. Save, close the game, then remove the mod. Keep a backup of the save until it loads fine without the mod
- Deposit Range takes only matching resources from Cart cargo
- Stockpile Range takes only from output stockpiles, a building's input storage is never drained

## Known Issues

- The game rarely stops responding while saving. Reopening the world loads the last completed save

## Bug Reports and Feedback

Please submit through GitHub Issues on this repo

## Credits

Thanks to [Beartwigs](https://beartwigs.com) for creating [Romestead](https://store.steampowered.com/app/1805320/Romestead)

## AI Disclosure

- AI does the writing - the code and all the text that comes with it
- I write the rules it follows: what gets built, how it works, how it reads
- I test every build

## License

Released under the [MIT License](LICENSE.txt)

## Links

- Nexus Mods: https://www.nexusmods.com/romestead/mods/91
- Thunderstore: https://thunderstore.io/c/romestead/p/BeesQ/BetterCarts
- More from me: https://solo.to/BeesQ
