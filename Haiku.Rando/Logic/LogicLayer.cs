using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Haiku.Rando.Checks;
using Haiku.Rando.Topology;
using MonoMod.Utils;
using UnityEngine;

namespace Haiku.Rando.Logic
{
    public sealed class LogicLayer
    {
        public LogicLayer(IReadOnlyDictionary<int, SceneLogic> logicByScene)
        {
            LogicByScene = logicByScene;

            var logicByEdge = new Dictionary<GraphEdge, IReadOnlyList<LogicSet>>();
            foreach (var sceneLogic in LogicByScene.Values)
            {
                foreach (var logicSet in sceneLogic.LogicByEdge)
                {
                    logicByEdge.Add(logicSet.Key, logicSet.Value);
                }
            }

            LogicByEdge = logicByEdge;
        }

        public IReadOnlyDictionary<int, SceneLogic> LogicByScene { get; }

        public IReadOnlyDictionary<GraphEdge, IReadOnlyList<LogicSet>> LogicByEdge { get; }

        public static LogicLayer Deserialize(RandoTopology topology, Func<Skip, bool> enabledSkips, StreamReader reader)
        {
            var tokens = TokenizeLogic(reader);
            var macros = new Dictionary<string, List<Token>>()
            {
                {"PowerProcessor", singleName(LogicStateNames.PowerProcessor)},
                {"HeatDrive", singleName(LogicStateNames.HeatDrive)},
                {"AmplifyingTransputer", singleName(LogicStateNames.AmplifyingTransputer)},
                {"GyroAccelerator", singleName(LogicStateNames.GyroAccelerator)},
                {"LIGHT", singleName(LogicStateNames.Light)},
                {"Light", singleName(enabledSkips(Skip.DarkRooms) ? "true" : LogicStateNames.Light)},
                {"BLJ", singleName(enabledSkips(Skip.BLJ) ? "true" : "false")},
                {"EnemyPogos", singleName(enabledSkips(Skip.EnemyPogos) ? "true" : "false")},
                {"BombJumps", singleName(enabledSkips(Skip.BombJumps) ? "true" : "false")}
            };
            if (enabledSkips(Skip.SkillChips))
            {
                macros["Ball"] = new()
                {
                    new(TokenType.Name, "Ball", -1),
                    new(TokenType.Name, LogicStateNames.AutoModifier, -1),
                    new(TokenType.Or, "|", -1)
                };
                macros["SelfDetonation"] = singleName(LogicStateNames.SelfDetonation);
            }
            else
            {
                macros["SelfDetonation"] = singleName("false");
            }

            return tokens == null ? null : ParseLogic(topology, macros, tokens.GetEnumerator());
        }

        private static List<Token> singleName(string name) => new() { new(TokenType.Name, name, -1) };

        private enum TokenType
        {
            Name,
            GroupName,
            Int,
            Colon,
            Comma,
            RightArrow,
            BidirArrow,
            LeftBrace,
            RightBrace,
            LeftParen,
            RightParen,
            Or,
            And,
            Hash,
            Terminator
        }

        private record struct Token(TokenType Type, string Content, int LineNumber);

        private static readonly Regex tokenPattern =
            new(@"^\s*(?:([\p{L}\*!][\p{L}\d\[\]\*]*)|(\$\w+)|(\d+)|(:)|(,)|(->)|(<->)|(\{)|(\})|(\()|(\))|(\|)|(\+)|(#))");
        private static readonly Regex commentPattern = new(@"^\s*//");

        private static List<Token> TokenizeLogic(StreamReader reader)
        {
            var lineno = 0;
            var tokens = new List<Token>();
            while (!reader.EndOfStream)
            {
                lineno++;
                var line = reader.ReadLine();
                while (true)
                {
                    var m = tokenPattern.Match(line);
                    if (!m.Success)
                    {
                        if (string.IsNullOrWhiteSpace(line) || commentPattern.IsMatch(line))
                        {
                            break;
                        }
                        Debug.LogError($"Unexpected token at line {lineno}: {line.Trim()}");
                        return null;
                    }
                    line = line.Substring(m.Length);
                    for (var i = TokenType.Name; i < TokenType.Terminator; i++)
                    {
                        var g = m.Groups[(int)i + 1];
                        if (g.Length != 0)
                        {
                            tokens.Add(new(i, g.Value, lineno));
                            break;
                        }
                    }
                }
                if (tokens.Count != 0)
                {
                    var last = tokens[tokens.Count - 1].Type;
                    if (last == TokenType.Name 
                        || last == TokenType.Int
                        || last == TokenType.RightBrace
                        || last == TokenType.RightParen)
                    {
                        tokens.Add(new(TokenType.Terminator, "", lineno));
                    }
                }
            }
            return tokens;
        }

