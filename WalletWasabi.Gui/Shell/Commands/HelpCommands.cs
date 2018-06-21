using AvalonStudio.Commands;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Dialogs;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class HelpCommands
	{
		public HelpCommands()
		{
			AboutCommand = new CommandDefinition(
				"About", null, ReactiveCommand.Create(async () => { await MainWindowViewModel.Instance.ShowDialogAsync(new CannotCloseDialogViewModel()); }));

			CustomerSupportCommand = new CommandDefinition(
				"Customer Support", null, ReactiveCommand.Create(() => { }));

			ReportBugCommand = new CommandDefinition(
				"Report Bug", null, ReactiveCommand.Create(() => { }));

			PrivacyPolicyCommand = new CommandDefinition(
				"Privacy Policy", null, ReactiveCommand.Create(() => { }));

			TermsAndConditionsCommand = new CommandDefinition(
				"Terms and Conditions", null, ReactiveCommand.Create(() => { }));
		}

		private void OnGenerateWallet()
		{
		}

		[ExportCommandDefinition("Help.About")]
		public CommandDefinition AboutCommand { get; }

		[ExportCommandDefinition("Help.CustomerSupport")]
		public CommandDefinition CustomerSupportCommand { get; }

		[ExportCommandDefinition("Help.ReportBug")]
		public CommandDefinition ReportBugCommand { get; }

		[ExportCommandDefinition("Help.PrivacyPolicy")]
		public CommandDefinition PrivacyPolicyCommand { get; }

		[ExportCommandDefinition("Help.TermsAndConditions")]
		public CommandDefinition TermsAndConditionsCommand { get; }
	}
}
