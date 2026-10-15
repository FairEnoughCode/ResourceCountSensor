# Resource Count Sensor

A logic sensor for **Oxygen Not Included**. It counts how much of the chosen resource you have, liquids and gases included, and sends a green or red signal depending on a threshold. You can count across the whole map, within range of the sensor, or in the room it stands in.

*Русская версия: [README.ru.md](README.ru.md)*

## What it does

The sensor is a 1×1 building in the **Automation** category. It unlocks together with the basic sensors.

Setting up the sensor:

- **Where to count**
  - **On Map**: the whole asteroid, the same number as in the resources panel on the right.
  - **Range**: a square of 1–10 tiles in every direction around the sensor. The area is highlighted while the sensor is selected.
  - **Room**: the enclosed area the sensor stands in, up to 256 tiles. Also highlighted.
- **What to count**: Materials, Liquids or Gases. This picks which resources the list shows.
- **Which resource**: pick it in the list that opens to the left of the panel.
- **Threshold**: in kg (shown in tonnes from 5,000 kg), with an **Above / Below** switch. By default the sensor sends a green signal while there is less of the resource than the threshold.

The building status shows the resource and its amount, the count mode and the sensor's signal.

### Good to know

- **Items, storage and tiles.** In Range and Room modes the sensor counts loose items on the floor, items in storage (including storage tiles), bottles and canisters. It also counts liquids and gases right in the tiles.
- **On Map counts materials only.** Across the whole map the game tracks liquids and gases only in bottles and storage, not in tiles. With a liquid or gas chosen, On Map shows **"no data"** and keeps the signal red, so a factory never switches on by a false zero. When you switch back to Range or Room, the sensor ticks Liquids or Gases again.
- **Rooms over 256 tiles** give no data: the status reads "tiles >256 (no data)".
- **No resource chosen**: the signal is red.
- **Copy Settings** works between sensors.

## Compatibility

- Works in the base game and with **Spaced Out!**, **The Frosty Planet Pack**, **The Bionic Booster Pack**, **The Prehistoric Planet Pack** and **The Aquatic Planet Pack**.
- Does not conflict with the *Resource Sensor* mod by Rubacava: they are different buildings.
- Built to survive game updates: the mod uses the game's own panels and entry points. If an update breaks one of its patches, only that patch is skipped (with a warning in the log), and the rest of the mod keeps working.

## Languages

English and Russian. The Russian text lives in `translations/ru.po`; other languages can be added the same way.

## Credits

Inspired by [*Resource Sensor*](https://steamcommunity.com/sharedfiles/filedetails/?id=2911545239) by Rubacava. The code and the look are written from scratch.

## Building from source

You need the .NET SDK ([dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)). Close the game before building.

1. Once per computer, create `GamePath.props` in the folder that holds the mod repositories (one level above this one) with the path to the game, the folder with `OxygenNotIncluded.exe`:

   ```xml
   <Project>
     <PropertyGroup>
       <GameDir>C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded</GameDir>
     </PropertyGroup>
   </Project>
   ```

   This file is not stored in git.

2. Build in the mod folder with `dotnet build` (or Ctrl+Shift+B in VS Code). The finished mod is copied to `Documents\Klei\OxygenNotIncluded\mods\Local\ResourceSensor`.

3. Start the game, enable **Resource Count Sensor** in Mods and restart.

## License

MIT, see [LICENSE](LICENSE).
