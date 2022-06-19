using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Logic
{
    public sealed class LogicEvaluator
    {
        public ICheckRandoContext Context { get; set; }

        public bool CanTraverse(GraphEdge edge)
        {
            //TODO
            return true;
        }

        public IReadOnlyList<LogicCondition> GetMissingLogic(GraphEdge edge)
        {
            //TODO
            return new LogicCondition[] { };
        }
    }
}
