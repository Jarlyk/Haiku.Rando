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

        //[Description("Randomize items and area transitions")]
        //Areas,

        [Description("Randomize pickups and room transitions")]
        Rooms,

        [Description("Randomize pickups and door transitions")]
        Doors
    }
}
