using System.Collections.Generic;
using System.IO;
using System.Linq;
using WalletWasabi.Serialization;
using WalletWasabi.Wallets.Slip39;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallets;

using uint8 = byte;

public class ShamirMnemonicTests
{
	private static readonly byte[] MS = "ABCDEFGHIJKLMNOP"u8.ToArray();

	[Fact]
	public void TestBasicSharingRandom()
	{
		byte[] secret = new byte[16];
		new Random().NextBytes(secret);
		var mnemonics = Shamir.Generate(1, [(3, 5)], secret);
		Assert.Equal(
			Shamir.Combine(mnemonics[..3]),
			Shamir.Combine(mnemonics[2..])
		);
	}

	[Fact]
	public void TestBasicSharingFixed()
	{
		var mnemonics = Shamir.Generate(1, [(3, 5)], MS);
		Assert.Equal(MS, Shamir.Combine(mnemonics[..3]));
		Assert.Equal(MS, Shamir.Combine(mnemonics[1..4]));
		Assert.Throws<ArgumentException>(() =>
			Shamir.Combine(mnemonics[..2])
		);
	}

	[Fact]
	public void TestPassphrase()
	{
		var mnemonics = Shamir.Generate(1, [(3, 5)], MS, "TREZOR");
		Assert.Equal(MS, Shamir.Combine(mnemonics[1..4], "TREZOR"));
		Assert.NotEqual(MS, Shamir.Combine(mnemonics[1..4]));
	}

	[Fact]
	public void TestNonExtendable()
	{
		var mnemonics = Shamir.Generate(1, [(3, 5)], MS, extendable: false);
		Assert.Equal(MS, Shamir.Combine(mnemonics[1..4]));
	}

	[Fact]
	public void TestIterationExponent()
	{
		var mnemonics = Shamir.Generate(1, [(3, 5)], MS, "TREZOR", iterationExponent: 1);
		Assert.Equal(MS, Shamir.Combine(mnemonics[1..4], "TREZOR"));
		Assert.NotEqual(MS, Shamir.Combine(mnemonics[1..4]));

		mnemonics = Shamir.Generate(1, [(3, 5)], MS, "TREZOR", iterationExponent: 2);
		Assert.Equal(MS, Shamir.Combine(mnemonics[1..4], "TREZOR"));
		Assert.NotEqual(MS, Shamir.Combine(mnemonics[1..4]));
	}

	[Fact]
	public void TestGroupSharing()
	{
		byte groupThreshold = 2;
		byte[] groupSizes = [5, 3, 5, 1];
		byte[] memberThresholds = [3, 2, 2, 1];
		var shares = Shamir.Generate(groupThreshold, memberThresholds.Zip(groupSizes).ToArray(), MS);
		var mnemonics = shares.GroupBy(x => x.GroupIndex).Select(x => x.ToArray()).ToArray();

		// Test all valid combinations of mnemonics.
		foreach (var groups in Combinations(mnemonics.Zip(memberThresholds, (a, b) => (Shares: a, MemberThreshold: b)),
			         groupThreshold))
		{
			foreach (var group1Subset in Combinations(groups[0].Shares, groups[0].MemberThreshold))
			{
				foreach (var group2Subset in Combinations(groups[1].Shares, groups[1].MemberThreshold))
				{
					var mnemonicSubset = Utils.Concat(group1Subset, group2Subset);
					mnemonicSubset = mnemonicSubset.OrderBy(x => Guid.NewGuid()).ToArray();
					Assert.Equal(MS, Shamir.Combine(mnemonicSubset));
				}
			}
		}

		Assert.Equal(MS, Shamir.Combine([mnemonics[2][0], mnemonics[2][2], mnemonics[3][0]]));
		Assert.Equal(MS, Shamir.Combine([mnemonics[2][3], mnemonics[3][0], mnemonics[2][4]]));

		Assert.Throws<ArgumentException>(() =>
			Shamir.Combine(Utils.Concat(mnemonics[0][2..], mnemonics[1][..1]))
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Combine(mnemonics[0][1..4])
		);
	}

