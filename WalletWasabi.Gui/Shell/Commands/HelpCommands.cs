using System.Composition;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using NBitcoin;
using ReactiveUI;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.KeyManagement;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class HelpCommands
	{
		[ImportingConstructor]
		public HelpCommands(CommandIconService commandIconService)
		{
			AboutCommand = new CommandDefinition(
				"About",
				commandIconService.GetCompletionKindImage("About"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
				}));

			CustomerSupportCommand = new CommandDefinition(
				"Customer Support",
				commandIconService.GetCompletionKindImage("CustomerSupport"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new CustomerSupportViewModel());
				}));

			ReportBugCommand = new CommandDefinition(
				"Report Bug",
				commandIconService.GetCompletionKindImage("ReportBug"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new ReportBugViewModel());
				}));

			PrivacyPolicyCommand = new CommandDefinition(
				"Privacy Policy",
				commandIconService.GetCompletionKindImage("PrivacyPolicy"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel());
				}));

			TermsAndConditionsCommand = new CommandDefinition(
				"Terms and Conditions",
				commandIconService.GetCompletionKindImage("TermsAndConditions"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel());
				}));

			LegalIssuesCommand = new CommandDefinition(
				"Legal Issues",
				commandIconService.GetCompletionKindImage("LegalIssues"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel());
				}));

			DevToolsCommand = new CommandDefinition(
				"Dev Tools",
				commandIconService.GetCompletionKindImage("DevTools"),
				ReactiveCommand.Create(() =>
				{
					var devTools = new DevTools(Window.OpenWindows.FirstOrDefault());

					var devToolsWindow = new Window
					{
						Width = 1024,
						Height = 512,
						Content = devTools,
						DataTemplates =
						{
							new ViewLocator<Avalonia.Diagnostics.ViewModels.ViewModelBase>(),
						}
					};

					devToolsWindow.Show();
				}));
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

		[ExportCommandDefinition("Help.LegalIssues")]
		public CommandDefinition LegalIssuesCommand { get; }

		[ExportCommandDefinition("Help.DevTools")]
		public CommandDefinition DevToolsCommand { get; }
	}
}
