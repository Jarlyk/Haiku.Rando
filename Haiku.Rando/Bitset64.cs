namespace Haiku.Rando
{
    internal struct Bitset64
    {
        private ulong _bits;

        public Bitset64(ulong b)
        {
            _bits = b;
        }

        private ulong Mask(int i)
        {
            if (!(i >= 0 && i < 64))
            {
                throw new System.ArgumentOutOfRangeException($"index {i} out of range [0,64[");
            }
            return 1UL << i;
        }

        public bool Contains(int i) => (_bits & Mask(i)) != 0;

        public void Add(int i)
        {
            _bits |= Mask(i);
        }

        public ulong Bits => _bits;
    }
}