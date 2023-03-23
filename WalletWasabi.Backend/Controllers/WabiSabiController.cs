using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Affiliation;
using WalletWasabi.Backend.Filters;
using WalletWasabi.Cache;
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
	public WabiSabiController(IdempotencyRequestCache idempotencyRequestCache, Arena arena, CoinJoinFeeRateStatStore coinJoinFeeRateStatStore, AffiliationManager affiliationManager)
	{
		IdempotencyRequestCache = idempotencyRequestCache;
		Arena = arena;
		CoinJoinFeeRateStatStore = coinJoinFeeRateStatStore;
		AffiliationManager = affiliationManager;
	}

	private static TimeSpan RequestTimeout { get; } = TimeSpan.FromMinutes(5);
	private IdempotencyRequestCache IdempotencyRequestCache { get; }
	private Arena Arena { get; }
	private CoinJoinFeeRateStatStore CoinJoinFeeRateStatStore { get; }
	private AffiliationManager AffiliationManager { get; }

	[HttpPost("status")]
	public async Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
	{
		var before = DateTimeOffset.UtcNow;
		var response = await Arena.GetStatusAsync(request, cancellationToken);
		var medians = CoinJoinFeeRateStatStore.GetDefaultMedians();
		var affiliateInformation = AffiliationManager.GetAffiliateInformation();
		var ret = new RoundStateResponse(response.RoundStates, medians, affiliateInformation);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("status", duration);
		return ret;
	}

	[HttpPost("connection-confirmation")]
	public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(RequestTimeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

		var before = DateTimeOffset.UtcNow;
		var ret = await IdempotencyRequestCache.GetCachedResponseAsync(request, action: Arena.ConfirmConnectionAsync, linkedCts.Token);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("connection-confirmation", duration);
		return ret;
	}

	[HttpPost("input-registration")]
	public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(RequestTimeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

		var before = DateTimeOffset.UtcNow;
		InputRegistrationResponse ret = await IdempotencyRequestCache.GetCachedResponseAsync(request, Arena.RegisterInputAsync, linkedCts.Token);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("input-registration", duration);
		return ret;
	}

	[HttpPost("output-registration")]
	public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(RequestTimeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

		var before = DateTimeOffset.UtcNow;
		await IdempotencyRequestCache.GetCachedResponseAsync(request, action: Arena.RegisterOutputCoreAsync, linkedCts.Token);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("output-registration", duration);
	}

	[HttpPost("credential-issuance")]
	public async Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
	{
		using CancellationTokenSource timeoutCts = new(RequestTimeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

		var before = DateTimeOffset.UtcNow;
		var ret = await IdempotencyRequestCache.GetCachedResponseAsync(request, action: Arena.ReissuanceAsync, linkedCts.Token);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("credential-issuance", duration);
		return ret;
	}

	[HttpPost("input-unregistration")]
	public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
	{
		var before = DateTimeOffset.UtcNow;
		await Arena.RemoveInputAsync(request, cancellableToken);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("input-unregistration", duration);
	}

	[HttpPost("transaction-signature")]
	public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
	{
		var before = DateTimeOffset.UtcNow;
		await Arena.SignTransactionAsync(request, cancellableToken);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("transaction-signature", duration);
	}

	[HttpPost("ready-to-sign")]
	public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellableToken)
	{
		var before = DateTimeOffset.UtcNow;
		await Arena.ReadyToSignAsync(request, cancellableToken);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("ready-to-sign", duration);
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
					MaxSuggestedAmount: r.Parameters.MaxSuggestedAmount.ToDecimal(NBitcoin.MoneyUnit.BTC),
					InputRegistrationRemaining: r.InputRegistrationTimeFrame.EndTime - DateTimeOffset.UtcNow));

		return new HumanMonitorResponse(response.ToArray());
	}
}
