using Microsoft.AspNetCore.Mvc;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabiClientLibrary.Controllers.Helpers;
using WalletWasabi.WabiSabiClientLibrary.Crypto;
using WalletWasabi.WabiSabiClientLibrary.Filters;
using WalletWasabi.WabiSabiClientLibrary.Models;

namespace WalletWasabi.WabiSabiClientLibrary.Controllers;

[ApiController]
[ExceptionTranslateFilter]
[Produces("application/json")]
public class Controller : ControllerBase, IDisposable
{
	private readonly WasabiRandom _random;
	private readonly Global _global;

	public Controller(Global global, WasabiRandom random)
	{
		_random = random;
		_global = global;
	}

	[HttpPost("get-version")]
	public GetVersionResponse GetVersionAsync()
	{
		return new GetVersionResponse(_global.Version, _global.CommitHash, _global.Debug);
	}

	[HttpPost("get-anonymity-scores")]
	public GetAnonymityScoresResponse GetAnonymityScores(GetAnonymityScoresRequest request)
	{
		return GetAnonymityScoresHelper.GetAnonymityScores(request);
	}

	/// <summary>
	/// Given a set of unspent transaction outputs, choose a subset of the outputs that are best to register in a single CoinJoin round according to the given strategy.
	/// </summary>
	/// <seealso cref="CoinJoinClient.SelectCoinsForRound"/>
	[HttpPost("select-inputs-for-round")]
	public SelectInputsForRoundResponse SelectInputsForRound(SelectInputsForRoundRequest request)
	{
		return SelectInputsForRoundHelper.SelectInputsForRound(request, _random);
	}

	/// <summary>
	/// Given a set of effective input amounts registered by a participant and a set of effective input amounts
	/// registered by other participants, decompose the amounts registered by the participant into output amounts.
	/// </summary>
	[HttpPost("get-outputs-amounts")]
	public GetOutputAmountsResponse GetOutputAmounts(GetOutputAmountsRequest request)
	{
		return GetOutputAmountsHelper.GetOutputAmounts(request, _random);
	}

	[HttpPost("get-zero-credential-requests")]
	public GetZeroCredentialRequestsResponse GetZeroCredentialRequests(GetZeroCredentialRequestsRequest request)
	{
		return CredentialHelper.GetZeroCredentialRequests(request, _random);
	}

	[HttpPost("get-real-credential-requests")]
	public GetRealCredentialRequestsResponse GetRealCredentialRequests(GetRealCredentialRequestsRequest request)
	{
		return CredentialHelper.GetRealCredentialRequests(request, _random);
	}

	[HttpPost("get-credentials")]
	public GetCredentialsResponse GetCredentials(GetCredentialsRequest request)
	{
		return CredentialHelper.GetCredentials(request, _random);
	}

	[HttpPost("init-liquidity-clue")]
	public InitLiquidityClueResponse InitLiquidityClue(InitLiquidityClueRequest request)
	{
		return LiquidityClueHelper.InitLiquidityClue(request);
	}

	[HttpPost("update-liquidity-clue")]
	public UpdateLiquidityClueResponse UpdateLiquidityClue(UpdateLiquidityClueRequest request)
	{
		return LiquidityClueHelper.UpdateLiquidityClue(request);
	}

	[HttpPost("get-liquidity-clue")]
	public GetLiquidityClueResponse GetLiquidityClue(GetLiquidityClueRequest request)
	{
		return LiquidityClueHelper.GetLiquidityClue(request);
	}

	public void Dispose()
	{
	}
}
