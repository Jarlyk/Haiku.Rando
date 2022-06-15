using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Logic
{
    public sealed class LogicEvaluator
    {
        public ICheckRandoContext Context { get; }

        public bool CanTraverse(IRandoEdge edge)
        {
            //TODO
            return true;
        }
    }
}
