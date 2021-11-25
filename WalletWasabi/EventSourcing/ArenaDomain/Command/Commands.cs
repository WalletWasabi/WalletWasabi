using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Crypto;
using WalletWasabi.EventSourcing.ArenaDomain.Aggregates;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.ArenaDomain.Command
{
	public record StartRoundCommand(RoundParameters2 RoundParameters, Guid IdempotenceId) : ICommand;
	public record InputRegisterCommand(Coin Coin, OwnershipProof OwnershipProof, Guid AliceId, Guid IdempotenceId) : ICommand;
	public record InputConnectionConfirmedCommand(Coin Coin, OwnershipProof OwnershipProof, Guid AliceId, Guid IdempotenceId) : ICommand;
	public record RemoveInputCommand(Guid AliceId, Guid IdempotenceId) : ICommand;

	public record RegisterOutputCommand(Script Script, long Value, Guid IdempotenceId) : ICommand;

	public record StartOutputRegistrationCommand(Guid IdempotenceId) : ICommand;

	public record StartConnectionConfirmationCommand(Guid IdempotenceId) : ICommand;

	public record StartTransactionSigningCommand(Guid IdempotenceId) : ICommand;

	public record SucceedRoundCommand(Guid IdempotenceId) : ICommand;

	public record InputReadyToSignCommand(Guid AliceId, Guid IdempotenceId) : ICommand;

	public record AddSignatureEvent(Guid AliceId, WitScript WitScript, Guid IdempotenceId) : ICommand;

	public record EndRoundCommand(Guid IdempotenceId) : ICommand;
}