	[Fact]
	public void TestGroupSharingThreshold1()
	{
		uint8 groupThreshold = 1;
		uint8[] groupSizes = [5, 3, 5, 1];
		uint8[] memberThresholds = [3, 2, 2, 1];
		var shares = Shamir.Generate(groupThreshold, memberThresholds.Zip(groupSizes).ToArray(), MS);
		var mnemonics = shares.GroupBy(x => x.GroupIndex).Select(x => x.ToArray()).ToArray();

		foreach (var (group, memberThreshold) in mnemonics.Zip(memberThresholds, (g, t) => (g, t)))
		{
			foreach (var groupSubset in Combinations(group, memberThreshold))
			{
				var mnemonicSubset = groupSubset.OrderBy(_ => Guid.NewGuid()).ToArray();
				Assert.Equal(MS, Shamir.Combine(mnemonicSubset));
			}
		}
	}

	[Fact]
	public void TestAllGroupsExist()
	{
		foreach (var groupThreshold in new uint8[] {1, 2, 5})
		{
			var shares = Shamir.Generate(groupThreshold, [(3, 5), (1, 1), (2, 3), (2, 5), (3, 5)], MS);
			var mnemonics = shares.GroupBy(x => x.GroupIndex).Select(x => x.ToArray()).ToArray();
			Assert.Equal(5, mnemonics.Length);
			Assert.Equal(19, mnemonics.Sum(g => g.Length));
		}
	}

	[Fact]
	public void TestInvalidSharing()
	{
		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(1, [(2, 3)], MS.Take(14).ToArray())
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(1, [(2, 3)], MS.Concat(new byte[] {0x58}).ToArray())
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(3, [(3, 5), (2, 5)], MS)
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(0, [(3, 5), (2, 5)], MS)
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(2, [(3, 2), (2, 5)], MS)
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(2, [(0, 2), (2, 5)], MS)
		);

		Assert.Throws<ArgumentException>(() =>
			Shamir.Generate(2, [(3, 5), (1, 3), (2, 5)], MS)
		);
	}

	[Theory]
	[MemberData(nameof(Slip39TestVector.TestCasesData), MemberType = typeof(Slip39TestVector))]
	public void TestVectors(Slip39TestVector test)
	{
		if (!string.IsNullOrEmpty(test.secretHex))
		{
			var shares = test.mnemonics.Select(Share.FromMnemonic).ToArray();
			var secret = Shamir.Combine(shares, "TREZOR");
			Assert.Equal(test.secretHex, Convert.ToHexString(secret).ToLower());

			//Assert.Equal(new BIP32Key(secret).ExtendedKey(), xprv);
		}
		else
		{
			Assert.Throws<ArgumentException>(() =>
			{
				var shares = test.mnemonics.Select(Share.FromMnemonic).ToArray();
				Shamir.Combine(shares);
				Assert.Fail($"Failed to raise exception for test vector \"{test.description}\".");
			});
		}
	}

	public static T[][] Combinations<T>(IEnumerable<T> iterable, int r)
	{
		IEnumerable<IEnumerable<T>> InternalCombinations()
		{
			var pool = iterable.ToArray();
			int n = pool.Length;
			if (r > n)
			{
				yield break;
			}

			var indices = Enumerable.Range(0, r).ToArray();

			yield return indices.Select(i => pool[i]);

			while (true)
			{
				int i;
				for (i = r - 1; i >= 0; i--)
				{
					if (indices[i] != i + n - r)
					{
						break;
					}
				}

				if (i < 0)
				{
					yield break;
				}

				indices[i]++;
				for (int j = i + 1; j < r; j++)
				{
					indices[j] = indices[j - 1] + 1;
				}

				yield return indices.Select(index => pool[index]);
			}
		}

		return InternalCombinations().Select(x => x.ToArray()).ToArray();
	}
}

public record Slip39TestVector(string description, string[] mnemonics, string secretHex, string xprv)
{
	private static readonly Decoder<Slip39TestVector> Slip39TestDecoder =
		Decode.Tuple4(Decode.String, Decode.Array(Decode.String), Decode.String, Decode.String)
			.AndThen(x => Decode.Succeed(new Slip39TestVector(x.d0, x.d1, x.d2, x.d3)));
	private static IEnumerable<Slip39TestVector> VectorsData()
	{
		string vectorsJson = File.ReadAllText("./UnitTests/Data/Slip39TestVectors.json");
		var fileDecoder = JsonDecoder.FromString(Decode.Array(Slip39TestDecoder));
		return fileDecoder(vectorsJson).Match(
			ts => ts,
			e => throw new Exception($"Something went wrong: {e}")
		);
	}

	private static readonly Slip39TestVector[] TestCases = VectorsData().ToArray();

	public static IEnumerable<object[]> TestCasesData =>
		TestCases.Select(testCase => new object[] {testCase});

	public override string ToString() => description;

}