        private static void ExpectTerminator(IEnumerator<Token> input)
        {
            if (input.MoveNext())
            {
                ExpectTokenOfType(input, "end of statement", TokenType.Terminator);
            }
            else
            {
                throw new InvalidOperationException("expected terminator at the end of a statement, got EOF");
            }
        }

        private static bool ExpectTokenOfType(IEnumerator<Token> input, string expectationName, params TokenType[] types)
        {
            if (types.Contains(input.Current.Type))
            {
                return true;
            }
            Debug.LogError($"logic error at line #{input.Current.LineNumber}: expected {expectationName}, got '{input.Current.Content}'");
            if (input.Current.Type != TokenType.Terminator)
            {
                SkipToNextTerminator(input);
            }
            return false;
        }

        private static void SkipToNextTerminator(IEnumerator<Token> input)
        {
            while (input.MoveNext())
            {
                if (input.Current.Type == TokenType.Terminator)
                {
                    return;
                }
            }
        }

        private static LogicLayer ParseLogic(RandoTopology topology, Dictionary<string, List<Token>> macros, IEnumerator<Token> input)
        {
            var logicByScene = new Dictionary<int, Dictionary<GraphEdge, List<LogicSet>>>();
            var groups = new Dictionary<string, List<string>>();
            RoomScene scene = null;
            while (true)
            {
                if (!input.MoveNext())
                {
                    break;
                }
                var det = input.Current;
                if (det.Type == TokenType.Name && det.Content == "Scene")
                {
                    if (!input.MoveNext())
                    {
                        Debug.LogError($"logic error at line #{det.LineNumber}: EOF within Scene declaration");
                        break;
                    }
                    groups.Clear();
                    var num = input.Current;
                    if (num.Type == TokenType.Int && int.TryParse(num.Content, out var sceneId) && topology.Scenes.TryGetValue(sceneId, out scene))
                    {
                        ExpectTerminator(input);
                    }
                    else
                    {
                        Debug.LogError($"logic error at line #{num.LineNumber}: scene '{num.Content}' does not exist");
                        scene = null;
                        if (num.Type != TokenType.Terminator)
                        {
                            SkipToNextTerminator(input);
                        }
                    }
                }
                else if (det.Type == TokenType.GroupName)
                {
                    var groupName = det.Content.Substring(1);
                    if (groups.ContainsKey(groupName))
                    {
                        Debug.LogError($"logic error at line #{det.LineNumber}: duplicate group '{det.Content}'");
                        SkipToNextTerminator(input);
                        continue;
                    }
                    if (ParseGroupDefinition(input) is var def && def != null)
                    {
                        groups[groupName] = def;
                    }
                }
                // TODO: implement NOT operator
                else if (det.Type == TokenType.Name)
                {
                    if (scene == null)
                    {
                        Debug.LogError($"logic error at line #{det.LineNumber}: logic clause outside the scope of a scene");
                        SkipToNextTerminator(input);
                        continue;
                    }
                    var fromNodes = FindNodes(scene, det.Content, groups);
                    if (fromNodes.Count == 0)
                    {
                        Debug.LogError($"logic error at line #{det.LineNumber}: cannot resolve source node '{det.Content}'");
                    }
                    if (!input.MoveNext())
                    {
                        Debug.LogError("logic error: EOF within logic clause");
                        continue;
                    }
                    if (!ExpectTokenOfType(input, "edge symbol", TokenType.RightArrow, TokenType.BidirArrow))
                    {
                        continue;
                    }
                    var twoWay = input.Current.Type == TokenType.BidirArrow;
                    if (!input.MoveNext())
                    {
                        Debug.LogError("logic error: EOF within logic clause");
                        continue;
                    }
                    if (!ExpectTokenOfType(input, "destination node", TokenType.Name))
                    {
                        continue;
                    }
                    var toNodes = FindNodes(scene, input.Current.Content, groups);
                    if (toNodes.Count == 0)
                    {
                        Debug.LogError($"logic error at line #{input.Current.LineNumber}: cannot resolve destination node '{input.Current.Content}'");
                    }
                    if (!input.MoveNext())
                    {
                        Debug.LogError("logic error: EOF within logic clause");
                        continue;
                    }
                    if (!ExpectTokenOfType(input, "colon", TokenType.Colon))
                    {
                        continue;
                    }
                    if (fromNodes.Count == 0 || toNodes.Count == 0)
                    {
                        SkipToNextTerminator(input);
                        continue;
                    }
                    var rpn = ParseLogicExpression(input, macros);
                    if (rpn == null)
                    {
                        SkipToNextTerminator(input);
                        continue;
                    }
                    var logicSets = EvalLogicExpression(rpn, term => ExpandAlias(term, scene));
                    if (logicSets == null)
                    {
                        continue;
                    }
                    if (!logicByScene.TryGetValue(scene.SceneId, out var logicByEdge))
                    {
                        logicByEdge = new();
                        logicByScene[scene.SceneId] = logicByEdge;
                    }
                    foreach (var set in logicSets)
                    {
                        AddLogic(fromNodes, toNodes, scene, logicByEdge, set);
                    }
                    if (twoWay)
                    {
                        foreach (var set in logicSets)
                        {
                            AddLogic(toNodes, fromNodes, scene, logicByEdge, set);
                        }
                    }
                }
                else
                {
                    Debug.LogError($"logic error at line #{det.LineNumber}: expected Scene, term or group name, got '{det.Content}'");
                    SkipToNextTerminator(input);
                }
            }
            //Expose final result as an immutable collection
            return new(logicByScene.ToDictionary(
                p => p.Key,
                p => new SceneLogic(p.Value.ToDictionary(x => x.Key, x => (IReadOnlyList<LogicSet>)x.Value))));
        }

