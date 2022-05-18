using System.Collections.Generic;
using System.Collections.Immutable;
using NBitcoin;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;
using WalletWasabi.WabiSabiClientLibrary.Crypto;
using WalletWasabi.WabiSabiClientLibrary.Models;
using WalletWasabi.WabiSabiClientLibrary.Models.SelectInputsForRound;
using Xunit;

namespace WalletWasabi.WabiSabiClientLibrary.Tests.UnitTests.Controllers.Helpers;

public class SelectInputsForRoundHelperTests
{
	[Fact]
	public void NoUtxoTest()
	{
		Utxo[] utxos = Array.Empty<Utxo>();

		SelectInputsForRoundRequest request = MakeRequest(utxos);
		SelectInputsForRoundResponse response = SelectInputsForRoundHelper.SelectInputsForRound(request, new DeterministicRandom(0));
		Assert.Empty(response.Indices);
	}

	[Fact]
	public void SingleNonPrivateUtxoTest()
	{
		Utxo[] utxos = new Utxo[]
		{
			// Not allowed script type.
			new(new OutPoint(new uint256("06d0e3ae26dc6a98da5ea16d19eb6ad2817aab3f510d91c13de2ea9457124258"), 0), Amount: Money.Coins(0.02m), "76a914ce926a6e50d2f12dd1fe3bda4521df3a13bb4bcb88ac", AnonymitySet: 10),

			// Anonymity set is higher than target 50. Might and might not be in the result.
			new(new OutPoint(new uint256("f35481573468b5e4f4a4fce6afb2c3efb5e7f9b18ad5413e45ce07a1de315d7c"), 0), Amount: Money.Coins(0.02m), "0014a16b5bb7788c6902f07c1049ca23d607116e7fb7", AnonymitySet: 60),

			// 0.009 is not sufficient amount, minimum allowed value is 0.01.
			new(new OutPoint(new uint256("dfb38af06d063128af9c4483bf944cc38c6608749cc145be2b9912ef7e185450"), 0), Amount: Money.Coins(0.009m), "0014c868304afd51a19aa4352ec81b1765f9e73aedd1", AnonymitySet: 10),

			// Ok.
			new(new OutPoint(new uint256("6a8cb2d81062ef93ae5d58b5cbe78d5fc5159f609e0d06f767d2f8eae5ead907"), 0), Amount: Money.Coins(0.015m),  "00145e6ebd498b8c999ca5843baf7f04e9a43c935932", AnonymitySet: 10),
		};

		SelectInputsForRoundRequest request = MakeRequest(utxos);
		SelectInputsForRoundResponse response = SelectInputsForRoundHelper.SelectInputsForRound(request, new DeterministicRandom(0));

		Array.Sort(response.Indices);

		// The first possible result and the second possible result.
		if (response.Indices.Length == 1)
		{
			Assert.Equal(ImmutableArray.Create(3), response.Indices);
		}
		else
		{
			Assert.Equal(ImmutableArray.Create(1, 3), response.Indices);
		}
	}

	[Fact]
	public void TwoUtxoWithHighAnonScoreTargetTest()
	{
		Utxo[] utxos = new Utxo[]
		{
			new(new OutPoint(new uint256("dfb38af06d063128af9c4483bf944cc38c6608749cc145be2b9912ef7e185450"), 0), Amount: Money.Coins(0.01m), "001491c6a44db2d01e66736b38205ed3003e572d41a3", AnonymitySet: 90),
			new(new OutPoint(new uint256("f35481573468b5e4f4a4fce6afb2c3efb5e7f9b18ad5413e45ce07a1de315d7c"), 1), Amount: Money.Coins(0.01m), "001424574dca4f45fe4bac6e91383ca357c387f25732", AnonymitySet: 90),
		};

		SelectInputsForRoundRequest request = MakeRequest(utxos);
		SelectInputsForRoundResponse response = SelectInputsForRoundHelper.SelectInputsForRound(request, new DeterministicRandom(0));
		Assert.Empty(response.Indices);
	}

	private static SelectInputsForRoundRequest MakeRequest(Utxo[] utxos)
		=> new(
			Utxos: utxos,
			AnonScoreTarget: 50,
			AllowedInputAmounts: new MoneyRange(Money.Coins(0.01m), Money.Coins(0.05m)),
			AllowedOutputAmounts: new MoneyRange(Money.Coins(0.01m), Money.Coins(0.05m)),
			AllowedInputTypes: (new ScriptType[] { ScriptType.P2WPKH }).ToImmutableSortedSet(),
			CoordinationFeeRate: CoordinationFeeRate.Zero,
			MiningFeeRate: new FeeRate(5m),
			LiquidityClue: Money.Zero,
			SemiPrivateThreshold: 2
		);
}
