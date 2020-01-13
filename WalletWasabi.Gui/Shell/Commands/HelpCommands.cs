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
using WalletWasabi.Logging;

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
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel())));

			CustomerSupportCommand = new CommandDefinition(
				"Customer Support",
				commandIconService.GetCompletionKindImage("CustomerSupport"),
				ReactiveCommand.CreateFromTask(async () =>
					{
						try
						{
							await IoHelpers.OpenBrowserAsync("https://www.reddit.com/r/WasabiWallet/");
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex);
							IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
						}
					}));

			ReportBugCommand = new CommandDefinition(
				"Report Bug",
				commandIconService.GetCompletionKindImage("ReportBug"),
				ReactiveCommand.CreateFromTask(async () =>
					{
						try
						{
							await IoHelpers.OpenBrowserAsync("https://github.com/zkSNACKs/WalletWasabi/issues");
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex);
							IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
						}
					}));

			DocsCommand = new CommandDefinition(
				"Documentation",
				commandIconService.GetCompletionKindImage("Documentation"),
				ReactiveCommand.CreateFromTask(async () =>
					{
						try
						{
							await IoHelpers.OpenBrowserAsync("https://docs.wasabiwallet.io/");
						}
						catch (Exception ex)
						{
							Logger.LogWarning(ex);
							IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
						}
					}));

			LegalDocumentsCommand = new CommandDefinition(
				"Legal Documents",
				commandIconService.GetCompletionKindImage("LegalDocuments"),
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new LegalDocumentsViewModel())));

			Observable
				.Merge(AboutCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(CustomerSupportCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(ReportBugCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(DocsCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(LegalDocumentsCommand.GetReactiveCommand().ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		[ExportCommandDefinition("Help.About")]
		public CommandDefinition AboutCommand { get; }

		[ExportCommandDefinition("Help.CustomerSupport")]
		public CommandDefinition CustomerSupportCommand { get; }

		[ExportCommandDefinition("Help.ReportBug")]
		public CommandDefinition ReportBugCommand { get; }

		[ExportCommandDefinition("Help.Documentation")]
		public CommandDefinition DocsCommand { get; }

		[ExportCommandDefinition("Help.LegalDocuments")]
		public CommandDefinition LegalDocumentsCommand { get; }
	}
}
