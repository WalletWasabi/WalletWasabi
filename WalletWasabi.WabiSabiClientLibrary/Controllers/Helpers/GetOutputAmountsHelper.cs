using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;

public class GetOutputAmountsHelper
{
	public static GetOutputAmountsResponse GetOutputAmounts(GetOutputAmountsRequest request, WasabiRandom random)
	{
		if (request.InputSize != ScriptType.Taproot.EstimateInputVsize())
		{
			throw new Exception("Incorrect input size. Only taproot is supported.");
		}

		if (request.OutputSize != ScriptType.Taproot.EstimateOutputVsize())
		{
			throw new Exception("Incorrect output size. Only taproot is supported");
		}

		AmountDecomposer decomposer = new(request.MiningFeeRate, request.AllowedOutputAmounts, request.AvailableVsize, true, random, false);

		IEnumerable<Money> myInputCoinEffectiveValues = request.InternalAmounts.Select(x => Money.FromUnit(x, MoneyUnit.Satoshi));
		IEnumerable<Money> othersInputCoinEffectiveValues = request.ExternalAmounts.Select(x => Money.FromUnit(x, MoneyUnit.Satoshi));

		IEnumerable<Money> response = decomposer.Decompose(myInputCoinEffectiveValues, othersInputCoinEffectiveValues).Select(x => x.Amount);
		long[] result = response.Select(x => x.Satoshi).ToArray();

		return new GetOutputAmountsResponse(result);
	}
}
