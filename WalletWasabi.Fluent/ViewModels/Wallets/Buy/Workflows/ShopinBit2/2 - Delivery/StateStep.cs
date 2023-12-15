using WalletWasabi.BuyAnything;
using CountryState = WalletWasabi.WebClients.ShopWare.Models.State;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class StateStep : WorkflowStep2<CountryState>
{
	public StateStep(Conversation2 conversation) : base(conversation)
	{
	}

	protected override Conversation2 PutValue(Conversation2 conversation, CountryState value) =>
		conversation.UpdateMetadata(m => m with { State = value });

	protected override CountryState? RetrieveValue(Conversation2 conversation) => conversation.MetaData.State;
}
