using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Protocol;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WalletWasabi.Helpers;

public static class NBitcoinHelpers
{
	/// <exception cref="InvalidOperationException">If valid output value cannot be created with the given parameters.</exception>
	/// <returns>Sum of outputs' values. Sum of inputs' values - the calculated fee.</returns>
	public static Money TakeFee(IEnumerable<Coin> inputs, int outputCount, Money feePerInputs, Money feePerOutputs)
	{
		var inputValue = inputs.Sum(coin => coin.TxOut.Value);
		var fee = (inputs.Count() * feePerInputs) + (outputCount * feePerOutputs);
		Money outputSum = inputValue - fee;
		if (outputSum < Money.Zero)
		{
			throw new InvalidOperationException($"{nameof(outputSum)} cannot be negative.");
		}
		return outputSum;
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
