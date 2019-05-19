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
					try
					{
						IoHelpers.OpenBrowser("https://www.reddit.com/r/WasabiWallet/");
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning<HelpCommands>(ex);
						IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
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
						Logging.Logger.LogWarning<HelpCommands>(ex);
						IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
					}
				}));

			FaqCommand = new CommandDefinition(
				"FAQ",
				commandIconService.GetCompletionKindImage("Faq"),
				ReactiveCommand.Create(() =>
				{
					try
					{
						IoHelpers.OpenBrowser("https://github.com/zkSNACKs/WalletWasabi/blob/master/WalletWasabi.Documentation/FAQ.md");
					}
					catch (Exception ex)
					{
						Logging.Logger.LogWarning<HelpCommands>(ex);
						IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
					}
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

#if DEBUG
			DevToolsCommand = new CommandDefinition(
				"Dev Tools",
				commandIconService.GetCompletionKindImage("DevTools"),
				ReactiveCommand.Create(() =>
				{
					var devTools = new DevTools(Application.Current.Windows.FirstOrDefault());

					var devToolsWindow = new Window {
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
#endif
		}

		[ExportCommandDefinition("Help.About")]
		public CommandDefinition AboutCommand { get; }

		[ExportCommandDefinition("Help.CustomerSupport")]
		public CommandDefinition CustomerSupportCommand { get; }

		[ExportCommandDefinition("Help.ReportBug")]
		public CommandDefinition ReportBugCommand { get; }

		[ExportCommandDefinition("Help.Faq")]
		public CommandDefinition FaqCommand { get; }

		[ExportCommandDefinition("Help.PrivacyPolicy")]
		public CommandDefinition PrivacyPolicyCommand { get; }

		[ExportCommandDefinition("Help.TermsAndConditions")]
		public CommandDefinition TermsAndConditionsCommand { get; }

		[ExportCommandDefinition("Help.LegalIssues")]
		public CommandDefinition LegalIssuesCommand { get; }

#if DEBUG

		[ExportCommandDefinition("Help.DevTools")]
		public CommandDefinition DevToolsCommand { get; }

#endif
	}
}
