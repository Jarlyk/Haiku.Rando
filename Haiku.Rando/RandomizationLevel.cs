using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Haiku.Rando
{
    public enum RandomizationLevel
    {
        [Description("Disable Randomization")]
        None,

        [Description("Randomize pickups only")]
        Pickups,

        [Description("Randomize items and area transitions")]
        Areas,

        [Description("Randomize items and room transitions")]
        Rooms
    }
}
