using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using WalletWasabi.Cache;
using WalletWasabi.Coordinator.Filters;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Coordinator.Controllers;

[ApiController]
[ExceptionTranslate]
[LateResponseLoggerFilter]
[Route("[controller]")]
[Produces("application/json")]
public class WabiSabiController : ControllerBase, IWabiSabiApiRequestHandler
{
	public WabiSabiController(IdempotencyRequestCache idempotencyRequestCache, Arena arena, CoinJoinFeeRateStatStore coinJoinFeeRateStatStore)
	{
		_idempotencyRequestCache = idempotencyRequestCache;
		_arena = arena;
		_coinJoinFeeRateStatStore = coinJoinFeeRateStatStore;
	}

	private readonly IdempotencyRequestCache _idempotencyRequestCache;
	private readonly Arena _arena;
	private readonly CoinJoinFeeRateStatStore _coinJoinFeeRateStatStore;

	[HttpPost("status")]
	public async Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
	{
		var response = await _arena.GetStatusAsync(request, cancellationToken);
		var medians = _coinJoinFeeRateStatStore.GetDefaultMedians();
		return response with {CoinJoinFeeRateMedians = medians};
	}

	[HttpPost("connection-confirmation")]
	public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
	{
		return await _idempotencyRequestCache.GetCachedResponseAsync(request, action: _arena.ConfirmConnectionAsync, cancellationToken);
	}

	[HttpPost("input-registration")]
	public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		return await _idempotencyRequestCache.GetCachedResponseAsync(request, _arena.RegisterInputAsync, cancellationToken);
	}

	[HttpPost("output-registration")]
	public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		await _idempotencyRequestCache.GetCachedResponseAsync(request, action: _arena.RegisterOutputCoreAsync, cancellationToken);
	}

	[HttpPost("credential-issuance")]
	public async Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
	{
		return await _idempotencyRequestCache.GetCachedResponseAsync(request, action: _arena.ReissuanceAsync, cancellationToken);
	}

	[HttpPost("input-unregistration")]
	public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
	{
		await _arena.RemoveInputAsync(request, cancellableToken);
	}

	[HttpPost("transaction-signature")]
	public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
	{
		await _arena.SignTransactionAsync(request, cancellableToken);
	}

	[HttpPost("ready-to-sign")]
	public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellableToken)
	{
		await _arena.ReadyToSignAsync(request, cancellableToken);
	}

	/// <summary>
	/// Information about the current Rounds designed for the human eyes.
	/// </summary>
	[HttpGet("human-monitor")]
	public HumanMonitorResponse GetHumanMonitor()
	{
		var response = _arena.Rounds
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
}
