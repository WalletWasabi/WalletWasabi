using System;

namespace MagicalCryptoWallet.Backend.Gcs
{
	internal class SipHasher
	{
		ulong v_0;
		ulong v_1;
		ulong v_2;
		ulong v_3;
		ulong count;
		ulong tmp;
		public SipHasher(ulong k0, ulong k1)
		{
			v_0 = 0x736f6d6570736575UL ^ k0;
			v_1 = 0x646f72616e646f6dUL ^ k1;
			v_2 = 0x6c7967656e657261UL ^ k0;
			v_3 = 0x7465646279746573UL ^ k1;
			count = 0;
			tmp = 0;
		}

		public SipHasher Write(byte[] data)
		{
			ulong v0 = v_0, v1 = v_1, v2 = v_2, v3 = v_3;
			var size = data.Length;
			var t = tmp;
			var c = count;
			int offset = 0;

			while (size-- != 0)
			{
				t |= ((ulong)((data[offset++]))) << (int)(8 * (c % 8));
				c++;
				if ((c & 7) == 0)
				{
					v3 ^= t;
					SIPROUND(ref v0, ref v1, ref v2, ref v3);
					SIPROUND(ref v0, ref v1, ref v2, ref v3);
					v0 ^= t;
					t = 0;
				}
			}

			v_0 = v0;
			v_1 = v1;
			v_2 = v2;
			v_3 = v3;
			count = c;
			tmp = t;

			return this;
		}

		public ulong Finalize()
		{
			ulong v0 = v_0, v1 = v_1, v2 = v_2, v3 = v_3;

			ulong t = tmp | (((ulong)count) << 56);

			v3 ^= t;
			SIPROUND(ref v0, ref v1, ref v2, ref v3);
			SIPROUND(ref v0, ref v1, ref v2, ref v3);
			v0 ^= t;
			v2 ^= 0xFF;
			SIPROUND(ref v0, ref v1, ref v2, ref v3);
			SIPROUND(ref v0, ref v1, ref v2, ref v3);
			SIPROUND(ref v0, ref v1, ref v2, ref v3);
			SIPROUND(ref v0, ref v1, ref v2, ref v3);
			return v0 ^ v1 ^ v2 ^ v3;
		}

		static void SIPROUND(ref ulong v_0, ref ulong v_1, ref ulong v_2, ref ulong v_3)
		{
			v_0 += v_1;
			v_1 = rotl64(v_1, 13);
			v_1 ^= v_0;
			v_0 = rotl64(v_0, 32);
			v_2 += v_3;
			v_3 = rotl64(v_3, 16);
			v_3 ^= v_2;
			v_0 += v_3;
			v_3 = rotl64(v_3, 21);
			v_3 ^= v_0;
			v_2 += v_1;
			v_1 = rotl64(v_1, 17);
			v_1 ^= v_2;
			v_2 = rotl64(v_2, 32);
		}

		public static ulong Hash(byte[] key, byte[] data)
		{
			var k0 = BitConverter.ToUInt64(key, 0);
			var k1 = BitConverter.ToUInt64(key, 8);

			var hasher = new SipHasher(k0, k1);
			hasher.Write(data);
			return hasher.Finalize();
		}

		private static ulong rotl64(ulong x, byte b)
		{
			return (((x) << (b)) | ((x) >> (64 - (b))));
		}
	}
}