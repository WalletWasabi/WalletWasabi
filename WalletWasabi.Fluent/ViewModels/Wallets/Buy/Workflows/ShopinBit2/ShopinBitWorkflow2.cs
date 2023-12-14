using System.Collections.Generic;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class ShopinBitWorkflow2 : Workflow2
{
	public override IEnumerable<IWorkflowStep2> GetSteps(Conversation2 conversation)
	{
		return new IWorkflowStep2[]
		{
			new WelcomeStep(conversation),
			new CountryStep(conversation),
			new RequestedItemStep(conversation),
			new PrivacyPolicyStep(conversation)
		};
	}
}
