using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using WalletWasabi.BuyAnything;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.WebClients.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows;

/// <summary>
/// ShopinBit Step #1: Welcome and Select Assistant Type
/// </summary>
public partial class WelcomeStep : WorkflowStep<BuyAnythingClient.Product?>
{
	public const string ServiceDescriptionUrl = "https://shopinbit.com/wasabiwelcome";
	
	[AutoNotify] private EnumValue<BuyAnythingClient.Product>? _product;

	public WelcomeStep(Conversation conversation, CancellationToken token) : base(conversation, token)
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
		yield return $"Please select the assistant that best fits your needs. Read more about them here: {ServiceDescriptionUrl}";
	}

	protected override BuyAnythingClient.Product? RetrieveValue(Conversation conversation) =>
		conversation.MetaData.Product;

	protected override Conversation PutValue(Conversation conversation, BuyAnythingClient.Product? value) =>
		conversation.UpdateMetadata(x => x with { Product = value });

	protected override string? StringValue(BuyAnythingClient.Product? value) =>
		value?.GetDescription();
}
