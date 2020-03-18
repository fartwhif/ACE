using System;

namespace ACE.Common.Cryptography
{
    public class CryptoSystem : ISAAC
    {
        public uint CurrentKey;
        public CryptoSystem(uint seed) : base()
        {
            Init(BitConverter.GetBytes(seed));
            ConsumeKey();
        }
        public CryptoSystem(byte[] seed) : base()
        {
            Init(seed);
            ConsumeKey();
        }
        public void ConsumeKey()
        {
            CurrentKey = Next();
        }
    }
}
