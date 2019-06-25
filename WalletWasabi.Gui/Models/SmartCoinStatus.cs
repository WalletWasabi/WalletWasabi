namespace WalletWasabi.Gui.Models
{
	public enum SmartCoinStatus
	{
		Unconfirmed, // The coin is unconfirmed.
		Confirmed, // The coin is confirmed.
		SpentAccordingToBackend, // The coin is spent according to the backend, but the wallet does not know about it yet. Probably a lost mempool transaction.
		MixingBanned, // The coin is banned from mixing.
		MixingWaitingForConfirmation, // The coin is on the mixing waiting list, but it is unconfirmed, so it cannot be registered yet.
		MixingOnWaitingList, // The coin is on the mixing waiting list.
		MixingInputRegistration, // The coin is registered for mixing.
		MixingConnectionConfirmation, // The coin being mixed and in ConnectionConfirmation phase already.
		MixingOutputRegistration, // The coin being mixed and in OutputRegistration phase already.
		MixingSigning // The coin being mixed and in Signing phase already.
	}
}
