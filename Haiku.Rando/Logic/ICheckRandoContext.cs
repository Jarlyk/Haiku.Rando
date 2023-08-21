using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Logic
{
    public interface ICheckRandoContext
    {
        int GetCount(LogicSymbol sym);
    }
}
