using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

public class AcceptOfferStep : WorkflowStep<object>
{
	public AcceptOfferStep(Conversation conversation) : base(conversation)
	{
	}

	public override async IAsyncEnumerable<Conversation> ExecuteAsync(Conversation conversation)
	{
		if (conversation.MetaData.OfferAccepted)
		{
			yield break;
		}

		IsBusy = true;

		await AcceptOfferAsync(conversation);

		yield return conversation.UpdateMetadata(m => m with { OfferAccepted = true });

		IsBusy = false;
	}

	protected override Conversation PutValue(Conversation conversation, object value) => conversation;

	protected override object? RetrieveValue(Conversation conversation) => conversation;

	private async Task AcceptOfferAsync(Conversation conversation)
	{
		var buyAnythingManager = Services.HostedServices.Get<BuyAnythingManager>();

		// TODO: pass cancellationtoken
		var cancellationToken = CancellationToken.None;

		var firstName = conversation.MetaData.FirstName;
		var lastName = conversation.MetaData.LastName;
		var streetName = conversation.MetaData.StreetName;
		var houseNumber = conversation.MetaData.HouseNumber;
		var postalCode = conversation.MetaData.PostalCode;
		var city = conversation.MetaData.City;
		var country = conversation.MetaData.Country;

		if (firstName is not { } ||
			lastName is not { } ||
			streetName is not { } ||
			houseNumber is not { } ||
			postalCode is not { } ||
			city is not { } ||
			country is not { }
		   )
		{
			throw new ArgumentException($"Conversation {conversation.Id} is missing Delivery information.");
		}

		var state = conversation.MetaData.State;

		await buyAnythingManager.AcceptOfferAsync(
			conversation.Id,
			firstName,
			lastName,
			streetName,
			houseNumber,
			postalCode,
			city,
			state is not null ? state.Id : "",
			country.Name,
			cancellationToken);
	}
}
