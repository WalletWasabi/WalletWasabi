using WalletWasabi.EventSourcing.Interfaces;

namespace WalletWasabi.Tests.UnitTests.EventSourcing.TestDomain
{
	public record RoundStarted(ulong MinInputSats) : IEvent;
	public record InputRegistered(string InputId, ulong Sats) : IEvent;
	public record InputUnregistered(string InputId) : IEvent;
	public record SigningStarted() : IEvent;
	public record RoundSucceeded(string TxId) : IEvent;
	public record RoundFailed(string Reason) : IEvent;
}