        private static List<string> ParseGroupDefinition(IEnumerator<Token> input)
        {
            if (!input.MoveNext())
            {
                Debug.LogError("logic error: EOF within group declaration");
                return null;
            }
            if (!ExpectTokenOfType(input, "opening brace", TokenType.LeftBrace))
            {
                return null;
            }
            var names = new List<string>();
            while (true)
            {
                if (!input.MoveNext())
                {
                    Debug.LogError("logic error: EOF within group declaration");
                    return null;
                }
                if (!ExpectTokenOfType(input, "node name", TokenType.Name))
                {
                    return null;
                }
                names.Add(input.Current.Content);
                if (!input.MoveNext())
                {
                    Debug.LogError("logic error: EOF within group declaration");
                    return null;
                }
                switch (input.Current.Type)
                {
                    case TokenType.Comma:
                        continue;
                    case TokenType.RightBrace:
                        ExpectTerminator(input);
                        return names;
                    default:
                        Debug.LogError($"logic error at line #{input.Current.LineNumber}: expected comma or closing brace, got '#{input.Current.Content}'");
                        if (input.Current.Type != TokenType.Terminator)
                        {
                            SkipToNextTerminator(input);
                        }
                        return null;
                }
            }
        }

        // Reads a logic expression from the input, and, if successful, returns it in RPN
        // (reverse Polish notation) form.
        private static List<Token> ParseLogicExpression(IEnumerator<Token> input, Dictionary<string, List<Token>> macros)
        {
            // An implementation of the shunting yard algorithm follows, with added error checks
            // to guard against multiple terms or multiple operators in a row.
            var opStack = new Stack<Token>();
            var output = new List<Token>();
            var expectTerm = true;

            void ErrExpectedTerm()
            {
                Debug.LogError($"logic error at line #{input.Current.LineNumber}: expected term, got operator '{input.Current.Content}'");
            }

            void ErrExpectedOp()
            {
                Debug.LogError($"logic error at line #{input.Current.LineNumber}: expected operator, got term '{input.Current.Content}'");
            }

            while (true)
            {
                nextToken:
                if (!input.MoveNext())
                {
                    Debug.LogError("logic error: EOF within logic expression");
                    return null;
                }
                switch (input.Current.Type)
                {
                    case TokenType.Name:
                    case TokenType.Int:
                        if (!expectTerm)
                        {
                            ErrExpectedTerm();
                            return null;
                        }
                        if (macros.TryGetValue(input.Current.Content, out var expansion))
                        {
                            foreach (var term in expansion)
                            {
                                var t = term;
                                t.LineNumber = input.Current.LineNumber;
                                output.Add(t);
                            }
                        }
                        else
                        {
                            output.Add(input.Current);
                        }
                        expectTerm = false;
                        break;
                    case TokenType.Hash:
                        if (expectTerm)
                        {
                            ErrExpectedOp();
                            return null;
                        }
                        opStack.Push(input.Current);
                        expectTerm = true;
                        break;
                    case TokenType.And:
                        if (expectTerm)
                        {
                            ErrExpectedOp();
                            return null;
                        }
                        while (opStack.TryPeek(out var op) && op.Type == TokenType.Hash)
                        {
                            output.Add(opStack.Pop());
                        }
                        opStack.Push(input.Current);
                        expectTerm = true;
                        break;
                    case TokenType.Or:
                        if (expectTerm)
                        {
                            ErrExpectedOp();
                            return null;
                        }
                        while (opStack.TryPeek(out var op) && 
                            (op.Type == TokenType.Hash || op.Type == TokenType.And))
                        {
                            output.Add(opStack.Pop());
                        }
                        opStack.Push(input.Current);
                        expectTerm = true;
                        break;
                    case TokenType.LeftParen:
                        if (!expectTerm)
                        {
                            ErrExpectedTerm();
                            return null;
                        }
                        opStack.Push(input.Current);
                        expectTerm = true;
                        break;
                    case TokenType.RightParen:
                        if (expectTerm)
                        {
                            ErrExpectedOp();
                            return null;
                        }
                        while (opStack.TryPop(out var op))
                        {
                            if (op.Type == TokenType.LeftParen)
                            {
                                expectTerm = false;
                                goto nextToken;
                            }
                            output.Add(op);
                        }
                        Debug.LogError($"logic error at line #{input.Current.LineNumber}: unmatched closing parenthesis");
                        return null;
                    case TokenType.Terminator:
                        while (opStack.TryPop(out var op))
                        {
                            if (op.Type == TokenType.LeftParen)
                            {
                                Debug.LogError($"logic error at line #{op.LineNumber}: unmatched opening parenthesis");
                                return null;
                            }
                            output.Add(op);
                        }
                        return output;
                    default:
                        Debug.LogError($"logic error at line #{input.Current.LineNumber}: expected term or operator, got '{input.Current.Content}'");
                        return null;
                }
            }
        }

