using System.Reactive;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Features.Onboarding;
using WalletWasabi.Fluent.Infrastructure;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Onboarding.Pages;

namespace WalletWasabi.Fluent.ViewModels.Onboarding;

[NavigationMetaData(Title = "Welcome")]
public partial class OnboardingWizardDialogViewModel : DialogViewModelBase<Unit>
{
	public OnboardingWizardDialogViewModel(UiContext uiContext)
	{
		var fourthViewModel = new FourthViewModel(
			() =>
			{
				uiContext.Navigate().To().WalletNamePage(WalletCreationOption.AddNewWallet);
				return Task.FromResult(true);
			},
			() =>
			{
				uiContext.Navigate().To().WalletNamePage(WalletCreationOption.ConnectToHardwareWallet);
				return Task.FromResult(true);
			},
			() =>
			{
				uiContext.Navigate().To().WalletNamePage(WalletCreationOption.ImportWallet);
				return Task.FromResult(true);
			},
			() =>
			{
				uiContext.Navigate().To().WalletNamePage(WalletCreationOption.RecoverWallet);
				return Task.FromResult(true);
			});

		var pages = new IWizardPage[]
		{
			new FirstViewModel(),
			new SecondViewModel(),
			new ThirdViewModel(),
			fourthViewModel
		};

		Wizard = new Wizard(pages);
	}

	public Wizard Wizard { get; }
}
