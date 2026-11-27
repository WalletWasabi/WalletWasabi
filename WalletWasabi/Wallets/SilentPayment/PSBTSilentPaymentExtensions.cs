using System.Diagnostics.CodeAnalysis;
using NBitcoin;
using NBitcoin.Secp256k1;

namespace WalletWasabi.Wallets.SilentPayment;

/// <summary>
/// PSBT extensions for BIP-375 Silent Payment support.
/// Provides methods to attach and retrieve silent payment information from PSBT outputs,
/// enabling hardware wallets and external signers to compute silent payment output scripts.
/// </summary>
public static class PSBTSilentPaymentExtensions
{
	/// <summary>
	/// BIP-375 PSBT output key type for silent payment v0 info.
	/// Value contains: 33-byte scan_key || 33-byte spend_key
	/// </summary>
	private const byte PSBT_OUT_SP_V0_INFO = 0x09;

	private static readonly byte[] SpV0InfoKey = [PSBT_OUT_SP_V0_INFO];

	/// <summary>
	/// Sets the silent payment address info on a PSBT output.
	/// This allows signers to compute the actual taproot output script using BIP-352.
	/// </summary>
	/// <param name="output">The PSBT output to annotate.</param>
	/// <param name="address">The silent payment address containing scan and spend keys.</param>
	public static void SetSilentPaymentInfo(this PSBTOutput output, SilentPaymentAddress address)
	{
		var value = new byte[66];
		Buffer.BlockCopy(address.ScanKey.ToBytes(), 0, value, 0, 33);
		Buffer.BlockCopy(address.SpendKey.ToBytes(), 0, value, 33, 33);
		output.Unknown[SpV0InfoKey] = value;
	}

	/// <summary>
	/// Gets the silent payment address info from a PSBT output, if present.
	/// </summary>
	/// <param name="output">The PSBT output to read from.</param>
	/// <returns>The silent payment address if the output has SP info, null otherwise.</returns>
	public static SilentPaymentAddress? GetSilentPaymentInfo(this PSBTOutput output)
	{
		if (!output.Unknown.TryGetValue(SpV0InfoKey, out var value) || value.Length != 66)
		{
			return null;
		}

		return new SilentPaymentAddress(
			Version: 0,
			ScanKey: ECPubKey.Create(value[..33]),
			SpendKey: ECPubKey.Create(value[33..]));
	}

	/// <summary>
	/// Checks if the PSBT output contains silent payment info.
	/// </summary>
	public static bool HasSilentPaymentInfo(this PSBTOutput output) =>
		output.Unknown.ContainsKey(SpV0InfoKey);

	/// <summary>
	/// Removes silent payment info from the PSBT output.
	/// Called after the output script has been computed and set.
	/// </summary>
	public static void ClearSilentPaymentInfo(this PSBTOutput output) =>
		output.Unknown.Remove(SpV0InfoKey);

	/// <summary>
	/// Gets all PSBT outputs that have silent payment info attached.
	/// </summary>
	/// <param name="psbt">The PSBT to scan.</param>
	/// <returns>Enumerable of (output index, PSBT output, silent payment address) tuples.</returns>
	public static IEnumerable<(int Index, PSBTOutput Output, SilentPaymentAddress Address)> GetSilentPaymentOutputs(this PSBT psbt) =>
		psbt.Outputs
			.Select((output, index) => (Index: index, Output: output, Address: output.GetSilentPaymentInfo()))
			.Where(x => x.Address is not null)
			.Select(x => (x.Index, x.Output, x.Address!));

	/// <summary>
	/// Checks if the PSBT has any outputs with silent payment info.
	/// </summary>
	public static bool HasSilentPaymentOutputs(this PSBT psbt) =>
		psbt.Outputs.Any(o => o.HasSilentPaymentInfo());

	/// <summary>
	/// Resolves all silent payment outputs in the PSBT by computing the actual taproot scripts.
	/// This should be called by signers that have access to the private keys for the inputs.
	/// Modifies the PSBT in place and preserves PSBT version and settings.
	/// </summary>
	/// <param name="psbt">The PSBT containing silent payment outputs.</param>
	/// <param name="getKeyForInput">
	/// Function that returns the signing key for a given input's script pubkey.
	/// Must return a key for all inputs; throws if any key is missing.
	/// </param>
	/// <returns>The same PSBT with silent payment outputs resolved to actual taproot scripts.</returns>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "NBitcoin Key class is not IDisposable")]
	public static PSBT ResolveSilentPaymentOutputs(this PSBT psbt, Func<Script, Key?> getKeyForInput)
	{
		var spOutputs = psbt.GetSilentPaymentOutputs().ToList();
		if (spOutputs.Count == 0)
		{
			return psbt;
		}

		// Collect input keys and build Utxo array for ECDH computation
		var utxos = new List<Utxo>();
		foreach (var input in psbt.Inputs)
		{
			var txOut = input.GetTxOut()
				?? throw new InvalidOperationException($"Input {input.PrevOut} has no UTXO information.");

			var key = getKeyForInput(txOut.ScriptPubKey)
				?? throw new InvalidOperationException($"No key for input {input.PrevOut}. Silent payment outputs require keys for all inputs.");

			// Apply taproot tweak if needed (Key is not IDisposable, CA2000 is a false positive)
			var signingKey = txOut.ScriptPubKey.IsScriptType(ScriptType.Taproot)
				? key.Tweak()
				: key;

			utxos.Add(new Utxo(input.PrevOut, signingKey, txOut.ScriptPubKey));
		}

		var spAddresses = spOutputs.Select(x => x.Address).ToArray();
		var derivedScripts = SilentPayment.GetPubKeys(spAddresses, utxos.ToArray());

		// Track which derived key to use for each address (supports multiple outputs to same address)
		var addressKeyIndex = spAddresses.Distinct().ToDictionary(a => a, _ => 0);

		// Modify PSBT outputs in place - preserves PSBT version and settings
		foreach (var (_, output, address) in spOutputs)
		{
			var keys = derivedScripts[address];
			var keyIndex = addressKeyIndex[address];

			if (keyIndex >= keys.Length)
			{
				throw new InvalidOperationException($"Not enough derived keys for silent payment address. Expected at least {keyIndex + 1}, got {keys.Length}.");
			}

			output.ScriptPubKey = new TaprootPubKey(keys[keyIndex].ToBytes()).ScriptPubKey;
			output.ClearSilentPaymentInfo();
			addressKeyIndex[address] = keyIndex + 1;
		}

		return psbt;
	}
}
