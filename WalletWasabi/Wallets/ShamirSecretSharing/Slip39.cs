using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using uint8 = byte;
using uint16 = ushort;
using MemberData = (byte memberThreshold, byte memberIndex, byte[] value);

namespace WalletWasabi.Wallets.Slip39;

using Group = (uint8 memberThreshold, uint8 count);
using ShareData = (uint8 memberIndex, uint8[] value);
using CommonParameters = (uint16 id, uint8, uint8, Dictionary<uint8, List<MemberData>>);

/// <summary>
/// A class for implementing Shamir's Secret Sharing with SLIP-39 enhancements.
/// </summary>
public record Shamir
{
	private const int SECRET_INDEX = 255;
	private const int DIGEST_INDEX = 254;
	internal const int MIN_STRENGTH_BITS = 128;
	private const int MAX_SHARE_COUNT = 16;
	private const int DIGEST_LENGTH_BYTES = 4;
	private const int BASE_ITERATION_COUNT = 10000;
	private const int ROUND_COUNT = 4;

	/// <summary>
	/// Generates SLIP-39 shares from a given seed.
	/// </summary>
	/// <param name="threshold">Number of shares necessary to recombine the see.</param>
	/// <param name="shares">Number of shares in which the seed will be splitted.</param>
	/// <param name="seed">The secret to be split into shares.</param>
	/// <param name="passphrase">The passphrase used for encryption.</param>
	/// <param name="iterationExponent">Exponent to determine the number of iterations for the encryption algorithm.</param>
	/// <param name="extendable"></param>
	/// <returns>A list of shares that can be used to reconstruct the secret.</returns>
	/// <exception cref="ArgumentException">Thrown when inputs do not meet the required constraints.</exception>
	public static Share[] Generate(
		uint8 threshold,
		uint8 shares,
		uint8[] seed,
		string passphrase = "",
		uint8 iterationExponent = 0,
		bool extendable = true)
	{
		return Generate(1, [(threshold, shares)], seed, passphrase, iterationExponent, extendable);
	}

