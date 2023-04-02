using System;
using System.Collections.Generic;
using System.Text;

namespace Haiku.Rando.Logic
{
    public sealed class LogicCondition
    {
        public LogicCondition(string stateName, int count = 1)
        {
            StateName = stateName;
            Count = count;
        }

        public string StateName { get; }

        public int Count { get; }

        public override string ToString()
        {
            if (Count == 1) return StateName;
            return $"{Count}#{StateName}";
        }
    }
}
