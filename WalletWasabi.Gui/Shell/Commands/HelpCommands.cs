using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using WalletWasabi.Gui.Tabs;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class HelpCommands
	{
		public Global Global { get; }

		[ImportingConstructor]
		public HelpCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = global.Global;
			AboutCommand = new CommandDefinition(
				"About",
				commandIconService.GetCompletionKindImage("About"),
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(Global))));

			CustomerSupportCommand = new CommandDefinition(
				"Customer Support",
				commandIconService.GetCompletionKindImage("CustomerSupport"),
				ReactiveCommand.Create(() =>
				{
					try
					{
						IoHelpers.OpenBrowser("https://www.reddit.com/r/WasabiWallet/");
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning(ex);
						IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(Global));
					}
				}));

			ReportBugCommand = new CommandDefinition(
				"Report Bug",
				commandIconService.GetCompletionKindImage("ReportBug"),
				ReactiveCommand.Create(() =>
				{
					try
					{
						IoHelpers.OpenBrowser("https://github.com/zkSNACKs/WalletWasabi/issues");
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning(ex);
						IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(Global));
					}
				}));

			DocsCommand = new CommandDefinition(
				"Documentation",
				commandIconService.GetCompletionKindImage("Documentation"),
				ReactiveCommand.Create(() =>
				{
					try
					{
						IoHelpers.OpenBrowser("https://docs.wasabiwallet.io/");
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning(ex);
						IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(Global));
					}
				}));

			PrivacyPolicyCommand = new CommandDefinition(
				"Privacy Policy",
				commandIconService.GetCompletionKindImage("PrivacyPolicy"),
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel(Global))));

			TermsAndConditionsCommand = new CommandDefinition(
				"Terms and Conditions",
				commandIconService.GetCompletionKindImage("TermsAndConditions"),
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel(Global))));

			LegalIssuesCommand = new CommandDefinition(
				"Legal Issues",
				commandIconService.GetCompletionKindImage("LegalIssues"),
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel(Global))));
		}

		[ExportCommandDefinition("Help.About")]
		public CommandDefinition AboutCommand { get; }

		[ExportCommandDefinition("Help.CustomerSupport")]
		public CommandDefinition CustomerSupportCommand { get; }

		[ExportCommandDefinition("Help.ReportBug")]
		public CommandDefinition ReportBugCommand { get; }

		[ExportCommandDefinition("Help.Documentation")]
		public CommandDefinition DocsCommand { get; }

		[ExportCommandDefinition("Help.PrivacyPolicy")]
		public CommandDefinition PrivacyPolicyCommand { get; }

		[ExportCommandDefinition("Help.TermsAndConditions")]
		public CommandDefinition TermsAndConditionsCommand { get; }

		[ExportCommandDefinition("Help.LegalIssues")]
		public CommandDefinition LegalIssuesCommand { get; }
	}
}
