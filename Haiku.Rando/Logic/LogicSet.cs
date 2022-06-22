using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Logic
{
    public sealed class LogicSet
    {
        public LogicSet(IReadOnlyList<LogicCondition> conditions)
        {
            Conditions = conditions;
        }

        public IReadOnlyList<LogicCondition> Conditions { get; }
    }
}
