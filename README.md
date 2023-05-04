# Introduction
This is a randomizer mod for the game Haiku, the Robot.

## Installation
This requires that you already have BepInEx and the Haiku.CoreModdingApi installed.  To install, simply place the Haiku.Rando.dll and Newtonsoft.Json.dll into your BepInEx/plugins folder.  In order to play the rando, you'll also want Debug Mod installed (to allow you to escape with map warp if you get trapped) and Configuration Editor to access the configuration.

## Usage
To configure the randomization options, press F1 (with Configuration Editor installed).  Alternately, you can run Haiku once and then edit the config file manually under BepInEx/config/haiku.rando.cfg.

This release supports both item rando and room rando.  The room rando is more likely to have problems, so if you run into any issues, please let me know.

## Configuration options

- **Randomization Level**: selects whether only item pickups are randomized (the default), or both room transitions and item
  pickups, or neither.
- **Seed**: the randomization seed. If left blank, the game will generate a random seed.
- **Random Start Location**: if enabled, starts the game at a randomly-chosen repair station instead of the opening cutscene wake location.
- **Starting Items**: grants Wrench, Whistle, or all Map Disruptors at the start of the game.
- **Pool**: selects which locations and respective items will be randomized.
- **Skips**: allows certain categories of skips to be considered for logic purposes:
    - BLJs
    - Enemy Pogos
    - Bomb Jumps
    - Dark Rooms (considers all dark rooms traversable without Bulblet, except for the elevator to Sunken Wastes)
    - Hazard Rooms (considers Surface and some parts of Incinerator traversable without their respective sealants)
    - Skill Chips (allows Auto Modifier and Self-Detonation to serve as replacements for Ball and Bomb respectively)

### QoL Options

These options are mostly for convenience, and don't affect the randomization at all:

- **Show Recent Pickups**: displays a list of the last few picked up items near
  the top-right corner of the screen.
- **Fast Money**: causes all scrap shrines to break fully in a single hit.
- **Synced Money**: makes scrap drop randomization from shrines and enemies
  consistent based on the seed.
- **Pre-Broken Doors**: opens breakable doors automatically upon entering the
  room; required for some transitions in room rando

## Building
This Git repo includes the Haiku API dependency, but does not include the Unity or Haiku Assemblies.  Prior to building, you'll need to copy the necessary files from the Managed folder in your Haiku installation to the lib/Game folder.  This particular mod also relies on the publicizer, so you will then need to copy the publicized Assembly-CSharp.dll into the lib/Game folder as well.

## Acknowledgements
Thanks to PimpasPimpinela for adding Lore, Map Marker and skip support, as well as improving logic support and fixing various bugs

Thanks to everyone who contributed to logic file development, including: Tomygood, ashley, Allison8Bit

Thanks to Schy for helping to get Haiku modding rolling; we miss you Schy

## Contact
You can reach me via Github or find me on the Haiku Discord.

## License
All mods contained herein are released under the standard MIT license, which is a permissive license that allows for free use.  The text of this is included in the LICENSE file in this release.
