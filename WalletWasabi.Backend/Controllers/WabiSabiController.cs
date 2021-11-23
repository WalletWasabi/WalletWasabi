using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Filters;
using WalletWasabi.EventSourcing;
using WalletWasabi.EventSourcing.ArenaDomain;
using WalletWasabi.EventSourcing.ArenaDomain.Events;
using WalletWasabi.Interfaces.EventSourcing;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers
{
	[ApiController]
	[ExceptionTranslate]
	[Route("[controller]")]
	[Produces("application/json")]
	public class WabiSabiController : ControllerBase, IWabiSabiApiRequestHandler
	{
		public WabiSabiController(IdempotencyRequestCache idempotencyRequestCache, Arena arena, IEventRepository eventRepository)
		{
			IdempotencyRequestCache = idempotencyRequestCache;
			Arena = arena;
			EventRepository = eventRepository;
		}

		private IdempotencyRequestCache IdempotencyRequestCache { get; }
		private Arena Arena { get; }
		public IEventRepository EventRepository { get; }

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
		public Task<EmptyResponse> RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
		{
			return IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.RegisterOutputAsync(request, token), cancellationToken);
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

		[HttpGet("round-events")]
		public async Task<IEnumerable<WrappedEvent>> GetRoundEvents(uint256 roundId, long afterSequenceId, CancellationToken cancellationToken)
		{
			var events = await EventRepository.ListEventsAsync(nameof(RoundAggregate), roundId.ToString(), afterSequenceId).ConfigureAwait(false);
			return events.Where(ev => ev.DomainEvent is IRoundClientEvent);
		}
	}
}
