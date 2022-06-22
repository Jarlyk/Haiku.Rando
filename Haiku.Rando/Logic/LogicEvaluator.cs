using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Checks;
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

        public bool MatchesState(RandoCheck check, string stateName)
        {
            if (stateName == LogicStateNames.Bomb) return IsAbility(check, AbilityId.Bomb);
            if (stateName == LogicStateNames.Dash) return false;
            if (stateName == LogicStateNames.DoubleJump) return IsAbility(check, AbilityId.DoubleJump);
            if (stateName == LogicStateNames.Grapple) return IsAbility(check, AbilityId.Grapple);
            if (stateName == LogicStateNames.Heal) return check.Type == CheckType.Wrench;
            if (stateName == LogicStateNames.Ball) return IsAbility(check, AbilityId.Ball);
            if (stateName == LogicStateNames.Blink) return IsAbility(check, AbilityId.Blink);
            if (stateName == LogicStateNames.Magnet) return IsAbility(check, AbilityId.Magnet);
            if (stateName == LogicStateNames.FireRes) return check.Type == CheckType.FireRes;
            if (stateName == LogicStateNames.WaterRes) return check.Type == CheckType.WaterRes;
            if (stateName == LogicStateNames.Light) return check.Type == CheckType.Bulblet;
            if (stateName == LogicStateNames.Chip) return check.Type == CheckType.Chip;
            if (stateName == LogicStateNames.ChipSlot) return check.Type == CheckType.ChipSlot;
            if (stateName == LogicStateNames.PowerCell) return check.Type == CheckType.PowerCell;

            //TODO: Specific chips?
            return false;
        }

        private static bool IsAbility(RandoCheck check, AbilityId id)
        {
            return check.Type == CheckType.Ability && check.CheckId == (int)id;
        }
    }
}
