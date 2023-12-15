using WalletWasabi.BuyAnything;
using CountryState = WalletWasabi.WebClients.ShopWare.Models.State;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class StateStep : WorkflowStep<CountryState>
{
	public StateStep(Conversation conversation) : base(conversation)
	{
	}

	protected override Conversation PutValue(Conversation conversation, CountryState value) =>
		conversation.UpdateMetadata(m => m with { State = value });

	protected override CountryState? RetrieveValue(Conversation conversation) => conversation.MetaData.State;
}
