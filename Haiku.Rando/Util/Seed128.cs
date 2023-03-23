using System.Text;
using System.Security.Cryptography;

namespace Haiku.Rando.Util
{
    public struct Seed128
    {
        public ulong S0;
        public ulong S1;

        public Seed128(string s)
        {
            var encoded = new UTF8Encoding().GetBytes(s);
            using var sha = SHA256.Create();
            var h = sha.ComputeHash(encoded);
            S0 = h[0] | ((ulong)h[1] << 8) | ((ulong)h[2] << 16) | ((ulong)h[3] << 24) | 
                ((ulong)h[4] << 32) | ((ulong)h[5] << 40) | ((ulong)h[6] << 48) |
                ((ulong)h[7] << 56);
            S1 = h[8] | ((ulong)h[9] << 8) | ((ulong)h[10] << 16) | ((ulong)h[11] << 24) |
                ((ulong)h[12] << 32) | ((ulong)h[13] << 40) | ((ulong)h[14] << 48) |
                ((ulong)h[15] << 56);
        }
    }
}