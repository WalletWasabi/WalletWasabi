using Avalonia.Controls;
using Avalonia.Diagnostics;
using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui.Tabs;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class HelpCommands
	{
		public HelpCommands()
		{
			AboutCommand = new CommandDefinition(
				"About", null, ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddDocument(new AboutViewModel());
				}));

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
