using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Filters;
using WalletWasabi.Cache;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers;

[ApiController]
[ExceptionTranslate]
[LateResponseLoggerFilter]
[Route("[controller]")]
[Produces("application/json")]
public class WabiSabiController : ControllerBase, IWabiSabiApiRequestHandler
{
	public WabiSabiController(IdempotencyRequestCache idempotencyRequestCache, Arena arena, CoinJoinFeeRateStatStore coinJoinFeeRateStatStore, CoinJoinMempoolManager coinJoinMempoolManager)
	{
		IdempotencyRequestCache = idempotencyRequestCache;
		Arena = arena;
		CoinJoinFeeRateStatStore = coinJoinFeeRateStatStore;
		CoinJoinMempoolManager = coinJoinMempoolManager;
	}

	private IdempotencyRequestCache IdempotencyRequestCache { get; }
	private Arena Arena { get; }
	private CoinJoinFeeRateStatStore CoinJoinFeeRateStatStore { get; }
	public CoinJoinMempoolManager CoinJoinMempoolManager { get; }

	[HttpPost("status")]
	public async Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
	{
		var response = await Arena.GetStatusAsync(request, cancellationToken);
		var medians = CoinJoinFeeRateStatStore.GetDefaultMedians();
		return response with {CoinJoinFeeRateMedians = medians};
	}

	[HttpPost("connection-confirmation")]
	public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
	{
		return await IdempotencyRequestCache.GetCachedResponseAsync(request, action: Arena.ConfirmConnectionAsync, cancellationToken);
	}

	[HttpPost("input-registration")]
	public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		return await IdempotencyRequestCache.GetCachedResponseAsync(request, Arena.RegisterInputAsync, cancellationToken);
	}

	[HttpPost("output-registration")]
	public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		await IdempotencyRequestCache.GetCachedResponseAsync(request, action: Arena.RegisterOutputCoreAsync, cancellationToken);
	}

	[HttpPost("credential-issuance")]
	public async Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
	{
		return await IdempotencyRequestCache.GetCachedResponseAsync(request, action: Arena.ReissuanceAsync, cancellationToken);
	}

	[HttpPost("input-unregistration")]
	public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
	{
		await Arena.RemoveInputAsync(request, cancellableToken);
	}

	[HttpPost("transaction-signature")]
	public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
	{
		await Arena.SignTransactionAsync(request, cancellableToken);
	}

	[HttpPost("ready-to-sign")]
	public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellableToken)
	{
		await Arena.ReadyToSignAsync(request, cancellableToken);
	}

	/// <summary>
	/// Information about the current Rounds designed for the human eyes.
	/// </summary>
	[HttpGet("human-monitor")]
	public HumanMonitorResponse GetHumanMonitor()
	{
		var response = Arena.Rounds
			.Where(r => r.Phase is not Phase.Ended)
			.OrderByDescending(x => x.InputCount)
			.Select(r =>
				new HumanMonitorRoundResponse(
					RoundId: r.Id,
					IsBlameRound: r is BlameRound,
					InputCount: r.InputCount,
					Phase: r.Phase.ToString(),
					MaxSuggestedAmount: r.Parameters.MaxSuggestedAmount.ToDecimal(MoneyUnit.BTC),
					InputRegistrationRemaining: r.InputRegistrationTimeFrame.EndTime - DateTimeOffset.UtcNow));

		return new HumanMonitorResponse(response.ToArray());
	}

	/// <summary>
	/// Gets the list of unconfirmed coinjoin transaction Ids.
	/// </summary>
	/// <returns>The list of coinjoin transactions in the mempool.</returns>
	/// <response code="200">An array of transaction Ids</response>
	[HttpGet("unconfirmed-coinjoins")]
	[ProducesResponseType(200)]
	public IActionResult GetUnconfirmedCoinjoins()
	{
		IEnumerable<string> unconfirmedCoinJoinString = GetUnconfirmedCoinJoinCollection().Select(x => x.ToString());
		return Ok(unconfirmedCoinJoinString);
	}

	private IEnumerable<uint256> GetUnconfirmedCoinJoinCollection() => CoinJoinMempoolManager.CoinJoinIds;
}