	/// <summary>
	/// Generates SLIP-39 shares from a given seed.
	/// </summary>
	/// <param name="groupThreshold">The number of groups required to reconstruct the secret.</param>
	/// <param name="groups">Array of tuples where each tuple represents (groupThreshold, shareCount) for each group.</param>
	/// <param name="seed">The secret to be split into shares.</param>
	/// <param name="passphrase">The passphrase used for encryption.</param>
	/// <param name="iterationExponent">Exponent to determine the number of iterations for the encryption algorithm.</param>
	/// <param name="extendable"></param>
	/// <returns>A list of shares that can be used to reconstruct the secret.</returns>
	/// <exception cref="ArgumentException">Thrown when inputs do not meet the required constraints.</exception>
	public static Share[] Generate(
		uint8 groupThreshold,
		Group[] groups,
		uint8[] seed,
		string passphrase = "",
		uint8 iterationExponent = 0,
		bool extendable = true)
	{
		var secret = seed;
		// Validating seed strength and format
		if (secret.Length * 8 < MIN_STRENGTH_BITS || secret.Length % 2 != 0)
		{
			throw new ArgumentException("master key entropy must be at least 128 bits and multiple of 16 bits");
		}

		// Validating group constraints
		if (groupThreshold > MAX_SHARE_COUNT)
		{
			throw new ArgumentException("more than 16 groups are not supported");
		}

		if (groupThreshold > groups.Length)
		{
			throw new ArgumentException("group threshold should not exceed number of groups");
		}

		if (groups.Any(group => group is {memberThreshold: 1, count: > 1}))
		{
			throw new ArgumentException("can only generate one share for threshold = 1");
		}

		if (groups.Any(group => group.memberThreshold > group.count))
		{
			throw new ArgumentException("number of shares must not be less than threshold");
		}

		// Generate a random identifier
		var id = (uint16) (BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)) %
		                   ((1 << (Share.ID_LENGTH_BITS + 1)) - 1));
		var shares = new List<Share>();

		// Encrypt the secret using the passphrase and identifier
		var encryptedSecret = Encrypt(id, iterationExponent, secret, passphrase, extendable);

		// Split the encrypted secret into group shares
		var groupShares = SplitSecret(
			groupThreshold,
			(byte) groups.Length,
			encryptedSecret);

		// Split each group share into member shares and create the final share objects
		foreach (var (groupIndex, groupShare) in groupShares)
		{
			var (memberThreshold, count) = groups[groupIndex];

			var memberShares = SplitSecret(memberThreshold, count, groupShare);
			foreach (var (memberIndex, value) in memberShares)
			{
				shares.Add(new Share(
					id,
					extendable,
					iterationExponent,
					groupIndex,
					groupThreshold,
					(byte) groups.Length,
					memberIndex,
					memberThreshold,
					value));
			}
		}

		return shares.ToArray();
	}

	/// <summary>
	/// Combines shares to reconstruct the original secret.
	/// </summary>
	/// <param name="shares">The array of shares to combine.</param>
	/// <param name="passphrase">The passphrase used for decrypting the shares.</param>
	/// <param name="extendable"></param>
	/// <returns>The reconstructed secret.</returns>
	/// <exception cref="ArgumentException">Thrown when the shares are insufficient or invalid.</exception>
	public static uint8[] Combine(Share[] shares, string passphrase = "")
	{
		// Preprocess the shares to extract group and member information
		var (id, iterationExponent, groupThreshold, groups) = Preprocess(shares);

		// Validating group constraints
		if (groups.Count < groupThreshold)
		{
			throw new ArgumentException("need shares from more groups to reconstruct secret");
		}

		if (groups.Count != groupThreshold)
		{
			throw new ArgumentException("shares from too many groups");
		}

		if (groups.Any(group => group.Value[0].Item1 != group.Value.Count))
		{
			throw new ArgumentException("for every group, number of member shares should match member threshold");
		}

		if (groups.Any(group => group.Value.Select(v => v.memberThreshold).ToHashSet().Count > 1))
		{
			throw new ArgumentException("member threshold must be the same within a group");
		}

		// Recover secrets for each group and then combine them to get the final secret
		var groupSecrets = new List<ShareData>();
		foreach (var group in groups)
		{
			var recoveredSecret = RecoverSecret(group.Value[0].memberThreshold,
				group.Value.Select(v => (v.memberIndex, v.value)).ToArray());
			groupSecrets.Add((group.Key, recoveredSecret));
		}

		var finalRecoveredSecret = RecoverSecret(groupThreshold, groupSecrets.ToArray());

		// Decrypt the secret using the passphrase
		var decryptedSeed = Decrypt(id, iterationExponent, finalRecoveredSecret, passphrase, shares[0].Extendable);
		return decryptedSeed;
	}

	/// <summary>
	/// Preprocesses the shares to group them by group index and validate constraints.
	/// </summary>
	/// <param name="shares">The array of shares to preprocess.</param>
	/// <returns>A tuple containing identifiers and group information for the shares.</returns>
	/// <exception cref="ArgumentException">Thrown when the shares do not meet the required constraints.</exception>
	private static CommonParameters Preprocess(Share[] shares)
	{
		if (shares.Length < 1)
		{
			throw new ArgumentException("need at least one share to reconstruct secret");
		}

		// Ensure all shares belong to the same secret
		var identifiers = shares.Select(s => s.Id).ToHashSet();
		if (identifiers.Count > 1)
		{
			throw new ArgumentException("shares do not belong to the same secret");
		}

		// Ensure all shares have the same iteration exponent, group threshold, and group count
		var iterationExponents = shares.Select(s => s.IterationExponent).ToHashSet();
		if (iterationExponents.Count > 1)
		{
			throw new ArgumentException("shares do not have the same iteration exponent");
		}

		var groupThresholds = shares.Select(s => s.GroupThreshold).ToHashSet();
		if (groupThresholds.Count > 1)
		{
			throw new ArgumentException("shares do not have the same group threshold");
		}

		var groupCounts = shares.Select(s => s.GroupCount).ToHashSet();
		if (groupCounts.Count > 1)
		{
			throw new ArgumentException("shares do not have the same group count");
		}

		if (shares.Any(s => s.GroupThreshold > s.GroupCount))
		{
			throw new ArgumentException("greater group threshold than group counts");
		}

		// Group the shares by group index
		var groups = new Dictionary<uint8, List<MemberData>>();
		foreach (var share in shares)
		{
			if (!groups.TryGetValue(share.GroupIndex, out var value))
			{
				value = new List<MemberData>();
				groups[share.GroupIndex] = value;
			}

			value.Add((share.MemberThreshold, share.MemberIndex, share.Value.ToArray()));
		}

		return (identifiers.First(),
			iterationExponents.First(),
			groupThresholds.First(),
			groups);
	}

	/// <summary>
	/// Recovers the secret from a set of shares.
	/// </summary>
	/// <param name="threshold">The number of shares required to reconstruct the secret.</param>
	/// <param name="shares">The shares to be used for reconstruction.</param>
	/// <returns>The recovered secret.</returns>
	/// <exception cref="ArgumentException">Thrown when the share digests are incorrect.</exception>
	public static uint8[] RecoverSecret(uint8 threshold, ShareData[] shares)
	{
		// If the threshold is 1, simply return the first share's value
		if (threshold == 1)
		{
			return shares[0].value;
		}

		// Interpolate the shares to recover the shared secret and digest
		var sharedSecret = Interpolate(shares, SECRET_INDEX);
		var digestShare = Interpolate(shares, DIGEST_INDEX);

		// Verify the share digest. (poor-man constant-time comparison)
		if (BitConverter.ToUInt32(ShareDigest(digestShare[DIGEST_LENGTH_BYTES..], sharedSecret)) !=
		    BitConverter.ToUInt32(digestShare[..DIGEST_LENGTH_BYTES]))
		{
			throw new ArgumentException("share digest incorrect");
		}

		return sharedSecret;
	}

	private static ShareData[] SplitSecret(uint8 threshold, uint8 shareCount, uint8[] sharedSecret)
	{
		if (threshold < 1)
		{
			throw new ArgumentException("sharing threshold must be > 1");
		}

		if (shareCount > MAX_SHARE_COUNT)
		{
			throw new ArgumentException("too many shares");
		}

		if (threshold > shareCount)
		{
			throw new ArgumentException("number of shares should be at least equal threshold");
		}

		var shares = new List<ShareData>();

		if (threshold == 1)
		{
			for (uint8 i = 0; i < shareCount; i++)
			{
				shares.Add((i, sharedSecret.ToArray()));
			}

			return shares.ToArray();
		}

		int randomSharesCount = Math.Max(threshold - 2, 0);

		using var rng = RandomNumberGenerator.Create();
		for (uint8 i = 0; i < randomSharesCount; i++)
		{
			var share = new uint8[sharedSecret.Length];
			rng.GetBytes(share);
			shares.Add((i, share));
		}

		var baseShares = new List<ShareData>(shares);
		var randomPart = new uint8[sharedSecret.Length - DIGEST_LENGTH_BYTES];
		rng.GetBytes(randomPart);

		var digest = ShareDigest(randomPart, sharedSecret);
		baseShares.Add((DIGEST_INDEX, Utils.Concat(digest, randomPart)));
		baseShares.Add((SECRET_INDEX, sharedSecret));

		for (uint8 i = (uint8) randomSharesCount; i < shareCount; i++)
		{
			var interpolatedShare = Interpolate(baseShares.ToArray(), i);
			shares.Add((i, interpolatedShare));
		}

		return shares.ToArray();
	}

	private static uint8[] ShareDigest(uint8[] random, uint8[] sharedSecret)
	{
		using var hmac = new HMACSHA256(random);
		var hash = hmac.ComputeHash(sharedSecret);
		return hash[..4];
	}

	/// <summary>
	/// Interpolates the shares to recover the secret.
	/// </summary>
	/// <param name="shares">The shares used for interpolation.</param>
	/// <param name="x">The index of the value to interpolate (secret or digest).</param>
	/// <returns>The interpolated value.</returns>
	private static uint8[] Interpolate(ShareData[] shares, uint8 x)
	{
		var xCoordinates = shares.Select(share => share.memberIndex).ToHashSet();
		if (xCoordinates.Count != shares.Length)
		{
			throw new ArgumentException("need unique shares for interpolation");
		}

		if (shares.Length < 1)
		{
			throw new ArgumentException("need at least one share for interpolation");
		}

		var len = shares[0].value.Length;
		if (shares.Any(share => share.value.Length != len))
		{
			throw new ArgumentException("shares should have equal length");
		}

		if (xCoordinates.Contains(x))
		{
			return shares.First(share => share.memberIndex == x).value;
		}

		static int Mod255(int n)
		{
			while (n < 0)
			{
				n += 255;
			}

			return n % 255;
		}

		int logProd = shares
			.Select(share => Log[share.memberIndex ^ x])
			.Aggregate(0, (a, v) => a + v);

		var result = new uint8[len];
		foreach (var (i, share) in shares)
		{
			var logBasis = Mod255(
				logProd - Log[i ^ x]
				        - shares.Select(j => Log[j.Item1 ^ i]).Aggregate(0, (a, v) => a + v)
			);

			for (var k = 0; k < share.Length; k++)
			{
				result[k] ^= share[k] != 0 ? Exp[Mod255(Log[share[k]] + logBasis)] : (uint8) 0;
			}
		}

		return result;
	}

	private static uint8[] Encrypt(uint16 identifier, uint8 iterationExponent, uint8[] master, string passphrase,
		bool extendable) =>
		Crypt(identifier, iterationExponent, master, [0, 1, 2, 3], CheckPassphrase(passphrase), extendable);

	private static uint8[] Decrypt(uint16 identifier, uint8 iterationExponent, uint8[] master, string passphrase,
		bool extendable) =>
		Crypt(identifier, iterationExponent, master, [3, 2, 1, 0], CheckPassphrase(passphrase), extendable);

	private static string CheckPassphrase(string passphrase) =>
		passphrase.Any(char.IsControl)
			? throw new NotSupportedException("Passphrase should only contain printable ASCII.")
			: passphrase;

	private static uint8[] Crypt(
		uint16 identifier,
		uint8 iterationExponent,
		uint8[] masterSecret,
		uint8[] range,
		string passphrase,
		bool extendable)
	{
		var len = masterSecret.Length / 2;
		var left = masterSecret[..len];
		var right = masterSecret[len..];
		foreach (var i in range)
		{
			var f = Feistel(identifier, iterationExponent, i, right, passphrase, extendable);
			(left, right) = (right, Xor(left, f));
		}

		return Utils.Concat(right, left);
	}

	private static uint8[] Feistel(uint16 id, uint8 iterationExponent, uint8 step, uint8[] block, string passphrase,
		bool extendable)
	{
		var key = Utils.Concat([step], Encoding.UTF8.GetBytes(passphrase));
		var saltPrefix = extendable ? [] : Utils.Concat("shamir"u8.ToArray(), [(uint8) (id >> 8), (uint8) (id & 0xff)]);
		var salt = Utils.Concat(saltPrefix, block);
		var iters = (BASE_ITERATION_COUNT / ROUND_COUNT) << iterationExponent;
		var pbkdf2 =  Rfc2898DeriveBytes.Pbkdf2(key, salt, iters, HashAlgorithmName.SHA256, block.Length);
		return pbkdf2;
	}

	private static byte[] Xor(byte[] a, byte[] b)
	{
		byte[] result = new byte[a.Length];
		for (int i = 0; i < a.Length; i++)
		{
			result[i] = (byte) (a[i] ^ b[i]);
		}

		return result;
	}

	private static readonly uint8[] Exp =
	[
		1, 3, 5, 15, 17, 51, 85, 255, 26, 46, 114, 150, 161, 248, 19, 53, 95, 225, 56, 72, 216,
		115, 149, 164, 247, 2, 6, 10, 30, 34, 102, 170, 229, 52, 92, 228, 55, 89, 235, 38, 106,
		190, 217, 112, 144, 171, 230, 49, 83, 245, 4, 12, 20, 60, 68, 204, 79, 209, 104, 184, 211,
		110, 178, 205, 76, 212, 103, 169, 224, 59, 77, 215, 98, 166, 241, 8, 24, 40, 120, 136, 131,
		158, 185, 208, 107, 189, 220, 127, 129, 152, 179, 206, 73, 219, 118, 154, 181, 196, 87,
		249, 16, 48, 80, 240, 11, 29, 39, 105, 187, 214, 97, 163, 254, 25, 43, 125, 135, 146, 173,
		236, 47, 113, 147, 174, 233, 32, 96, 160, 251, 22, 58, 78, 210, 109, 183, 194, 93, 231, 50,
		86, 250, 21, 63, 65, 195, 94, 226, 61, 71, 201, 64, 192, 91, 237, 44, 116, 156, 191, 218,
		117, 159, 186, 213, 100, 172, 239, 42, 126, 130, 157, 188, 223, 122, 142, 137, 128, 155,
		182, 193, 88, 232, 35, 101, 175, 234, 37, 111, 177, 200, 67, 197, 84, 252, 31, 33, 99, 165,
		244, 7, 9, 27, 45, 119, 153, 176, 203, 70, 202, 69, 207, 74, 222, 121, 139, 134, 145, 168,
		227, 62, 66, 198, 81, 243, 14, 18, 54, 90, 238, 41, 123, 141, 140, 143, 138, 133, 148, 167,
		242, 13, 23, 57, 75, 221, 124, 132, 151, 162, 253, 28, 36, 108, 180, 199, 82, 246,
	];

	private static readonly uint8[] Log =
	[
		0, 0, 25, 1, 50, 2, 26, 198, 75, 199, 27, 104, 51, 238, 223, 3, 100, 4, 224, 14, 52, 141,
		129, 239, 76, 113, 8, 200, 248, 105, 28, 193, 125, 194, 29, 181, 249, 185, 39, 106, 77,
		228, 166, 114, 154, 201, 9, 120, 101, 47, 138, 5, 33, 15, 225, 36, 18, 240, 130, 69, 53,
		147, 218, 142, 150, 143, 219, 189, 54, 208, 206, 148, 19, 92, 210, 241, 64, 70, 131, 56,
		102, 221, 253, 48, 191, 6, 139, 98, 179, 37, 226, 152, 34, 136, 145, 16, 126, 110, 72, 195,
		163, 182, 30, 66, 58, 107, 40, 84, 250, 133, 61, 186, 43, 121, 10, 21, 155, 159, 94, 202,
		78, 212, 172, 229, 243, 115, 167, 87, 175, 88, 168, 80, 244, 234, 214, 116, 79, 174, 233,
		213, 231, 230, 173, 232, 44, 215, 117, 122, 235, 22, 11, 245, 89, 203, 95, 176, 156, 169,
		81, 160, 127, 12, 246, 111, 23, 196, 73, 236, 216, 67, 31, 45, 164, 118, 123, 183, 204,
		187, 62, 90, 251, 96, 177, 134, 59, 82, 161, 108, 170, 85, 41, 157, 151, 178, 135, 144, 97,
		190, 220, 252, 188, 149, 207, 205, 55, 63, 91, 209, 83, 57, 132, 60, 65, 162, 109, 71, 20,
		42, 158, 93, 86, 242, 211, 171, 68, 17, 146, 217, 35, 32, 46, 137, 180, 124, 184, 38, 119,
		153, 227, 165, 103, 74, 237, 222, 197, 49, 254, 24, 13, 99, 140, 128, 192, 247, 112, 7,
	];
}

