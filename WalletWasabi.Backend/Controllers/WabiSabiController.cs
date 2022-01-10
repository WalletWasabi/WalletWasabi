using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Controllers.WabiSabi;
using WalletWasabi.Backend.Filters;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers;

[ApiController]
[ExceptionTranslate]
[Route("[controller]")]
[Produces("application/json")]
public class WabiSabiController : ControllerBase, IWabiSabiApiRequestHandler
{
	public WabiSabiController(IdempotencyRequestCache idempotencyRequestCache, Arena arena)
	{
		IdempotencyRequestCache = idempotencyRequestCache;
		Arena = arena;
	}

	private IdempotencyRequestCache IdempotencyRequestCache { get; }
	private Arena Arena { get; }

	[HttpGet("status")]
	public Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
	{
		return Arena.GetStatusAsync(cancellationToken);
	}

	[HttpPost("connection-confirmation")]
	public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
	{
		return IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.ConfirmConnectionAsync(request, token), cancellationToken);
	}

	[HttpPost("input-registration")]
	public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		return IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.RegisterInputAsync(request, token), cancellationToken);
	}

	[HttpPost("output-registration")]
	public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		return IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.RegisterOutputCoreAsync(request, token), cancellationToken);
	}

	[HttpPost("credential-issuance")]
	public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
	{
		return IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.ReissuanceAsync(request, token), cancellationToken);
	}

	[HttpPost("input-unregistration")]
	public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
	{
		return Arena.RemoveInputAsync(request, cancellableToken);
	}

	[HttpPost("transaction-signature")]
	public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
	{
		return Arena.SignTransactionAsync(request, cancellableToken);
	}

	[HttpPost("ready-to-sign")]
	public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellableToken)
	{
		return Arena.ReadyToSignAsync(request, cancellableToken);
	}
}
