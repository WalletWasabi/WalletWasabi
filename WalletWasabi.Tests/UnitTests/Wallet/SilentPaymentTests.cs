using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Blockchain.Keys;
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
					Script.FromHex(x.PrevOut.ScriptPubKey.Hex)));
				var partialSecret = SilentPayment.ComputePartialSecret(utxos);
				var xonlyPks = SilentPayment.GetPubKeys(sending.Given.Recipients, partialSecret, Network.Main);
				var actual = xonlyPks.SelectMany(x => x.Value).Select(x => Encoders.Hex.EncodeData(x.PubKey.ToBytes()));
				// the outputs are ordered by amount but we don't handle them, that's why we ignore the order.
				Assert.Subset(sending.Expected.Outputs.SelectMany(x => x).ToHashSet(), actual.ToHashSet());
			}
			catch (ArgumentException e) when(e.Message.Contains("Invalid ec private key") && test.comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}

		/*
		foreach (var receiving in test.Receiving)
		{
			try
			{
				var utxos = receiving.Given.Vin.Select(x => (OutPoint.Parse(x.TxId + "-" + x.Vout), ExtractPubKey(x)));
				var partialSecret = SilentPayment.TweakData(utxos);
				var scanKey = new Scalar(Encoders.Hex.DecodeData(receiving.Given.Key_Material.scan_priv_key));
				var spendKey = new Scalar(Encoders.Hex.DecodeData(receiving.Given.Key_Material.spend_priv_key));
				var tweakKey = new ECPubKey((scanKey * partialSecret.Q).ToGroupElement(), null);
				var xonlyPks = SilentPayment.ComputeScriptPubKey(SilentPaymentAddress.Parse(receiving.Expected.Addresses[0], Network.Main), partialSecret, Network.Main);
			}
			catch (ArgumentException e) when(e.Message.Contains("Invalid ec private key") && test.comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}
		*/
	}

	private ECXOnlyPubKey ExtractPubKey(ReceivingVin vin)
	{
		var spk = Script.FromHex(vin.PrevOut.ScriptPubKey.Hex);
		if (spk.IsScriptType(ScriptType.Taproot))
		{
			return ECXOnlyPubKey.Create(PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(spk).ToBytes());
		}
		if (spk.IsScriptType(ScriptType.P2WPKH))
		{
			return ECXOnlyPubKey.Create(
				PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(new WitScript(vin.TxInWitness)).PublicKey
					.GetTaprootFullPubKey().OutputKey.ToBytes());
		}

		throw new InvalidDataException("wtf");
	}
}

public record ScriptPubKey(string Hex);
public record Output(ScriptPubKey ScriptPubKey);

public record ReceivingExpectedOutput(string priv_key_tweak, string pub_key, string signature);
public record ReceivingVin( string TxId, int Vout, Output PrevOut, string? ScriptSig, string? TxInWitness);
public record SendingVin( string TxId, int Vout, string Private_Key, Output PrevOut);
public record SendingGiven(SendingVin[] Vin, string[] Recipients);

public record KeyMaterial(string spend_priv_key, string scan_priv_key);
public record ReceivingGiven(ReceivingVin[] Vin, string[] Outputs, KeyMaterial Key_Material);
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
		return vectors; //.Where(x => x.comment == "Single recipient: taproot only inputs with even y-values");
	}

	private static readonly SilentPaymentTestVector[] TestCases = VectorsData().ToArray();

	public static object[][] TestCasesData =>
		TestCases.Select(testCase => new object[] { testCase }).ToArray();

	public override string ToString() => comment;
}
