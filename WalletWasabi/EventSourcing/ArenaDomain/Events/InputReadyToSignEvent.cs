using NBitcoin;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.ArenaDomain.Events
{
	public record InputReadyToSignEvent(OutPoint AliceOutPoint) : IEvent;
}
