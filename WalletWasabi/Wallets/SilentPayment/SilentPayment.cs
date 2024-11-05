using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Secp256k1;
using WabiSabi.Helpers;

namespace WalletWasabi.Wallets.SilentPayment;

public class SilentPayment
{
	public static ECPrivKey ComputePartialSecret(IEnumerable<Utxo> utxos)
	{
		using var a = SumPrivkeys(utxos);
		var outpoints = utxos.Select(x => x.OutPoint);

		var firstInput = outpoints.Select(x => x.ToBytes()).Order(BytesComparer.Instance).First();
		var outpointHash = TaggedHash("BIP0352/Inputs", ByteHelpers.Combine(firstInput, a.CreatePubKey().ToBytes()));
		return ECPrivKey.Create((a.sec * new Scalar(outpointHash)).ToBytes());
	}

	public static Dictionary<SilentPaymentAddress, SilentPaymentPubKey[]> GetPubKeys(IEnumerable<string> recipients, ECPrivKey partialSecret, Network network)
	{
		return recipients
			.Select(ParseAddress)
			.GroupBy(x => x.ScanKey, (_, addresses) => addresses.Select(ComputePubKey))
			.SelectMany(x => x)
			.GroupBy(x => x.Address)
			.ToDictionary(x => x.Key, x => x.ToArray());

		SilentPaymentAddress ParseAddress(string address) => SilentPaymentAddress.Parse(address, network);
		SilentPaymentPubKey ComputePubKey(SilentPaymentAddress addr, int i)
		{
			var pubkey = SilentPayment.ComputePubKey(addr, partialSecret, i).ToXOnlyPubKey();
			return new SilentPaymentPubKey(pubkey, addr);
		}
	}

	public static Script ComputeScriptPubKey(SilentPaymentAddress addr, ECPrivKey partialSecret, int i)
	{
		var pubkey = ComputePubKey(addr, partialSecret, i);
		return new TaprootPubKey(pubkey.ToXOnlyPubKey().ToBytes()).ScriptPubKey;
	}

	public static ECPrivKey ComputePrivKey(SilentPaymentAddress addr, ECPrivKey spendKey, ECPrivKey partialSecret, int i)
	{
		var sharedPubkey = addr.ScanKey.GetSharedPubkey(partialSecret);
		using var k = ECPrivKey.Create(TaggedHash("BIP0352/SharedSecret", ByteHelpers.Combine(sharedPubkey.ToBytes(), Serialize32(i))));
		return ECPrivKey.Create((k.sec + spendKey.sec).ToBytes());
	}

	private static ECPubKey ComputePubKey(SilentPaymentAddress addr, ECPrivKey partialSecret, int i)
	{
		var sharedPubkey = addr.ScanKey.GetSharedPubkey(partialSecret);
		using var k = ECPrivKey.Create(TaggedHash("BIP0352/SharedSecret", ByteHelpers.Combine(sharedPubkey.ToBytes(), Serialize32(i))));

		// Let Pmk = k·G + Bm
		var pmk = k.CreatePubKey().Q.ToGroupElementJacobian() + addr.SpendKey.Q;
		return new ECPubKey(pmk.ToGroupElement(), null);
	}

	private static ECPrivKey SumPrivkeys(IEnumerable<Utxo> utxos)
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

	private static byte[] TaggedHash(string tag, byte[] data)
	{
		var tagHash = Hashes.SHA256(Encoding.UTF8.GetBytes(tag));
		var concat = ByteHelpers.Combine(tagHash, tagHash, data);
		return Hashes.SHA256(concat);
	}

	private static byte[] Serialize32(int i)
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
public record SilentPaymentPubKey(ECXOnlyPubKey PubKey, SilentPaymentAddress Address);
