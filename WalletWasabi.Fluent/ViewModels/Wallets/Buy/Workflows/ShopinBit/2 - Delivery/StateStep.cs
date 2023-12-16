using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using CountryState = WalletWasabi.WebClients.ShopWare.Models.State;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public partial class StateStep : WorkflowStep<CountryState>
{
	[AutoNotify] private CountryState[] _states = Array.Empty<CountryState>();

	public StateStep(Conversation conversation) : base(conversation)
	{
	}

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		yield return "State:";
	}

	public override async IAsyncEnumerable<Conversation> ExecuteAsync(Conversation conversation)
	{
		// TODO: pass CancellationToken
		var cancellationToken = CancellationToken.None;

		if (conversation.MetaData.Country is { } country)
		{
			var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

			States = await buyAnythingManager.GetStatesForCountryAsync(country, cancellationToken);

			if (States.Length == 0)
			{
				SetCompleted();
				yield break;
			}
		}

		await foreach (var c in base.ExecuteAsync(conversation))
		{
			yield return c;
		}
	}

	protected override Conversation PutValue(Conversation conversation, CountryState value) =>
		conversation.UpdateMetadata(m => m with { State = value });

	protected override CountryState? RetrieveValue(Conversation conversation) => conversation.MetaData.State;
}
