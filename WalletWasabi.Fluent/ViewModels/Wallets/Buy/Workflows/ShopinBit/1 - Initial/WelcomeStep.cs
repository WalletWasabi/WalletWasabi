using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.BuyAnything;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #1: Welcome and Select Assistant Type
/// </summary>
public partial class WelcomeStep : WorkflowStep<BuyAnythingClient.Product?>
{
	[AutoNotify] private EnumValue<BuyAnythingClient.Product>? _product;

	public WelcomeStep(Conversation conversation) : base(conversation)
	{
		var productsEnum = Enum.GetValues<BuyAnythingClient.Product>();

		Products = new(productsEnum.Select(x => new EnumValue<BuyAnythingClient.Product>(x, x.GetDescription() ?? "")));
		_product = Products.FirstOrDefault(x => x.Value == Value) ?? Products.FirstOrDefault();

		this.WhenAnyValue(x => x.Product)
			.Select(x => x?.Value)
			.BindTo(this, x => x.Value);
	}

	public ObservableCollection<EnumValue<BuyAnythingClient.Product>> Products { get; }

	protected override IEnumerable<string> BotMessages(Conversation conversation)
	{
		// Welcome
		yield return "Welcome to our 'Buy Anything' service! To get started, please select the assistant that best fits your needs.";

		// Fast Travel Assistant
		yield return "Fast Travel Assistant\n\nChoose this option if you have a specific flight or hotel in mind and need quick assistance with booking.";

		// General Travel Assistant
		yield return "General Travel Assistant\n\nSelect this if you're just starting to plan your travel and don't have any travel details yet.";

		// All-Purpose Concierge Assistant
		yield return "All-Purpose Concierge Assistant\n\nOur all-purpose assistant, ready to help with a wide range of purchases, from tech gadgets to vehicles and more.";
	}

	protected override BuyAnythingClient.Product? RetrieveValue(Conversation conversation) =>
		conversation.MetaData.Product;

	protected override Conversation PutValue(Conversation conversation, BuyAnythingClient.Product? value) =>
		conversation.UpdateMetadata(x => x with { Product = value });

	protected override string? StringValue(BuyAnythingClient.Product? value) =>
		value?.GetDescription();
}
