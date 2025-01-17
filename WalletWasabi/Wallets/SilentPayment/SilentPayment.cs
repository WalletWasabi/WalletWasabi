using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wallets.SilentPayment;

public static class SilentPayment
{
	private static readonly byte[] NUMS =
		Encoders.Hex.DecodeData("50929b74c1a04954b78b4b6035e97a5e078a5a0f28ec96d547bfee9ace803ac0");

	public static ECPubKey ComputeSharedSecretReceiver(OutPoint[] prevOuts, GE[] pubKeys, ECPrivKey b) =>
		ComputeSharedSecret(prevOuts, A: SumPublicKeys(pubKeys), b);

	public static ECPubKey ComputeSharedSecretReceiver(ECPubKey tweakData, ECPrivKey b) =>
		new(tweakData.GetSharedPubkey(b).Q, null);

	public static ECPubKey TweakData(OutPoint[] inputs, GE[] pubKeys) =>
		TweakData(inputs, SumPublicKeys(pubKeys));

	public static ECPubKey TweakData(Utxo[] utxos) =>
		TweakData(utxos.Select(x => x.OutPoint).ToArray(), SumPrivateKeys(utxos).CreatePubKey());

	public static Dictionary<SilentPaymentAddress, ECXOnlyPubKey[]> GetPubKeys(IEnumerable<SilentPaymentAddress> recipients, Utxo[] utxos) =>
		recipients
			.GroupBy(x => x.ScanKey, (scanKey, addresses) => {
				var sharedSecret = ComputeSharedSecretSender(utxos, scanKey);
				return addresses.Select((addr, k) => (
					Address: addr,
					PubKey: ComputePubKey(addr.SpendKey, sharedSecret, (uint) k)));
			})
			.SelectMany(x => x)
			.GroupBy(x => x.Address)
			.ToDictionary(x => x.Key, x => x.Select(y => y.PubKey).ToArray());

	public static (SilentPaymentAddress Address, ECXOnlyPubKey PubKey)[] GetPubKeys(IEnumerable<SilentPaymentAddress> addresses,
		ECPubKey sharedSecret, ECXOnlyPubKey[] outputs) =>
		Enumerable
			.Range(0, outputs.Length)
			.Select(n => addresses.Select(address =>
				(Address: address, PubKey: ComputePubKey(address.SpendKey, sharedSecret, (uint) n))))
			.SelectMany(x => x)
			.Where(x => outputs.Select(o => o.Q).Contains(x.PubKey.Q))
			.ToArray();

	public static Dictionary<SilentPaymentAddress, Script[]> ExtractSilentPaymentScriptPubKeys(SilentPaymentAddress[] addresses, ECPubKey tweakData, Transaction tx, ECPrivKey scanKey)
	{
		if (!IsElegible(tx))
		{
			return [];
		}

		var taprootPubKeys = tx.Outputs
			.Where(x => x.ScriptPubKey.IsScriptType(ScriptType.Taproot))
			.Select(x => PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(x.ScriptPubKey))
			.DropNulls()
			.Select(x => ECXOnlyPubKey.Create(x.ToBytes()))
			.ToArray();
		var sharedSecret = ComputeSharedSecretReceiver(tweakData, scanKey);
		var silentPaymentOutputs = GetPubKeys(addresses, sharedSecret, taprootPubKeys).GroupBy(x => x.Address);
		return silentPaymentOutputs.ToDictionary(x => x.Key, x => x.Select(y => new TaprootPubKey(y.PubKey.ToBytes()).ScriptPubKey).ToArray());
	}

	private static bool IsElegible(Transaction tx) =>
		tx.Outputs.Any(x => x.ScriptPubKey.IsScriptType(ScriptType.Taproot));

	public static ECPrivKey CreateLabel(ECPrivKey scanKey, uint label) =>
		ECPrivKey.Create(
			TaggedHash(
				"BIP0352/Label",
				ByteHelpers.Combine(scanKey.sec.ToBytes(), Serialize32(label))));

	// Let ecdh_shared_secret = input_hash·a·Bscan
	private static ECPubKey ComputeSharedSecret(OutPoint[] outpoints, ECPrivKey a, ECPubKey B) =>
		DHSharedSecret(InputHash(outpoints, a.CreatePubKey()), B, a);

	// Let ecdh_shared_secret = input_hash·bscan·A
	private static ECPubKey ComputeSharedSecret(OutPoint[] outpoints, ECPubKey A, ECPrivKey b) =>
		DHSharedSecret(InputHash(outpoints, A), A, b);

	private static ECPubKey DHSharedSecret(Scalar inputHash, ECPubKey pubKey, ECPrivKey privKey) =>
		new(TweakData(inputHash, pubKey).GetSharedPubkey(privKey).Q, null);

	private static ECPubKey TweakData(OutPoint[] inputs, ECPubKey A) =>
		TweakData(InputHash(inputs, A), A);

	private static ECPubKey TweakData(Scalar inputHash, ECPubKey pubKey) =>
		new ECPubKey((inputHash * pubKey.Q).ToGroupElement(), null);

	// let tk = hash_BIP0352/SharedSecret(serP(ecdh_shared_secret) || ser32(k))
	public static ECPrivKey TweakKey(ECPubKey sharedSecret, uint k) =>
		ECPrivKey.Create(
			TaggedHash(
				"BIP0352/SharedSecret",
				ByteHelpers.Combine(sharedSecret.ToBytes(), Serialize32(k))));

	public static ECPubKey ComputeSharedSecretSender(Utxo[] utxos, ECPubKey B)
	{
		using var a = SumPrivateKeys(utxos);
		return ComputeSharedSecret(utxos.Select(x => x.OutPoint).ToArray(), a, B);
	}

