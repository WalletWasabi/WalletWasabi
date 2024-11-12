using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
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
	}
}

public record ScriptPubKey(string Hex);
public record Output(ScriptPubKey ScriptPubKey);
public record Vin( string TxId, int Vout, string Private_Key, Output PrevOut);
public record Given(Vin[] Vin, string[] Recipients);
public record Expected(string[][] Outputs);
public record Sending(Given Given, Expected Expected);

public record SilentPaymentTestVector(string comment, Sending[] Sending)
{

	private static IEnumerable<SilentPaymentTestVector> VectorsData()
	{
		var vectorsJson = File.ReadAllText("./UnitTests/Data/SilentPaymentTestVectors.json");
		var vectors = JsonConvert.DeserializeObject<IEnumerable<SilentPaymentTestVector>>(vectorsJson);
		return vectors;
	}

	private static readonly SilentPaymentTestVector[] TestCases = VectorsData().ToArray();

	public static object[][] TestCasesData =>
		TestCases.Select(testCase => new object[] { testCase }).ToArray();

	public override string ToString() => comment;
}