public record Share(
	uint16 Id,
	bool Extendable,
	uint8 IterationExponent,
	uint8 GroupIndex,
	uint8 GroupThreshold,
	uint8 GroupCount,
	uint8 MemberIndex,
	uint8 MemberThreshold,
	uint8[] Value)
{
	private static int Bits2Words(int n) => (n + RADIX_BITS - 1) / RADIX_BITS;
	internal const int ID_LENGTH_BITS = 15;
	private const int RADIX_BITS = 10;

	private static int ID_EXP_LENGTH_WORDS =
		Bits2Words(ID_LENGTH_BITS + EXTENDABLE_FLAG_LENGTH_BITS + ITERATION_EXP_LENGTH_BITS);

	private const int EXTENDABLE_FLAG_LENGTH_BITS = 1;
	private const int ITERATION_EXP_LENGTH_BITS = 4;
	private const int CHECKSUM_LENGTH_WORDS = 3;
	private static int GROUP_PREFIX_LENGTH_WORDS = ID_EXP_LENGTH_WORDS + 1;
	private static int METADATA_LENGTH_WORDS = ID_EXP_LENGTH_WORDS + 2 + CHECKSUM_LENGTH_WORDS;
	private static int MIN_MNEMONIC_LENGTH_WORDS = METADATA_LENGTH_WORDS + Bits2Words(Shamir.MIN_STRENGTH_BITS);

	public static Share FromMnemonic(string mnemonic)
	{
		var words = WordList.MnemonicToIndices(mnemonic);

		if (words.Length < MIN_MNEMONIC_LENGTH_WORDS)
		{
			throw new ArgumentException(
				$"Invalid mnemonic length. The length of each mnemonic must be at least {MIN_MNEMONIC_LENGTH_WORDS} words.");
		}

		var prefix = WordsToBytes(words[..(ID_EXP_LENGTH_WORDS + 2)]);
		var prefixReader = new BitStreamReader(prefix);
		var id = prefixReader.ReadUint16(ID_LENGTH_BITS);
		var extendable = prefixReader.ReadUint8(EXTENDABLE_FLAG_LENGTH_BITS) == 1;

		if (Checksum(words, extendable) != 1)
		{
			throw new ArgumentException(
				$"Invalid mnemonic checksum for \"{string.Join(" ", mnemonic.Split().Take(ID_EXP_LENGTH_WORDS + 2))} ...\".");
		}

		var paddingLen = RADIX_BITS * (words.Length - METADATA_LENGTH_WORDS) % 16;
		if (paddingLen > 8)
		{
			throw new ArgumentException("Invalid mnemonic length.");
		}

		var paddedValue = WordsToBytes(words[(ID_EXP_LENGTH_WORDS + 2)..^CHECKSUM_LENGTH_WORDS]);
		var valueReader = new BitStreamReader(paddedValue);
		if (valueReader.Read(paddingLen) != 0)
		{
			throw new ArgumentException("Invalid padding.");
		}

		var value = new List<uint8>();
		while (valueReader.CanRead(8))
		{
			value.Add(valueReader.ReadUint8(8));
		}

		return new Share(
			Id: id,
			Extendable: extendable,
			IterationExponent: prefixReader.ReadUint8(ITERATION_EXP_LENGTH_BITS),
			GroupIndex: prefixReader.ReadUint8(4),
			GroupThreshold: (uint8) (prefixReader.ReadUint8(4) + 1),
			GroupCount: (uint8) (prefixReader.ReadUint8(4) + 1),
			MemberIndex: prefixReader.ReadUint8(4),
			MemberThreshold: (uint8) (prefixReader.ReadUint8(4) + 1),
			Value: value.ToArray()
		);
	}

	public string ToMnemonic(string[] wordlist)
	{
		var prefixWriter = new BitStreamWriter();
		prefixWriter.Write(Id, ID_LENGTH_BITS);
		prefixWriter.Write(Extendable ? 1ul : 0, EXTENDABLE_FLAG_LENGTH_BITS);
		prefixWriter.Write(IterationExponent, ITERATION_EXP_LENGTH_BITS);
		prefixWriter.Write(GroupIndex, 4);
		prefixWriter.Write((uint8) (GroupThreshold - 1), 4);
		prefixWriter.Write((uint8) (GroupCount - 1), 4);
		prefixWriter.Write(MemberIndex, 4);
		prefixWriter.Write((uint8) (MemberThreshold - 1), 4);
		var valueWordCount = (8 * Value.Length + RADIX_BITS - 1) / RADIX_BITS;
		var padding = valueWordCount * RADIX_BITS - Value.Length * 8;

		var valueWriter = new BitStreamWriter();
		valueWriter.Write(0, padding);
		foreach (var b in Value)
		{
			valueWriter.Write(b, 8);
		}

		var bytes = Utils.Concat(prefixWriter.ToByteArray(), valueWriter.ToByteArray());
		var words = Utils.Concat(BytesToWords(bytes), new uint16[] {0, 0, 0});
		var chk = Checksum(words, Extendable) ^ 1;
		var len = words.Length;
		for (var i = 0; i < 3; i++)
		{
			words[len - 3 + i] = (uint16) ((chk >> (RADIX_BITS * (2 - i))) & 1023);
		}

		return string.Join(" ", words.Select(i => wordlist[i]));
	}

	private static uint16[] BytesToWords(uint8[] bytes)
	{
		var words = new List<uint16>();
		var reader = new BitStreamReader(bytes);
		while (reader.CanRead(10))
		{
			words.Add(reader.ReadUint16(10));
		}

		return words.ToArray();
	}

	private static byte[] WordsToBytes(ushort[] words)
	{
		var writer = new BitStreamWriter();

		foreach (var word in words)
		{
			writer.Write(word, 10);
		}

		return writer.ToByteArray();
	}

	private static readonly uint8[] CustomizationStringOrig = "shamir"u8.ToArray();
	private static readonly uint8[] CustomizationStringExtendable = "shamir_extendable"u8.ToArray();

	private static int Checksum(uint16[] values, bool extendable)
	{
		var gen = new[]
		{
			0x00E0E040, 0x01C1C080, 0x03838100, 0x07070200, 0x0E0E0009,
			0x1C0C2412, 0x38086C24, 0x3090FC48, 0x21B1F890, 0x03F3F120,
		};

		var chk = 1;
		var customizationString = extendable ? CustomizationStringExtendable : CustomizationStringOrig;
		foreach (var v in customizationString.Select(x => (uint16) x).Concat(values))
		{
			var b = chk >> 20;
			chk = ((chk & 0xFFFFF) << 10) ^ v;
			for (var i = 0; i < 10; i++)
			{
				chk ^= ((b >> i) & 1) != 0 ? gen[i] : 0;
			}
		}

		return chk;
	}
}

public static class Utils
{
	public static T[] Concat<T>(params T[][] arrays) =>
		arrays.SelectMany(x => x).ToArray();
}
