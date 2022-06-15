using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;
using UnityEngine;

namespace Haiku.Rando.Checks
{
    public interface ICheckHandler
    {
        void RemoveCheck(RandoCheck check);

        void AddCheck(Vector2 location, RandoCheck check);
    }
}
