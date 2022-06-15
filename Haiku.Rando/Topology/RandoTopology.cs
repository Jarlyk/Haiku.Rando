using System;
using System.Collections.Generic;
using System.Text;

namespace Haiku.Rando.Topology
{
    public sealed class RandoTopology
    {
        public IReadOnlyList<IRandoNode> Nodes { get; }

        public IReadOnlyList<IRandoEdge> Edges { get; }
    }
}
