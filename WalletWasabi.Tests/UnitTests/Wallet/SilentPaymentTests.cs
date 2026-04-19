using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WalletWasabi.Extensions;
using WalletWasabi.Wallets.SilentPayment;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Wallet;

public class SilentPaymentTests
{
	[Theory]
	[MemberData(nameof(TestCasesData))]
	public void TestVectors(SilentPaymentTestVector test)
	{
		// Sending functionality
		foreach (var (given, expected) in test.Sending)
		{
			try
			{
				var utxos = given.Vin.Select(x => new Utxo(
					new OutPoint(uint256.Parse(x.TxId), x.Vout), new Key(Encoders.Hex.DecodeData(x.Private_Key)),
					Script.FromHex(x.PrevOut.ScriptPubKey.Hex))).ToArray();
				var recipients = given.Recipients.Select(x => SilentPaymentAddress.Parse(x, Network.Main));
				var xonlyPks = SilentPayment.GetPubKeys(recipients, utxos);
				var actual = xonlyPks.SelectMany(x => x.Value).Select(x => Encoders.Hex.EncodeData(x.ToBytes()));

				Assert.Subset(expected.Outputs.SelectMany(x => x).ToHashSet(), actual.ToHashSet());
			}
			catch (ArgumentException e) when(e.Message.Contains("Invalid ec private key") && test.Comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}

		// Receiving functionality

		// message and auxiliary data used in signature
		// see: https://github.com/bitcoinops/taproot-workshop/blob/master/1.1-schnorr-signatures.ipynb
		var msg = NBitcoin.Crypto.Hashes.SHA256(Encoders.ASCII.DecodeData("message"));
		var aux = NBitcoin.Crypto.Hashes.SHA256(Encoders.ASCII.DecodeData("random auxiliary data"));

		foreach (var (given, expected) in test.Receiving)
		{
			var (givenInputs, givenOutputs, keyMaterial, labels) = given;
			try
			{
				var prevOuts = givenInputs.Select(x => OutPoint.Parse(x.TxId + "-" + x.Vout)).ToArray();
				var pubKeys = givenInputs.Select(ExtractPubKey).DropNulls().ToArray();
				if (pubKeys.Length == 0)
				{
					continue; // if there are no pubkeys then nothing can be done
				}

				// Parse key material (scan and spend keys)
				using var scanKey = ParsePrivKey(keyMaterial.Scan_priv_key);
				using var spendKey = ParsePrivKey(keyMaterial.Spend_priv_key);

				// Addresses
				var baseAddress = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());

				// Creates a lookup table Dic<SilentPaymentAddress, (ECPrivKey labelSecret, ECPubKey labelPubKey)>
				Func<(LabelInfo LabelInfo, SilentPaymentAddress Address), SilentPaymentAddress> keySelector = x => x.Address;
				var addressesTable = labels
					.Select(label => SilentPayment.CreateLabel(scanKey, (uint) label))
					.Select(labelSecret => new LabelInfo.Full(labelSecret, labelSecret.CreatePubKey()))
					.Select(labelInfo => (LabelInfo: (LabelInfo)labelInfo, Address: baseAddress.DeriveAddressForLabel(labelInfo.PubKey)!)) // each label has a different address
					.Prepend((LabelInfo: new LabelInfo.None(), baseAddress))
					.ToDictionary(keySelector, x => x.LabelInfo);

				var addresses = addressesTable.Keys.ToArray();
				var expectedAddresses = expected.Addresses.Select(x => SilentPaymentAddress.Parse(x, Network.Main));
				Assert.Equal(expectedAddresses, addresses);

				var sharedSecret = SilentPayment.ComputeSharedSecretReceiver(prevOuts, pubKeys, scanKey);

				// Outputs
				var givenOutputPubKeys = givenOutputs.Select(ParseXOnlyPubKey).ToArray();
				var detectedOutputPubKeys = SilentPayment.GetPubKeys(addresses.ToArray(), sharedSecret, givenOutputPubKeys);
				var detectedOutputs = detectedOutputPubKeys.Select(x => Encoders.Hex.EncodeData(x.PubKey.ToBytes())).ToArray();
				var expectedOutputs = expected.Outputs.Select(x => x.Pub_key).ToArray();

				Assert.Equal(detectedOutputs.ToHashSet(), expectedOutputs.ToHashSet());

				// Tweak Key

				// Enrich the detected output xonlypubkeys with corresponding label info (secret and public key)
				var detectedXonlyWithLabelInfo = detectedOutputPubKeys
					.Select(x => (x.Address, x.PubKey, LabelInfo: addressesTable[x.Address]))
					.ToArray();

				// Compute tweakKey for each detected output
				var tweakKeys = detectedXonlyWithLabelInfo
					.Select((pk, k) => {
						var tk = SilentPayment.TweakKey(sharedSecret, (uint) k);
						return pk.LabelInfo switch
						{
							LabelInfo.None => (pk.PubKey, TweakKey: tk),
							LabelInfo.Full info => (pk.PubKey, TweakKey: tk.TweakAdd(info.Secret.sec.ToBytes())),
							_ => throw new ArgumentException("Unknown label type")
						};
					})
					.ToArray();

				var detectedTweakKeys = tweakKeys.Select(x => Encoders.Hex.EncodeData(x.TweakKey.sec.ToBytes()));
				var expectedTweakKeys = expected.Outputs.Select(o => o.Priv_key_tweak);

				Assert.Equal(expectedTweakKeys.ToHashSet(), detectedTweakKeys.ToHashSet());

				// Signature
				var expectedSignature = expected.Outputs.Select(o => o.Signature).ToHashSet();
				var tweakKeyMap = tweakKeys.ToDictionary(x => x.PubKey, x => x.TweakKey);
				var computedSignatures = detectedOutputPubKeys
					.Select(x => (x.Address, x.PubKey, TweakKey: tweakKeyMap[x.PubKey]))
					.Select(x => SilentPayment.ComputePrivKey(spendKey, x.TweakKey))
					.Select(x => x.SignBIP340(msg, aux))
					.Select(x => Encoders.Hex.EncodeData(x.ToBytes()))
					.ToArray();

				Assert.Equal(expectedSignature.ToHashSet(), computedSignatures.ToHashSet());
			}
			catch (InvalidOperationException e) when(e.Message.Contains("infinite") && test.Comment.Contains("point at infinity"))
			{
				// ignore because it is expected to fail;
			}
		}

		ECXOnlyPubKey ParseXOnlyPubKey(string pk) =>
			ECXOnlyPubKey.Create(Encoders.Hex.DecodeData(pk));

		ECPrivKey ParsePrivKey(string pk) =>
			ECPrivKey.Create(Encoders.Hex.DecodeData(pk));
	}