        private static List<LogicSet> EvalLogicExpression(List<Token> rpn, Func<string, string> expand)
        {
            var stack = new Stack<object>();

            foreach (var cmd in rpn)
            {
                switch (cmd.Type)
                {
                    case TokenType.Name:
                        stack.Push(new List<LogicSet>() {new (new List<LogicCondition> { new(expand(cmd.Content)) })});
                        break;
                    case TokenType.Int:
                        if (!int.TryParse(cmd.Content, out var n))
                        {
                            Debug.LogError($"logic error at line #{cmd.LineNumber}: integer out of range");
                            return null;
                        }
                        stack.Push(n);
                        break;
                    case TokenType.And:
                        if (!stack.TryPopOperands(out List<LogicSet> left, out List<LogicSet> right))
                        {
                            Debug.LogError($"logic error at line #{cmd.LineNumber}: expected terms as the operands of +");
                            return null;
                        }
                        var result = new List<LogicSet>();
                        foreach (var leftSet in left)
                        {
                            foreach (var rightSet in right)
                            {
                                result.Add(new(leftSet.Conditions.Concat(rightSet.Conditions).ToList()));
                            }
                        }
                        stack.Push(result);
                        break;
                    case TokenType.Or:
                        if (!stack.TryPopOperands(out left, out right))
                        {
                            Debug.LogError($"logic error at line #{cmd.LineNumber}: expected term sets as the operands of |");
                            return null;
                        }
                        stack.Push(left.Concat(right).ToList());
                        break;
                    case TokenType.Hash:
                        if (!stack.TryPopOperands(out int leftN, out right))
                        {
                            Debug.LogError($"logic error at line #{cmd.LineNumber}: expected integer and term set as the operands of #");
                            return null;
                        }
                        stack.Push(right.Select(
                            ls => new LogicSet(ls.Conditions.Select(
                                c => new LogicCondition(c.StateName, c.Count * leftN)).ToList())).ToList());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"unexpected token '{cmd.Content}' while evaluating logic expression at line #{cmd.LineNumber}");
                }
            }
            if (stack.Count == 1 && stack.Pop() is List<LogicSet> finalResult)
            {
                return finalResult;
            }
            throw new InvalidOperationException($"expected stack to finish with one list of LogicSets, but it didn't");
        }

        private static IReadOnlyList<IRandoNode> FindNodes(RoomScene scene, string pattern, Dictionary<string, List<string>> groups)
        {
            if (pattern.StartsWith("!"))
            {
                return scene.Nodes.Except(FindNodes(scene, pattern.Substring(1), groups)).ToList();
            }
            return groups.TryGetValue(pattern, out var list)
                ? list.SelectMany(scene.FindNodes).ToList()
                : scene.FindNodes(pattern);
        }

        private static void AddLogic(IReadOnlyList<IRandoNode> nodes1, IReadOnlyList<IRandoNode> nodes2, RoomScene scene, Dictionary<GraphEdge, List<LogicSet>> logicByEdge,
                                     LogicSet set)
        {
            foreach (var node1 in nodes1)
            {
                foreach (var node2 in nodes2)
                {
                    var edge = scene.Edges.FirstOrDefault(e => e.Origin == node1 && e.Destination == node2);
                    if (edge != null)
                    {
                        if (!logicByEdge.TryGetValue(edge, out var logicList))
                        {
                            logicList = new List<LogicSet>();
                            logicByEdge.Add(edge, logicList);
                        }

                        logicList.Add(set);
                    }
                }
            }
        }

        private static string ExpandAlias(string stateText, RoomScene scene)
        {
            var check = scene.Nodes.OfType<RandoCheck>().FirstOrDefault(c => c.Alias == stateText);
            return check != null ? LogicEvaluator.GetStateName(check) : stateText;
        }
    }
}
