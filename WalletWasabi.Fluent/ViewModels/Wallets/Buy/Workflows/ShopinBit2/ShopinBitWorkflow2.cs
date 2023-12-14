using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class ShopinBitWorkflow2 : Workflow2
{
	private readonly Wallet _wallet;

	public ShopinBitWorkflow2(Wallet wallet, Conversation2 conversation) : base(conversation)
	{
		_wallet = wallet;
	}

	public override async Task<Conversation2> ExecuteAsync()
	{
		await ExecuteStepAsync(new WelcomeStep(Conversation));
		await ExecuteStepAsync(new CountryStep(Conversation));
		await ExecuteStepAsync(new RequestedItemStep(Conversation));
		await ExecuteStepAsync(new PrivacyPolicyStep(Conversation));

		await ExecuteStepAsync(new StartConversationStep(Conversation));

		return Conversation;
	}
}
