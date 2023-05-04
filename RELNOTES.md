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