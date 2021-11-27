using NBitcoin;
using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.EventSourcing.ArenaDomain.Events
{
	public record InputUnregistered(OutPoint AliceOutPoint) : IEvent;
}