	// Let input_hash = hashBIP0352/Inputs(outpointL || A)
	private static Scalar InputHash(OutPoint[] outpoints, ECPubKey A)
	{
		var outpointL = outpoints.Select(x => x.ToBytes()).Order(BytesComparer.Instance).First();
		var hash = TaggedHash("BIP0352/Inputs", ByteHelpers.Combine(outpointL, A.ToBytes()));
		return new Scalar(hash);
	}

	public static ECPrivKey ComputePrivKey(ECPrivKey spendKey, ECPubKey sharedSecret, uint k)
	{
		using var tweakKey = TweakKey(sharedSecret, k);
		return ComputePrivKey(spendKey, tweakKey);
	}

	public static ECPrivKey ComputePrivKey(ECPrivKey spendKey, ECPrivKey tweakKey) =>
		ECPrivKey.Create((tweakKey.sec + spendKey.sec).ToBytes());

	public static GE? ExtractPubKey(Script scriptSig, WitScript txInWitness, Script prevOutScriptPubKey)
	{
		var spk = prevOutScriptPubKey;
		if (txInWitness != WitScript.Empty && spk.IsScriptType(ScriptType.Taproot))
		{
			var pubKeyParameters = PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(spk);
			var annex = txInWitness[txInWitness.PushCount -1][^1] == 0x50 ? 1 : 0;
			if (txInWitness.PushCount > annex &&
			    ByteHelpers.CompareFastUnsafe(txInWitness[txInWitness.PushCount - annex - 1][1..33], NUMS))
			{
				return null;
			}
			return ECXOnlyPubKey.Create(pubKeyParameters.ToBytes()).Q;
		}
		if (txInWitness != WitScript.Empty && spk.IsScriptType(ScriptType.P2WPKH))
		{
			var witScriptParameters =
				PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(txInWitness);
			if (witScriptParameters is { } nonNullWitScriptParameters && nonNullWitScriptParameters.PublicKey.IsCompressed)
			{
				var q = ECPubKey.Create(nonNullWitScriptParameters.PublicKey.ToBytes()).ToXOnlyPubKey().Q;
				return nonNullWitScriptParameters.PublicKey.ToBytes()[0] == 0x02 ? q : q.Negate();
			}
		}
		if (scriptSig != Script.Empty && spk.IsScriptType(ScriptType.P2PKH))
		{
			var pubKeyId = PayToPubkeyHashTemplate.Instance.ExtractScriptPubKeyParameters(spk);
			var pubKeyHash = new uint160(pubKeyId.ToBytes());

			var scriptSigBytes = scriptSig.ToBytes();
			var pubKeyBytes = Enumerable.Range(33, scriptSigBytes.Length-32)
				.Reverse()
				.Select(i => scriptSigBytes[(i-33)..i])
				.Where(pkb => pkb[0] is 2 or 3 or 4)
				.FirstOrDefault(pkb => Hashes.Hash160(pkb)== pubKeyHash);

			return pubKeyBytes != null
				? ECPubKey.Create(pubKeyBytes).Q
				: null;
		}
		if (scriptSig != Script.Empty && spk.IsScriptType(ScriptType.P2SH))
		{
			var p2sh = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
			if (txInWitness != WitScript.Empty && p2sh is not null && p2sh.RedeemScript.IsScriptType(ScriptType.P2WPKH))
			{
				var witScriptParameters =
					PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(txInWitness);
				if (witScriptParameters is {PublicKey.IsCompressed: true})
				{
					var q = ECPubKey.Create(witScriptParameters.PublicKey.ToBytes()).ToXOnlyPubKey().Q;
					return witScriptParameters.PublicKey.ToBytes()[0] == 0x02 ? q : q.Negate();
				}
			}
		}

		return null;
	}

	internal static ECXOnlyPubKey ComputePubKey(ECPubKey Bm, ECPubKey sharedSecret, uint k)
	{
		using var tk = TweakKey(sharedSecret, k);

		// Let Pmk = k·G + Bm
		var pmk = tk.CreatePubKey().Q.ToGroupElementJacobian() + Bm.Q;
		return new ECPubKey(pmk.ToGroupElement(), null).ToXOnlyPubKey();
	}

	private static ECPrivKey SumPrivateKeys(IEnumerable<Utxo> utxos)
	{
		var sum = utxos
			.Select(x => NegateKey(x.SigningKey, x.ScriptPubKey.IsScriptType(ScriptType.Taproot)))
			.Aggregate(Scalar.Zero, (acc, key) => acc.Add(key.sec));

		return ECPrivKey.Create(sum.ToBytes());

		ECPrivKey NegateKey(Key key, bool isTaproot)
		{
			var pk = ECPrivKey.Create(key.ToBytes());
			pk.CreateXOnlyPubKey(out var parity);
			return isTaproot && parity ? ECPrivKey.Create(pk.sec.Negate().ToBytes()) : pk;
		}
	}

	// Let A = A1 + A2 + ... + An
	private static ECPubKey SumPublicKeys(IEnumerable<GE> pubKeys) =>
		new(pubKeys.Aggregate(GEJ.Infinity, (acc, key) => acc + key).ToGroupElement(), null);

	private static byte[] TaggedHash(string tag, byte[] data)
	{
		var tagHash = Hashes.SHA256(Encoding.UTF8.GetBytes(tag));
		var concat = ByteHelpers.Combine(tagHash, tagHash, data);
		return Hashes.SHA256(concat);
	}

	private static byte[] Serialize32(uint i)
	{
		var result = new byte[4];
		BitConverter.GetBytes(i).CopyTo(result, 0);
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(result);
		}
		return result;
	}
}

public record Utxo(OutPoint OutPoint, Key SigningKey, Script ScriptPubKey);
