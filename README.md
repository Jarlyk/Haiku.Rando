# Introduction
This is a randomizer mod for the game Haiku, the Robot.

## Installation
This requires that you already have BepInEx and the Haiku.CoreModdingApi installed.  To install, simply place the Haiku.Rando.dll and Newtonsoft.Json.dll into your BepInEx/plugins folder.  In order to play the rando, you'll also want Debug Mod installed (to allow you to escape with map warp if you get trapped) and Configuration Editor to access the configuration.

## Usage
To configure the randomization options, press F1 (with Configuration Editor installed).  Alternately, you can run Haiku once and then edit the config file manually under BepInEx/config/haiku.rando.cfg.

This mod supports both item rando and room rando.  The room rando is lightly tested and
therefore more likely to have problems, so if you run into any issues, please let us know.

## Configuration options

- **Randomization Level**: selects whether only item pickups are randomized, or also room transitions.
  There are four levels:
  - Disable Randomization (randomizes nothing)
  - Randomize pickups only
  - Randomize pickups and room transitions
  - Randomize pickups and door transitions - the following doors are included:
    - Steam Town engine
    - Echo
    - Reaper
    - Pantry (in Bunker)
    - Atom (in First Tree)
    - Elder Snailbot
    - Near Tire Mother (in Abandoned Wastes)
    - Reactor Core (in Old Arcadia besides Elegy)
- **Seed**: the randomization seed. If left blank, the game will generate a random seed.
- **Random Start Location**: if enabled, starts the game at a randomly-chosen repair station instead of the opening cutscene wake location.
- **Train Lover Mode**: if enabled, starts the game in the train with one station unlocked.
  If Random Start Location is on, that station is chosen randomly; otherwise it is always
  Abandoned Wastes.
- **Starting Items**: grants Wrench, Whistle, all Map Disruptors, or
  some Spare Parts (exact amount randomized) at the start of the game.
- **Pool**: selects which locations and respective items will be randomized.
- **Skips**: allows certain categories of skips to be considered for logic purposes:
    - BLJs
    - Enemy Pogos
    - Bomb Jumps
    - Dark Rooms: considers all dark rooms traversable without Bulblet, except for the elevator to Sunken Wastes
    - Hazard Rooms: considers Surface and some parts of Incinerator traversable without their respective sealants
    - Skill Chips: allows Auto Modifier and Self-Detonation to serve as replacements for Ball and Bomb respectively
    - Double Jump Chains: movement that takes advantage of Blink and Grapple refreshing
    the double jump

### QoL Options

These options are mostly for convenience, and don't affect the randomization at all:

- **Show Recent Pickups**: displays a list of the last few picked up items near
  the top-right corner of the screen.
- **Fast Money**: causes all scrap shrines to break fully in a single hit.
- **Synced Money**: makes scrap drop randomization from shrines and enemies
  consistent based on the seed.
- **Pre-Broken Doors**: opens breakable doors automatically upon entering the
  room; required for some transitions in room rando

### Multiworld

This randomizer supports multiworld games, where items are shuffled between several different players'
worlds. It uses the same protocol as [Hollow Knight's multiworld][hkmw], which enables it to work
with the regular multiworld server for that game as well as to play mixed Haiku/HK multiworld
randomizers.

To play a multiworld game in Haiku:

1. Set the server address, your nickname and a room name in the configuration as appropriate.
2. Enable randomization and select your settings as with a single-world game.
2. Press the Ready button in the config panel; if successful, a message will appear on screen indicating which players are in the same room.
3. Once this messsage appears, one player should press "Start MW" to generate the multiworld seed
   for everyone. (this can be done from Hollow Knight as well as Haiku)
4. When the multiworld is successfully generated, you will see a message indicating you are ready to
   join, along with a hash that you can compare with other players to verify they have the same seed.
5. At this point, open a new save file to join you to the multiworld.
6. You should save your game as soon as possible so that you can rejoin later if your game crashes
   or otherwise fails to save.

If you must leave the game early, the "Eject" button will return all items in your world that
belong to other players, to their respective owners.

If you need to change your server, nickname or room name during the setup, press Disconnect, adjust 
your settings, then press Ready again.

[hkmw]: https://github.com/Shadudev/HollowKnight.MultiWorld/blob/master/MultiWorldMod/README.md

## Building
This Git repo includes the Haiku API dependency, but does not include the Unity or Haiku Assemblies.  Prior to building, you'll need to copy the necessary files from the Managed folder in your Haiku installation to the lib/Game folder.  This particular mod also relies on the publicizer, so you will then need to copy the publicized Assembly-CSharp.dll into the lib/Game folder as well.

## Acknowledgements
Thanks to PimpasPimpinela for adding Lore, Map Marker and skip support, as well as improving logic support and fixing various bugs

Thanks to everyone who contributed to logic file development, including: Tomygood, ashley, Allison8Bit

Thanks to Schy for helping to get Haiku modding rolling; we miss you Schy

## Contact
You can reach us via Github or find us on the Haiku Discord.

## License
All mods contained herein are released under the standard MIT license, which is a permissive license that allows for free use.  The text of this is included in the LICENSE file in this release.
