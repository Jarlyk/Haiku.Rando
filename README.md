
[//]: # ( Haiku Rando 0.3 )

# Introduction
This is a randomizer mod for the game Haiku, the Robot.  It is still in development, but is ready for Beta release testing.

### Installation
This requires that you already have BepInEx and the Haiku.CoreModdingApi installed.  To install, simply place the Haiku.Rando.dll and Newtonsoft.Json.dll into your BepInEx/plugins folder.  In order to play the rando, you'll also want Debug Mod installed (to allow you to escape with map warp if you get trapped) and Configuration Editor to access the configuration.

### Usage
To configure the randomization options, press F1 (with Configuration Editor installed).  Alternately, you can run Haiku once and then edit the config file manually under BepInEx/config/haiku.rando.cfg.

To use a set seed, enter any text in the Seed config entry.  If you leave it blank, the game will generate a random seed.

This release supports both item rando and room rando.  The room rando is more likely to have problems, so if you run into any issues, please let me know.

### Building
This Git repo includes the Haiku API dependency, but does not include the Unity or Haiku Assemblies.  Prior to building, you'll need to copy the necessary files from the Managed folder in your Haiku installation to the lib/Game folder.  This particular mod also relies on the publicizer, so you will then need to copy the publicized Assembly-CSharp.dll into the lib/Game folder as well.

### Acknowledgements
Thanks to PimpasPimpinela for adding Lore support, as well as improving logic support and fixing various bugs

Thanks to everyone who contributed to logic file development, including: Tomygood, ashley, Allison8Bit

Thanks to Schy for helping to get Haiku modding rolling; we miss you Schy

### Contact
You can reach me via Github or find me on the Haiku Discord.

### License
All mods contained herein are released under the standard MIT license, which is a permissive license that allows for free use.  The text of this is included in the LICENSE file in this release.
