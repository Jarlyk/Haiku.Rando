using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Haiku.Rando.Checks;
using Haiku.Rando.Topology;
using Haiku.Rando.Util;
using UnityEngine;

namespace Haiku.Rando.Logic
{
    public sealed class LogicEvaluator
    {
        public LogicEvaluator(IReadOnlyList<LogicLayer> layers)
        {
            Layers = layers;
        }

        public IReadOnlyList<LogicLayer> Layers { get; }

        public ICheckRandoContext Context { get; set; }

        public bool CanTraverse(GraphEdge edge)
        {
            var sets = GetAllLogic(edge);

            if (sets.Count == 0) return true;

            return sets.Any(IsLogicSatisfied);
        }

        public IReadOnlyList<LogicSet> GetAllLogic(GraphEdge edge)
        {
            var result = new List<LogicSet>();
            foreach (var layer in Layers)
            {
                if (layer.LogicByEdge.TryGetValue(edge, out var set))
                {
                    result.AddRange(set);
                }
            }

            return result;
        }

        public IReadOnlyList<LogicCondition> GetMissingLogic(GraphEdge edge, Xoroshiro128Plus random)
        {
            var sets = GetAllLogic(edge);
            if (sets.Count == 0) return Array.Empty<LogicCondition>();

            //There may be multiple possible logical options
            //For the purposes of reporting missing logic, we prioritize sets that are missing less
            var missingPerSet = sets.Select(GetMissingLogic).ToList();
            if (missingPerSet.Any(m => m.Count == 0)) return Array.Empty<LogicCondition>();

            var byCount = missingPerSet.GroupBy(m => m.Sum(s => s.Count)).OrderBy(g => g.Key).ToList();
            var group = byCount[0].ToList();
            if (group.Count == 1) return group[0];
            var pick = random.NextRange(0, group.Count);
            return group[pick];
        }

        /// <summary>
        /// Returns true if all logic related to the edge returns a constant 'false'
        /// This is used to represent inaccessible paths
        /// </summary>
        /// <param name="edge">Edge for which to check the true</param>
        /// <returns>True iff all logic is constant 'false'</returns>
        public bool IsFalse(GraphEdge edge)
        {
            var sets = GetAllLogic(edge);
            return sets.Count > 0 && sets.All(s => s.Conditions.Count == 1 &&
                                                   s.Conditions[0].StateName
                                                    .Equals("false", StringComparison.InvariantCultureIgnoreCase));
        }

        private IReadOnlyList<LogicCondition> GetMissingLogic(LogicSet set)
        {
            var result = new List<LogicCondition>();
            foreach (var condition in set.Conditions)
            {
                var diff = condition.Count - Context.GetCount(condition.StateName);
                if (diff > 0)
                {
                    result.Add(new LogicCondition(condition.StateName, diff));
                }
            }

            return result;
        }

        public bool IsLogicSatisfied(LogicSet set)
        {
            return set.Conditions.All(IsConditionSatsified);
        }

        public bool IsConditionSatsified(LogicCondition condition)
        {
            if (condition.StateName.Equals("false", StringComparison.InvariantCultureIgnoreCase)) return false;
            if (condition.StateName.Equals("true", StringComparison.InvariantCultureIgnoreCase)) return true;

            var result = Context.GetCount(condition.StateName) >= condition.Count;
            //Debug.Log($"Checking condition {condition} => {result}");
            return result;
        }

        public static bool MatchesState(int edgeSceneId, RandoCheck check, string stateName)
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

            var bracketIndex = stateName.IndexOf('[');
            if (bracketIndex == -1)
            {
                //If no index specifier, only match if in the same room
                return MatchesType(stateName, check.Type) && check.SceneId == edgeSceneId;
            }

            var baseName = stateName.Substring(0, bracketIndex);
            if (!MatchesType(baseName, check.Type)) return false;

            var endIndex = stateName.IndexOf(']');
            if (endIndex > bracketIndex)
            {
                if (int.TryParse(stateName.Substring(bracketIndex + 1, endIndex - bracketIndex - 1), out int checkId))
                    return check.CheckId == checkId;
            }

            return false;
        }

        private static bool IsAbility(RandoCheck check, AbilityId id)
        {
            return check.Type == CheckType.Ability && check.CheckId == (int)id;
        }

        private static bool MatchesType(string stateName, CheckType type) => type switch
        {
            CheckType.Chip => stateName == "Chip",
            CheckType.ChipSlot => stateName == "Slot",
            CheckType.PowerCell => stateName == "PowerCell",
            CheckType.Item => stateName == "Item",
            CheckType.MapDisruptor => stateName == "Disruptor",
            CheckType.Lever => stateName == "Lever",
            CheckType.Coolant => stateName == "Coolant",
            CheckType.TrainStation => stateName == "TrainStation",
            CheckType.Clock => stateName == "Clock",
            _ => false
        };

        public static string GetStateName(RandoCheck check)
        {
            switch (check.Type)
            {
                case CheckType.Wrench:
                    return LogicStateNames.Heal;
                case CheckType.Bulblet:
                    return LogicStateNames.Light;
                case CheckType.Ability:
                    return GetAbilityStateName((AbilityId)check.CheckId);
                case CheckType.Item:
                    return $"Item[{check.CheckId}]";
                case CheckType.Chip:
                    return $"Chip[{check.CheckId}]";
                case CheckType.ChipSlot:
                    return $"Slot[{check.CheckId}]";
                case CheckType.MapDisruptor:
                    return $"Disruptor[{check.CheckId}]";
                case CheckType.Lore:
                    return $"Lore[{check.CheckId}]";
                case CheckType.Lever:
                    return $"Lever[{check.CheckId}]";
                case CheckType.PartsMonument:
                    return null;
                case CheckType.PowerCell:
                    return $"PowerCell[{check.CheckId}]";
                case CheckType.Coolant:
                    return $"Coolant[{check.SaveId}]";
                case CheckType.FireRes:
                    return LogicStateNames.FireRes;
                case CheckType.WaterRes:
                    return LogicStateNames.WaterRes;
                case CheckType.TrainStation:
                    return $"TrainStation[{check.CheckId}]";
                case CheckType.Clock:
                    return "Clock";
                case CheckType.MapMarker:
                    return $"Marker[{check.CheckId}]";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static string GetAbilityStateName(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Magnet:
                    return LogicStateNames.Magnet;
                case AbilityId.Ball:
                    return LogicStateNames.Ball;
                case AbilityId.Bomb:
                    return LogicStateNames.Bomb;
                case AbilityId.Blink:
                    return LogicStateNames.Blink;
                case AbilityId.DoubleJump:
                    return LogicStateNames.DoubleJump;
                case AbilityId.Grapple:
                    return LogicStateNames.Grapple;
                case AbilityId.None:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
