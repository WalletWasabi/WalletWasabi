using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BuyAnything;
using WalletWasabi.Fluent.ViewModels.HelpAndSupport;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public sealed partial class DeliveryWorkflow : Workflow
{
	public DeliveryWorkflow(
		WorkflowState workflowState,
		WebClients.ShopWare.Models.State[] states)
	{
		var termsOfServiceUrl = "https://shopinbit.com/Information/Terms-Conditions/";

		Steps = new List<WorkflowStep>
		{
			// Info
			new(false,
				new DefaultInputValidator(
					workflowState,
					() => $"To proceed, I'll need some details to ensure a smooth delivery. Please provide the following information:")),
			// Firstname
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "Your First Name:")),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new FirstNameInputValidator(
					workflowState,
					ChatMessageMetaData.ChatMessageTag.FirstName),
				null),
			// Lastname
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "Your Last Name:")),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new LastNameInputValidator(
					workflowState,
					ChatMessageMetaData.ChatMessageTag.LastName),
				null),
			// Streetname
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "Street Name:")),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new StreetNameInputValidator(
					workflowState,
					ChatMessageMetaData.ChatMessageTag.StreetName),
				null),
			// Housenumber
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "House Number:")),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new HouseNumberInputValidator(
					workflowState,
					ChatMessageMetaData.ChatMessageTag.HouseNumber),
				null),
			// ZIP/Postalcode
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "ZIP/Postal Code:")),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new PostalCodeInputValidator(
					workflowState,
					ChatMessageMetaData.ChatMessageTag.PostalCode),
				null),
			// City
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "City:")),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new CityInputValidator(
					workflowState,
					ChatMessageMetaData.ChatMessageTag.City),
				null),
			// State
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => "State:"),
				CanSkipStateStep(states)),
			new EditableWorkflowStep(requiresUserInput: true,
				userInputValidator: new StateInputValidator(
					workflowState,
					states,
					ChatMessageMetaData.ChatMessageTag.State),
				null,
				CanSkipStateStep(states)),
			// Accept Terms of service
			new (false,
				new DefaultInputValidator(
					workflowState,
					() => $"Thank you for providing your details. Please double-check them for accuracy. If everything looks good, agree to our Terms and Conditions and click 'BUY NOW' to proceed")),
			new (requiresUserInput: true,
				userInputValidator: new ConfirmTosInputValidator(
					workflowState,
					new LinkViewModel
					{
						Link = termsOfServiceUrl,
						Description = "Accept the Terms of service",
						IsClickable = true
					},
					() => null,
					"BUY NOW")),
		};

		CreateCanEditObservable();
	}

	private bool CanSkipStateStep(WebClients.ShopWare.Models.State[] states)
	{
		return states.Length <= 0;
	}

	protected override void CreateCanEditObservable()
	{
		CanEditObservable = this.WhenAnyValue(x => x.IsCompleted).Select(x => !x);
	}

	public override bool TryToEditStep(WorkflowStep step, string message)
	{
		var result = base.TryToEditStep(step, message);

		// TODO: Edit step message.

		step.Update(message);

		return result;
	}
}
