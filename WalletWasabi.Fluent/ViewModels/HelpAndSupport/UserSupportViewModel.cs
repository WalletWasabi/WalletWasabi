using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Windows.Input;
using JetBrains.Annotations;
using ReactiveUI;
using WalletWasabi.Fluent.Model;
using WalletWasabi.Fluent.ViewModels.Settings;
using WalletWasabi.Gui;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;
using WalletWasabi.Tor;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.HelpAndSupport
{
	[NavigationMetaData(
		Title = "User Support",
		Caption = "",
		Order = 0,
		Category = "Help & Support",
		Keywords = new[]
		{
			"Support", "Website"
		},
		IconName = "person_support_regular")]
	public partial class UserSupportViewModel : TriggerCommandViewModel
	{
		public override ICommand TargetCommand =>
			ReactiveCommand.CreateFromTask(async () =>
				await IoHelpers.OpenBrowserAsync(AboutViewModel.UserSupportLink));
	}

}