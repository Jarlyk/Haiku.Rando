using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;
using UnityEngine;

namespace Haiku.Rando.Checks
{
    public sealed class AbilityCheckHandler : ICheckHandler
    {
        public void RemoveCheck(RandoCheck check)
        {
            //TODO: Find ability object
            //TODO: Remove object
        }

        public void AddCheck(Vector2 location, RandoCheck check)
        {
            //TODO: Construct new ability object based on check specifier
        }
    }
}
