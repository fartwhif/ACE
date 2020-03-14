using System;

namespace ACE.Common.Cryptography
{
    /// <summary>
    /// benchmark comparison with old version of ISAAC (higher is better)
    /// debug:   new 86%  old 100%
    /// release: new 112% old 100%
    /// </summary>
    public class ISAAC {
        public const int SIZEL = 8;              /* log of size of rsl[] and mem[] */
        public const int SIZE = 1 << SIZEL;               /* size of rsl[] and mem[] */
        public const int MASK = (SIZE - 1) << 2;            /* for pseudorandom lookup */
        public uint count;                           /* count through the results in rsl[] */
        public uint[] rsl;                                /* the results given to the user */
        private uint[] mem;                                   /* the internal state */
        private uint a;                                              /* accumulator */
        private uint b;                                          /* the last result */
        private uint c;              /* counter, guarantees cycle is at least 2^^40 */

        public uint Next() {
            if (0 == count--) {
                Isaac();
                count = SIZE - 1;
            }
            return rsl[count];
        }

        public ISAAC() {
            mem = new uint[SIZE];
            rsl = new uint[SIZE];
            Init(false);
        }
        public void Init(byte[] seed) {
            var x = BitConverter.ToUInt32(seed, 0);
            Init2(x, x, x);
        }

        private void Init2(uint a, uint b, uint c) {
            this.a = a;
            this.b = b;
            this.c = c;

            mem = new uint[SIZE];
            rsl = new uint[SIZE];
            Init(true);
        }

        /* Generate 256 results.  This is a fast (not small) implementation. */
        public void Isaac() {
            uint i, j, x, y;

            b += ++c;
            for (i = 0, j = SIZE / 2; i < SIZE / 2;) {
                x = mem[i];
                a ^= a << 13;
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;

                x = mem[i];
                a ^= (a >> 6);
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;

                x = mem[i];
                a ^= a << 2;
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;

                x = mem[i];
                a ^= (a >> 16);
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;
            }

            for (j = 0; j < SIZE / 2;) {
                x = mem[i];
                a ^= a << 13;
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;

                x = mem[i];
                a ^= (a >> 6);
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;

                x = mem[i];
                a ^= a << 2;
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;

                x = mem[i];
                a ^= (a >> 16);
                a += mem[j++];
                mem[i] = y = mem[(x & MASK) >> 2] + a + b;
                rsl[i++] = b = mem[((y >> SIZEL) & MASK) >> 2] + x;
            }
        }


        /* initialize, or reinitialize, this instance of rand */
        public void Init(bool flag) {
            uint i;
            uint a, b, c, d, e, f, g, h;
            a = b = c = d = e = f = g = h = 0x9e3779b9;                        /* the golden ratio */

            for (i = 0; i < 4; ++i) {
                a ^= b << 11; d += a; b += c;
                b ^= (c >> 2); e += b; c += d;
                c ^= d << 8; f += c; d += e;
                d ^= (e >> 16); g += d; e += f;
                e ^= f << 10; h += e; f += g;
                f ^= (g >> 4); a += f; g += h;
                g ^= h << 8; b += g; h += a;
                h ^= (a >> 9); c += h; a += b;
            }

            for (i = 0; i < SIZE; i += 8) {              /* fill in mem[] with messy stuff */
                if (flag) {
                    a += rsl[i]; b += rsl[i + 1]; c += rsl[i + 2]; d += rsl[i + 3];
                    e += rsl[i + 4]; f += rsl[i + 5]; g += rsl[i + 6]; h += rsl[i + 7];
                }
                a ^= b << 11; d += a; b += c;
                b ^= (c >> 2); e += b; c += d;
                c ^= d << 8; f += c; d += e;
                d ^= (e >> 16); g += d; e += f;
                e ^= f << 10; h += e; f += g;
                f ^= (g >> 4); a += f; g += h;
                g ^= h << 8; b += g; h += a;
                h ^= (a >> 9); c += h; a += b;
                mem[i] = a; mem[i + 1] = b; mem[i + 2] = c; mem[i + 3] = d;
                mem[i + 4] = e; mem[i + 5] = f; mem[i + 6] = g; mem[i + 7] = h;
            }

            if (flag) {           /* second pass makes all of seed affect all of mem */
                for (i = 0; i < SIZE; i += 8) {
                    a += mem[i]; b += mem[i + 1]; c += mem[i + 2]; d += mem[i + 3];
                    e += mem[i + 4]; f += mem[i + 5]; g += mem[i + 6]; h += mem[i + 7];
                    a ^= b << 11; d += a; b += c;
                    b ^= (c >> 2); e += b; c += d;
                    c ^= d << 8; f += c; d += e;
                    d ^= (e >> 16); g += d; e += f;
                    e ^= f << 10; h += e; f += g;
                    f ^= (g >> 4); a += f; g += h;
                    g ^= h << 8; b += g; h += a;
                    h ^= (a >> 9); c += h; a += b;
                    mem[i] = a; mem[i + 1] = b; mem[i + 2] = c; mem[i + 3] = d;
                    mem[i + 4] = e; mem[i + 5] = f; mem[i + 6] = g; mem[i + 7] = h;
                }
            }

            Isaac();
            count = SIZE;
        }
    }
}
