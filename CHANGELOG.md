# 2.4.2 (31 August 2024)

Bug fixes:

- Enabling Splunk no longer causes multiworld generation to fail.
- The Space Disturbance location is now reachable with just the right
  transition, Blink and Amplifying Transputer

# 2.4.1 (9 February 2024)

Bug fixes:

- Starting items (including scrap) are now granted only once.
- The Include Old Arcadia and Include Lost Archives settings are now preserved properly in the save file.
  (Previously, if these options were enabled, they would
  no longer be on after reloading the file, causing the entire seed to change.)
- The train location no longer resets to the starting
  station when reloading a Train Lover Mode save.

# 2.4 (28 October 2023)

New features:

- Door rando: randomizes all 8 minor door transitions.
- The reward at the end of Splunk's quest can be randomized.
- Options to exclude Old Arcadia and Lost Archives from randomization.

Bug fixes:

- Blink-only entrance to the Factory shrine room is now in logic.
- Logic now allows for crossing the first Splunk room from right to left with enemy pogos.
- Fixed a false positive on entry to the Car Battery room.
- Transition rando (room or door)'s permutations are no longer restricted to pairwise swaps.

Known issues:

- Full room rando (not door) may sometimes generate uncompletable seeds. Fixes are welcome.

# 2.3 (5 October 2023)

New features:

- Multiworld support, compatible with [Hollow Knight's][hkmw]. All randomization settings in either
  game, as well as connections for HK, are supported.
- The recent items list now shows the area each item was obtained in, or the originating player for
  items sent by other players in a multiworld game.

[hkmw]: https://github.com/Shadudev/HollowKnight.MultiWorld/blob/master/MultiWorldMod/README.md

# 2.2.4 (27 September 2023)

Bug fixes:

- An issue that caused seed generation to frequently fail (manifested as a lockup on 
  entering the game) under certain combinations of pools and skips has been resolved.
- The Creator trio cutscene is no longer interactable if the fight is entered in Old
  Arcadia before it is done in the Lost Archives. This can happen when lever rando is
  enabled.

# 2.2.3 (9 September 2023)

Bug fixes:

- The piano check now works even if the Cassette Tape has been obtained
- The train station selector UI now works properly when Clock is randomized but not a starting item
- Slate's Rusted Key only requires a single Creator in logic instead of all three
- Fixed a false positive in the Snailbot Burrow lever room

# 2.2.2 (30 August 2023)

Enhancements:

- Seeds take much less time to generate than in previous versions.

Bug fixes:

- The capsule fragments in Sonnet's shop are no longer always replaced
  with the same item.
- Mischievous's power cell check only spawns after defeating the boss.
- Fixed false logic negatives in the double-lever Factory room.
- When not starting with Wrench, extraneous Wrenches no longer appear in the item pool. 

# 2.2.1 (13 August 2023)

Bug fixes:

- Fixed logic false positives in Incinerator and Sunken Wastes

# 2.2 (6 August 2023)

New features:

- Old Arcadia's rooms and items are included in the randomization. This adds three levers,
  a lore tablet, and several scrap nodes to the pool.
- Levers have been added as a new randomizable pool.
  Lever checks are obtained by striking the lever, and collecting a lever *item* opens
  the respective gate.
- There is an option to start the game with a small, randomized amount of spare parts.

Bug fixes:

- The game will no longer fail to start on systems whose locale uses symbols other than `.`
  (dot) as a decimal separator.
- Fixed false positives in the Bulb Hive room.

Known issues:

- If randomized, The Old Arcadia elevator lever leaves behind a second, inactive lever
  when hit. This is purely a cosmetic issue; it otherwise functions as expected.

# 2.1 (23 July 2023)

New features:

- A new Double Jump Chain skip category, with logic for movement
  that uses Blink or Grapple to refresh the double jump in mid-air.

Bug fixes:

- Assorted base logic fixes.
- The Newtonsoft.JSON DLL is now included in the release zip file;
  some previous versions had not, and did not work correctly when
  installed from Scarab as a result.

# 2.0 (9 June 2023)

New features:

- Works with the new Obscure Information DLC (patch 1.5.1.2). Older versions of the game are no longer supported.
  Rooms and items in Old Arcadia are not randomized.
- Scrap nodes have been added as a new randomizable pool.
  Adjacent nodes are consolidated into a single check with the combined value of all of the nodes.
- The Clock repair is randomizable.
- The new Train Lover Mode starts you in the train with the clock already fixed and one random station unlocked.
- With Skill Chips on, Bulb Relation will serve as a replacement for Bulblet, with the exception of levers that
  specifically require Bulblet.

Bug fixes:

- Going through the Magnet boss room left to right is no longer in logic.
- Fixed false positives in the Sunken Wastes elevator room.
- "Nothing" items are much less likely to be concentrated near the starting area.

# 1.1.2 (13 May 2023)

Bug fixes:
- Lore items now appear in the recent items list like everything else
- Picking up lore, map marker, or Nothing checks will no longer make a certain Rusted Key item disappear

# 1.1.1 (4 May 2023)

Bug fixes:
- The limited subset of shop item in Abandoned Wastes (which in vanilla are duplicates of four train shop items)
  is no longer treated as independent locations; instead these locations always offer the same items as their counterparts in the train.
  This prevents items from being irretrieavably lost once the Clock is repaired.
- Logic no longer allows going up from Sunken Wastes through the elevator without Light under any circumstances

# 1.1 (29 April 2023)

New features:

- A togglable list of the 5 most recently-obtained items (excluding lore tablets).
- Logic for several skip categories, each of which can be enabled individually:
    - BLJs
    - Enemy Pogos
    - Bomb Jumps
    - Dark Rooms (considers all dark rooms traversable without Bulblet, except for the elevator to Sunken Wastes)
    - Hazard Rooms (considers Surface and some parts of Incinerator traversable without their respective sealants)
    - Skill Chips (allows Auto Modifier and Self-Detonation to serve as replacements for Ball and Bomb respectively)

Bug fixes:
- A large number of false negatives and false positives in logic across all areas have been corrected