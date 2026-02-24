using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers;

public static class NBitcoinHelpers
{
	public static void PatchTestNet()
	{
		// This is necessary to force the static members to be initialized
		RuntimeHelpers.RunClassConstructor(typeof(Network).TypeHandle);

		// Access the Bitcoin.Instance
		var bitcoinInstance = Bitcoin.Instance;

		// Get the private field `_Networks` using reflection
		var networksField = bitcoinInstance
			.GetType()
			.GetField("_Networks", BindingFlags.NonPublic | BindingFlags.Instance);

		// Get the internal dictionary
		var networks = networksField!.GetValue(bitcoinInstance) as ConcurrentDictionary<ChainName, Network>;

		var testnet4 = networks![new ChainName("testnet4")];

		// Replaces testnet by testnet4 network
		networks[new ChainName("testnet")] = testnet4;

		var otherAliasesField = typeof(Network)
			.GetField("_OtherAliases", BindingFlags.NonPublic | BindingFlags.Static);

		var otherAliases = otherAliasesField!.GetValue(null) as ConcurrentDictionary<string, Network>;
		otherAliases!["test"] = testnet4;
		otherAliases["testnet"] = testnet4;
	}

	public static ExtPubKey BetterParseExtPubKey(string extPubKeyString)
	{
		extPubKeyString = Guard.NotNullOrEmptyOrWhitespace(nameof(extPubKeyString), extPubKeyString, trim: true);

		ExtPubKey epk;
		try
		{
			epk = ExtPubKey.Parse(extPubKeyString, Network.Main); // Starts with "ExtPubKey": "xpub...
		}
		catch
		{
			try
			{
				epk = ExtPubKey.Parse(extPubKeyString, Network.TestNet); // Starts with "ExtPubKey": "xpub...
			}
			catch
			{
				try
				{
					epk = ExtPubKey.Parse(extPubKeyString, Network.RegTest); // Starts with "ExtPubKey": "xpub...
				}
				catch
				{
					// Try hex, Old wallet format was like this.
					epk = new ExtPubKey(Convert.FromHexString(extPubKeyString)); // Starts with "ExtPubKey": "hexbytes...
				}
			}
		}

		return epk;
	}

	public static BitcoinAddress BetterParseBitcoinAddress(string bitcoinAddressString)
	{
		bitcoinAddressString = Guard.NotNullOrEmptyOrWhitespace(nameof(bitcoinAddressString), bitcoinAddressString, trim: true);

		BitcoinAddress ba;
		try
		{
			ba = BitcoinAddress.Create(bitcoinAddressString, Network.Main);
		}
		catch
		{
			try
			{
				ba = BitcoinAddress.Create(bitcoinAddressString, Network.TestNet);
			}
			catch
			{
				ba = BitcoinAddress.Create(bitcoinAddressString, Network.RegTest);
			}
		}

		return ba;
	}

	public static async Task<AddressManager> LoadAddressManagerFromPeerFileAsync(string filePath, Network? expectedNetwork = null)
	{
		byte[] data, hash;
		using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
		{
			data = new byte[fs.Length - 32];
			await fs.ReadAsync(data.AsMemory(0, data.Length)).ConfigureAwait(false);
			hash = new byte[32];
			await fs.ReadAsync(hash.AsMemory(0, 32)).ConfigureAwait(false);
		}
		var actual = Hashes.DoubleSHA256(data);
		var expected = new uint256(hash);
		if (expected != actual)
		{
			throw new FormatException("Invalid address manager file");
		}

		BitcoinStream stream = new(data) { Type = SerializationType.Disk };
		uint magic = 0;
		stream.ReadWrite(ref magic);
		if (expectedNetwork is { } && expectedNetwork.Magic != magic)
		{
			throw new FormatException("This file is not for the expected network");
		}

		var addrman = new AddressManager();
		addrman.ReadWrite(stream);
		return addrman;
	}

	public static bool TryParseBitcoinAddress(Network network, string queryStr, [NotNullWhen(true)] out BitcoinAddress? address)
	{
		address = null;
		try
		{
			address = BitcoinAddress.Create(queryStr, network);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