	private GE? ExtractPubKey(ReceivingVin vin)
	{
		var spk = Script.FromHex(vin.PrevOut.ScriptPubKey.Hex);
		var scriptSig = Script.FromHex(vin.ScriptSig!);
		var txInWitness = string.IsNullOrEmpty(vin.TxInWitness) ? null : new WitScript(Encoders.Hex.DecodeData(vin.TxInWitness));
		return SilentPayment.ExtractPubKey(scriptSig, txInWitness!, spk);
	}

	public static TheoryData<SilentPaymentTestVector> TestCasesData
	{
		get
		{
			var json = File.ReadAllText("./UnitTests/Data/SilentPaymentTestVectors.json");
			var vectors = JsonSerializer.Deserialize<SilentPaymentTestVector[]>(json);
			Assert.NotNull(vectors);

			var theoryData = new TheoryData<SilentPaymentTestVector>();
			theoryData.AddRange(vectors);

			return theoryData;
		}
	}
}

public record ScriptPubKey([property: JsonPropertyName("hex")] string Hex);
public record Output([property: JsonPropertyName("scriptPubKey")] ScriptPubKey ScriptPubKey);

public record ReceivingExpectedOutput(
	[property: JsonPropertyName("priv_key_tweak")] string Priv_key_tweak,
	[property: JsonPropertyName("pub_key")] string Pub_key,
	[property: JsonPropertyName("signature")] string Signature);
public record ReceivingVin(
	[property: JsonPropertyName("txid")] string TxId,
	[property: JsonPropertyName("vout")] int Vout,
	[property: JsonPropertyName("prevout")] Output PrevOut,
	[property: JsonPropertyName("scriptSig")] string? ScriptSig,
	[property: JsonPropertyName("txinwitness")] string? TxInWitness);
public record SendingVin(
	[property: JsonPropertyName("txid")] string TxId,
	[property: JsonPropertyName("vout")] int Vout,
	[property: JsonPropertyName("private_key")] string Private_Key,
	[property: JsonPropertyName("prevout")] Output PrevOut);
public record SendingGiven(
	[property: JsonPropertyName("vin")] SendingVin[] Vin,
	[property: JsonPropertyName("recipients")] string[] Recipients);

public record KeyMaterial(
	[property: JsonPropertyName("spend_priv_key")] string Spend_priv_key,
	[property: JsonPropertyName("scan_priv_key")] string Scan_priv_key);
public record ReceivingGiven(
	[property: JsonPropertyName("vin")] ReceivingVin[] Vin,
	[property: JsonPropertyName("outputs")] string[] Outputs,
	[property: JsonPropertyName("key_material")] KeyMaterial Key_Material,
	[property: JsonPropertyName("labels")] int[] Labels);
public record SendingExpected([property: JsonPropertyName("outputs")] string[][] Outputs);

public record ReceivingExpected(
	[property: JsonPropertyName("addresses")] string[] Addresses,
	[property: JsonPropertyName("outputs")] ReceivingExpectedOutput[] Outputs);

public record Sending(
	[property: JsonPropertyName("given")] SendingGiven Given,
	[property: JsonPropertyName("expected")] SendingExpected Expected);
public record Receiving(
	[property: JsonPropertyName("given")] ReceivingGiven Given,
	[property: JsonPropertyName("expected")] ReceivingExpected Expected);

public record SilentPaymentTestVector(
	[property: JsonPropertyName("comment")] string Comment,
	[property: JsonPropertyName("sending")] Sending[] Sending,
	[property: JsonPropertyName("receiving")] Receiving[] Receiving)
{
	public override string ToString() => Comment;
}

public abstract record LabelInfo
{
	public record Full(ECPrivKey Secret, ECPubKey PubKey) : LabelInfo;

	public record None : LabelInfo;
}
