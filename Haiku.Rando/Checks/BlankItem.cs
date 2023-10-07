using System;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    internal class BlankItem : IRandoItem
    {
        public static BlankItem Instance = new();

        private BlankItem() {}

        public void Give(MonoBehaviour self)
        {
            throw new InvalidOperationException("impossible");
        }

        public bool Obtained() => true;

        public UIDef UIDef() => new();

        public string UIName() => "";

        public override string ToString() => "Blank";

        public int Index => int.MinValue;
    }
}