
[//]: # ( Haiku Rando )

# Introduction
This is the beginnings of a randomizer for the game Haiku, the Robot.  It is still in very early development and not yet functional.

### Installation
This requires that you already have BepInEx and the Haiku.CoreModdingApi installed.  To install, simply place the Haiku.Rando.dll into your BepInEx/plugins folder.

### Usage
Currently this is just a proof of concept for automatically scanning topology to find checks.  To trigger the scan procedure, start a new game from the start, then press Shift+Y.

### Building
This Git repo includes the Haiku API dependency, but does not include the Unity or Haiku Assemblies.  Prior to building, you'll need to copy the necessary files from the Managed folder in your Haiku installation to the lib/Game folder.  This particular mod also relies on the publicizer, so you will then need to copy the publicized Assembly-CSharp.dll into the lib/Game folder as well.

### Acknowledgements
Thanks to everyone who contributed to logic file development: Tomygood.
Thanks to Schy for helping to get Haiku modding rolling; we miss you Schy

### Contact
You can reach me via Github or find me on the Haiku Discord.

### License
All mods contained herein are released under the standard MIT license, which is a permissive license that allows for free use.  The text of this is included in the LICENSE file in this release.