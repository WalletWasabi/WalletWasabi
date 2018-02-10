using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Backend.Gcs
{
	// Implements a Golomb-coded set to be use in the creation of client-side filter
	// for a new kind Bitcoin light clients. This code is based on the BIP:
	// https://github.com/Roasbeef/bips/blob/master/gcs_light_client.mediawiki
	public class GCSFilter
	{
		public byte P { get; }
		public int N { get; }
		public ulong ModulusP { get; }
		public ulong ModulusNP { get; }
		public BitArray Data { get; }

		public GCSFilter(BitArray data, byte P, int N)
		{
			this.P = P;
			this.N = N;


			var modP = 1UL << P;
			this.ModulusP = modP;
			this.ModulusNP = ((ulong)N) * modP;
		}

		public static GCSFilter Build(byte[] k, byte P, IEnumerable<byte[]> data)
		{
			if (P == 0x00)
				throw new ArgumentException("P cannot be zero", nameof(P));

			var bytesData = data as byte[][] ?? data.ToArray();
			if (data == null || !bytesData.Any())
				throw new ArgumentException("data can not be null or empty array", nameof(data));


			var hs = ConstructHashedSet(P, k, bytesData);
			var filterData = Compress(hs, P);
			var N = bytesData.Length;

			return new GCSFilter(filterData, P, N);
		}


		internal static List<ulong> ConstructHashedSet(byte P, byte[] key, IEnumerable<byte[]> data)
		{
			// N the number of items to be inserted into the set
			var dataArrayBytes = data as byte[][] ?? data.ToArray();
			var N = dataArrayBytes.Count();

			// The list of data item hashes
			var values = new ConcurrentBag<ulong>();
			var modP = 1UL << P;
			var modNP = ((ulong)N) * modP;
			var nphi = modNP >> 32;
			var nplo = (ulong)((uint)modNP);

			// Process the data items and calculate the 64 bits hash for each of them
			Parallel.ForEach(dataArrayBytes, item =>
			{
				var hash = SipHasher.Hash(key, item);
				var value = FastReduction(hash, nphi, nplo);
				values.Add(value);
			});

			var ret = values.ToList();
			ret.Sort();
			return ret;
		}

		private static BitArray Compress(List<ulong> values, byte P)
		{
			var bitArray = new BitArray();
			var bitStream = new BitStream(bitArray);
			var sw = new GRCodedStreamWriter(bitStream, P);

			foreach (var value in values)
			{
				sw.Write(value);
			}
			return bitArray;
		}


		public bool Match(byte[] data, byte[] key)
		{
			return MatchAny(new[] { data }, key);
		}

		public bool MatchAny(IEnumerable<byte[]> data, byte[] key)
		{
			if (data == null || !data.Any())
				throw new ArgumentException("data can not be null or empty array", nameof(data));

			var hs = ConstructHashedSet(P, key, data);

			var lastValue1 = 0UL;
			var lastValue2 = hs[0];
			var i = 1;

			var bitStream = new BitStream(Data);
			var sr = new GRCodedStreamReader(bitStream, P, 0);

			while (lastValue1 != lastValue2)
			{
				if (lastValue1 > lastValue2)
				{
					if (i < hs.Count)
					{
						lastValue2 = hs[i];
						i++;
					}
					else
					{
						return false;
					}
				}
				else if (lastValue2 > lastValue1)
				{
					var val = sr.Read();
					lastValue1 = val;
				}
			}
			return true;
		}

		internal static ulong FastReduction(ulong value, ulong nhi, ulong nlo)
		{
			// First, we'll spit the item we need to reduce into its higher and
			// lower bits.
			var vhi = value >> 32;
			var vlo = (ulong)((uint)value);

			// Then, we distribute multiplication over each part.
			var vnphi = vhi * nhi;
			var vnpmid = vhi * nlo;
			var npvmid = nhi * vlo;
			var vnplo = vlo * nlo;

			// We calculate the carry bit.
			var carry = ((ulong)((uint)vnpmid) + (ulong)((uint)npvmid) +
			(vnplo >> 32)) >> 32;

			// Last, we add the high bits, the middle bits, and the carry.
			value = vnphi + (vnpmid >> 32) + (npvmid >> 32) + carry;

			return value;
		}

	}
}