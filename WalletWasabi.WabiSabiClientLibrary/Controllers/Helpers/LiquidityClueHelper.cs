using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using WalletWasabi.Blockchain.Analysis;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;

public class LiquidityClueHelper
{
	public static InitLiquidityClueResponse InitLiquidityClue(InitLiquidityClueRequest request)
	{
		LiquidityClueProvider liquidityClueProvider = new();
		liquidityClueProvider.InitLiquidityClue(request.ExternalAmounts);
		Money? rawLiquidityClue = liquidityClueProvider.LiquidityClue;
		return new InitLiquidityClueResponse(rawLiquidityClue);
	}

	public static UpdateLiquidityClueResponse UpdateLiquidityClue(UpdateLiquidityClueRequest request)
	{
		LiquidityClueProvider liquidityClueProvider = new();
		liquidityClueProvider.LiquidityClue = request.RawLiquidityClue;
		liquidityClueProvider.UpdateLiquidityClue(request.MaxSuggestedAmount, request.ExternalAmounts);
		Money? rawLiquidityClue = liquidityClueProvider.LiquidityClue;
		return new UpdateLiquidityClueResponse(rawLiquidityClue);
	}

	public static GetLiquidityClueResponse GetLiquidityClue(GetLiquidityClueRequest request)
	{
		LiquidityClueProvider liquidityClueProvider = new();
		liquidityClueProvider.LiquidityClue = request.RawLiquidityClue;
		Money liquidityClue = liquidityClueProvider.GetLiquidityClue(request.MaxSuggestedAmount);
		return new GetLiquidityClueResponse(liquidityClue);
	}
}

