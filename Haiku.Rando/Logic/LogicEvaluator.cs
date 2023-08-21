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
            return sets.Count > 0 && sets.All(
                s => s.Conditions.Count == 1 &&
                s.Conditions[0].Symbol == LogicSymbol.False);
        }

        private IReadOnlyList<LogicCondition> GetMissingLogic(LogicSet set)
        {
            var result = new List<LogicCondition>();
            foreach (var condition in set.Conditions)
            {
                var diff = condition.Count - Context.GetCount(condition.Symbol);
                if (diff > 0)
                {
                    result.Add(new LogicCondition(condition.Symbol, diff));
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
            if (condition.Symbol == LogicSymbol.False)
            {
                return false;
            }
            if (condition.Count == 0)
            {
                return true;
            }
            return Context.GetCount(condition.Symbol) >= condition.Count;
        }

        public static LogicSymbol SymbolForCheck(RandoCheck check) => check.Type switch
        {
            CheckType.Wrench => LogicSymbol.Wrench,
            CheckType.Bulblet => LogicSymbol.Light,
            CheckType.Ability => (AbilityId)check.CheckId switch
            {
                AbilityId.Magnet => LogicSymbol.Magnet,
                AbilityId.Ball => LogicSymbol.Ball,
                AbilityId.Bomb => LogicSymbol.Bomb,
                AbilityId.Blink => LogicSymbol.Blink,
                AbilityId.DoubleJump => LogicSymbol.DoubleJump,
                AbilityId.Grapple => LogicSymbol.Grapple,
                _ => LogicSymbol.Nil
            },
            CheckType.Item => (ItemId)check.CheckId switch
            {
                ItemId.RustedKey => LogicSymbol.RustedKey,
                ItemId.ElectricKey => LogicSymbol.ElectricKey,
                ItemId.GreenSkull => LogicSymbol.GreenSkull,
                ItemId.RedSkull => LogicSymbol.RedSkull,
                _ => LogicSymbol.Nil
            },
            CheckType.Chip => check.CheckId switch
            {
                1 => LogicSymbol.GyroAccelerator,
                3 => LogicSymbol.PowerProcessor,
                6 => LogicSymbol.AutoModifier,
                11 => LogicSymbol.BulbRelation,
                16 => LogicSymbol.SelfDetonation,
                20 => LogicSymbol.AmplifyingTransputer,
                25 => LogicSymbol.HeatDrive,
                _ => LogicSymbol.Nil
            },
            CheckType.ChipSlot => ChipSlotSymbol(check.CheckId),
            CheckType.Lever => LeverSymbol(check.CheckId),
            CheckType.PowerCell => LogicSymbol.PowerCell,
            CheckType.Coolant => LogicSymbol.Coolant,
            CheckType.FireRes => LogicSymbol.FireRes,
            CheckType.WaterRes => LogicSymbol.WaterRes,
            CheckType.TrainStation => 
                check.CheckId >= 0 && check.CheckId < 8 ? (LogicSymbol)((int)LogicSymbol.AbandonedWastesTrainStation + check.CheckId) : LogicSymbol.Nil,
            CheckType.Clock => LogicSymbol.Clock,
            _ => LogicSymbol.Nil
        };

        internal static LogicSymbol ChipSlotSymbol(int slotId) =>
            ChipSlotColorSymbol(GameManager.instance.chipSlot[slotId].chipSlotColor);
        
        internal static LogicSymbol ChipSlotColorSymbol(string color) =>
            color switch
            {
                "red" => LogicSymbol.RedChipSlot,
                "green" => LogicSymbol.GreenChipSlot,
                "blue" => LogicSymbol.BlueChipSlot,
                _ => LogicSymbol.Nil
            };
        
        private static LogicSymbol LeverSymbol(int leverId) => leverId switch
        {
            4 => LogicSymbol.Lever4,
            6 => LogicSymbol.Lever6,
            7 => LogicSymbol.Lever7,
            9 => LogicSymbol.Lever9,
            12 => LogicSymbol.Lever12,
            13 => LogicSymbol.Lever13,
            15 => LogicSymbol.Lever15,
            17 => LogicSymbol.Lever17,
            20 => LogicSymbol.Lever20,
            21 => LogicSymbol.Lever21,
            22 => LogicSymbol.Lever22,
            25 => LogicSymbol.Lever25,
            26 => LogicSymbol.Lever26,
            27 => LogicSymbol.Lever27,
            28 => LogicSymbol.Lever28,
            30 => LogicSymbol.Lever30,
            37 => LogicSymbol.Lever37,
            38 => LogicSymbol.Lever38,
            42 => LogicSymbol.Lever42,
            44 => LogicSymbol.Lever44,
            45 => LogicSymbol.Lever45,
            46 => LogicSymbol.Lever46,
            47 => LogicSymbol.Lever47,
            48 => LogicSymbol.Lever48,
            49 => LogicSymbol.Lever49,
            51 => LogicSymbol.Lever51,
            53 => LogicSymbol.Lever53,
            54 => LogicSymbol.Lever54,
            55 => LogicSymbol.Lever55,
            56 => LogicSymbol.Lever56,
            71 => LogicSymbol.Lever71,
            72 => LogicSymbol.Lever72,
            75 => LogicSymbol.Lever75,
            _ => LogicSymbol.Nil
        };
    }
}
