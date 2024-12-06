using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using Newtonsoft.Json;
using WalletWasabi.Extensions;
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
				var actual = xonlyPks.SelectMany(x => x.Value).Select(x => Encoders.Hex.EncodeData(x.ToBytes()));

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
				var prevOuts = given.Vin.Select(x => OutPoint.Parse(x.TxId + "-" + x.Vout)).ToArray();
				var pubKeys = given.Vin.Select(ExtractPubKey).DropNulls().ToArray();
				if (!pubKeys.Any())
				{
					continue;
				}
				var scanKey = ECPrivKey.Create(Encoders.Hex.DecodeData(given.Key_Material.scan_priv_key));
				var spendKey = ECPrivKey.Create(Encoders.Hex.DecodeData(given.Key_Material.spend_priv_key));
				var labels = given.Labels;

				var baseAddress = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());
				var addresses = new List<SilentPaymentAddress> {baseAddress};
				foreach (var label in labels)
				{
					var labelKey = SilentPayment.CreateLabel(scanKey, (uint)label);
					addresses.Add(baseAddress.DeriveAddressForLabel(labelKey));
				}
				var expectedAddresses = expected.Addresses.Select(x => SilentPaymentAddress.Parse(x, Network.Main));
				Assert.Equal(expectedAddresses, addresses);

				var sharedSecret = SilentPayment.ComputeSharedSecretReceiver(prevOuts, pubKeys, scanKey);

				var outputs = given.Outputs.Select(Tweak).ToArray();
				var xonlyPks = SilentPayment.GetPubKeys(addresses.ToArray(), sharedSecret, outputs);
				var all = xonlyPks;

				Assert.All(
					expected.Outputs.Select(x => Tweak(x.pub_key)),
					expectedPk => Assert.Contains(all, pk => pk.Q.x == expectedPk.Q.x ));
			}
			catch (InvalidOperationException e) when(e.Message.Contains("infinite") && test.comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}

		ECXOnlyPubKey Tweak(string pk)
		{
			var tripb = TaprootInternalPubKey.Parse(pk);
			var ok = tripb.GetTaprootFullPubKey().OutputKey;
			return ECXOnlyPubKey.Create(ok.ToBytes());
		}
	}


	private GE? ExtractPubKey(ReceivingVin vin)
	{
		var spk = Script.FromHex(vin.PrevOut.ScriptPubKey.Hex);
		var scriptSig = Script.FromHex(vin.ScriptSig);
		var txInWitness = string.IsNullOrEmpty(vin.TxInWitness) ? null :  new WitScript (Encoders.Hex.DecodeData(vin.TxInWitness));
		return SilentPayment.ExtractPubKey(scriptSig, txInWitness, spk);
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
		return vectors; //.Where(x => x.comment.Contains("Multiple outputs with labels: un-labeled and labeled address; same recipient"));
	}

	private static readonly SilentPaymentTestVector[] TestCases = VectorsData().ToArray();

	public static object[][] TestCasesData =>
		TestCases.Select(testCase => new object[] { testCase }).ToArray();

	public override string ToString() => comment;
}
