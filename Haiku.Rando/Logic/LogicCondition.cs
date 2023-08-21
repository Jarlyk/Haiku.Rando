using System;
using System.Collections.Generic;
using System.Text;

namespace Haiku.Rando.Logic
{
    public sealed class LogicCondition
    {
        public LogicCondition(LogicSymbol symbol, int count = 1)
        {
            Symbol = symbol;
            Count = count;
        }

        public LogicSymbol Symbol { get; }

        public int Count { get; }

        public override string ToString()
        {
            if (Count == 1) return Symbol.ToString();
            return $"{Count}#{Symbol}";
        }
    }
}
