using System;
using System.Collections.Generic;
using System.Text;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Logic
{
    public sealed class SceneLogic
    {
        public SceneLogic(IReadOnlyDictionary<GraphEdge, IReadOnlyList<LogicSet>> logicByEdge)
        {
            LogicByEdge = logicByEdge;
        }

        public IReadOnlyDictionary<GraphEdge, IReadOnlyList<LogicSet>> LogicByEdge { get; }
    }
}
