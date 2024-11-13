using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicData;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Crypto;
using WalletWasabi.Helpers;
using WalletWasabi.Wallets.SilentPayment;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class SilentPaymentTests
{
	[Theory]
	[MemberData(nameof(SilentPaymentTestVector.TestCasesData), MemberType = typeof(SilentPaymentTestVector))]
	public void TestVectors(SilentPaymentTestVector test)
	{
		foreach (var sending in test.Sending)
		{
			try
			{
				var utxos = sending.Given.Vin.Select(x => new Utxo(
					new OutPoint(uint256.Parse(x.TxId), x.Vout), new Key(Encoders.Hex.DecodeData(x.Private_Key)),
					Script.FromHex(x.PrevOut.ScriptPubKey.Hex))).ToArray();
				var recipients = sending.Given.Recipients.Select(x => SilentPaymentAddress.Parse(x, Network.Main));
				var xonlyPks = SilentPayment.GetPubKeys(recipients, utxos);
				var actual = xonlyPks.SelectMany(x => x.Value).Select(x => Encoders.Hex.EncodeData(x.PubKey.ToBytes()));

				Assert.Subset(sending.Expected.Outputs.SelectMany(x => x).ToHashSet(), actual.ToHashSet());
			}
			catch (ArgumentException e) when(e.Message.Contains("Invalid ec private key") && test.comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}

		foreach (var receiving in test.Receiving)
		{
			var given = receiving.Given;
			var expected = receiving.Expected;
			try
			{
				var utxos = given.Vin.Select(x => (OutPoint.Parse(x.TxId + "-" + x.Vout), ExtractPubKey(x))).ToArray();
				if (!utxos.Any(x => x.Item2 is not null))
				{
					continue;
				}
				var scanKey = ECPrivKey.Create(Encoders.Hex.DecodeData(given.Key_Material.scan_priv_key));
				var spendKey = ECPrivKey.Create(Encoders.Hex.DecodeData(given.Key_Material.spend_priv_key));
				var labels = given.Labels;
				var outputs = given.Outputs.Select(Encoders.Hex.DecodeData).Select(x => ECXOnlyPubKey.Create(x)).ToArray();

				var baseAddress = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());
				var addresses = new List<SilentPaymentAddress> {baseAddress};
				foreach (var label in labels)
				{
					var labelKey = SilentPayment.CreateLabel(scanKey, (uint)label);
					addresses.Add(baseAddress.DeriveAddressForLabel(labelKey));
				}
				var expectedAddresses = expected.Addresses.Select(x => SilentPaymentAddress.Parse(x, Network.Main));

				var sharedSecret = SilentPayment.ComputeSharedSecretReceiver(utxos, scanKey);

				var xonlyPks = SilentPayment.GetPubKeys(addresses.ToArray(), sharedSecret, outputs);
				var all = xonlyPks.Select(x => x.ToBytes()).ToArray();

				Assert.All(
					expected.Outputs.Select(x => x.pub_key),
					expectedPk => Assert.Contains(all, pk => Encoders.Hex.EncodeData(pk) == expectedPk ));
			}
			catch (ArgumentException e) when(e.Message.Contains("Invalid WitScript") && test.comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}
	}

	private GE? ExtractPubKey(ReceivingVin vin)
	{
		var spk = Script.FromHex(vin.PrevOut.ScriptPubKey.Hex);
		if (spk.IsScriptType(ScriptType.Taproot))
		{
			var pubKeyParameters = PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(spk);
			var txInWitnessBytes = Encoders.Hex.DecodeData(vin.TxInWitness);
			var txInWitness = new WitScript(txInWitnessBytes);
			var annex = txInWitnessBytes[^1] == 0x50 ? 1 : 0;
			if (txInWitness.PushCount > annex &&
			    ByteHelpers.CompareFastUnsafe(txInWitness[txInWitness.PushCount - annex - 1][1..33], SilentPayment.NUMS))
			{
				return null;
			}
			return ECXOnlyPubKey.Create(pubKeyParameters.ToBytes()).Q;
		}
		if (spk.IsScriptType(ScriptType.P2WPKH))
		{
			var witScriptParameters =
				PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(new WitScript(vin.TxInWitness));
			return witScriptParameters is { } nonNullWitScriptParameters
				? ECXOnlyPubKey.Create(nonNullWitScriptParameters.PublicKey.GetTaprootFullPubKey().OutputKey.ToBytes()).Q
				: null;
		}
		if (spk.IsScriptType(ScriptType.P2PKH))
		{
			var pk = Script.FromHex(vin.ScriptSig).GetAllPubKeys().First();
			return pk.IsCompressed && pk.GetScriptPubKey(ScriptPubKeyType.Legacy) == spk
				? ECPubKey.Create(pk.ToBytes()).Q
				: null;
		}
		if (spk.IsScriptType(ScriptType.P2SH))
		{
			var scriptSig = new Script(vin.ScriptSig);
			var p2sh = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
			if (p2sh.RedeemScript.IsScriptType(ScriptType.P2WPKH))
			{
				var witScriptParameters =
					PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(new WitScript(vin.TxInWitness));
				return witScriptParameters is { } nonNullWitScriptParameters
					? ECXOnlyPubKey
						.Create(nonNullWitScriptParameters.PublicKey.GetTaprootFullPubKey().OutputKey.ToBytes()).Q
					: null;
			}
		}

		return null;
	}
}

public record ScriptPubKey(string Hex);
public record Output(ScriptPubKey ScriptPubKey);

public record ReceivingExpectedOutput(string priv_key_tweak, string pub_key, string signature);
public record ReceivingVin( string TxId, int Vout, Output PrevOut, string? ScriptSig, string? TxInWitness);
public record SendingVin( string TxId, int Vout, string Private_Key, Output PrevOut);
public record SendingGiven(SendingVin[] Vin, string[] Recipients);

public record KeyMaterial(string spend_priv_key, string scan_priv_key);
public record ReceivingGiven(ReceivingVin[] Vin, string[] Outputs, KeyMaterial Key_Material, int[] Labels);
public record SendingExpected(string[][] Outputs);

public record ReceivingExpected(string[] Addresses, ReceivingExpectedOutput[] Outputs);

public record Sending(SendingGiven Given, SendingExpected Expected);
public record Receiving(ReceivingGiven Given, ReceivingExpected Expected);

public record SilentPaymentTestVector(string comment, Sending[] Sending, Receiving[] Receiving)
{

	private static IEnumerable<SilentPaymentTestVector> VectorsData()
	{
		var vectorsJson = File.ReadAllText("./UnitTests/Data/SilentPaymentTestVectors.json");
		var vectors = JsonConvert.DeserializeObject<IEnumerable<SilentPaymentTestVector>>(vectorsJson);
		return vectors;  //.Where(x => x.comment.Contains("NUMS"));
	}

	private static readonly SilentPaymentTestVector[] TestCases = VectorsData().ToArray();

	public static object[][] TestCasesData =>
		TestCases.Select(testCase => new object[] { testCase }).ToArray();

	public override string ToString() => comment;
}
