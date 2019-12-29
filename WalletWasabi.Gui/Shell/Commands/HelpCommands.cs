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
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.TextResourcing;
using WalletWasabi.Gui.Tabs;
using WalletWasabi.Logging;

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
							Logger.LogWarning(ex);
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
							Logger.LogWarning(ex);
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
							Logger.LogWarning(ex);
							IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel(Global));
						}
					}));

			LegalDocumentsCommand = new CommandDefinition(
				"Legal Documents",
				commandIconService.GetCompletionKindImage("LegalDocuments"),
				ReactiveCommand.Create(() => IoC.Get<IShell>().AddOrSelectDocument(() => new LegalDocumentsViewModel(Global, legalDoc: Global?.LegalDocuments))));

			Observable
				.Merge(AboutCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(CustomerSupportCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(ReportBugCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(DocsCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(LegalDocumentsCommand.GetReactiveCommand().ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					NotificationHelpers.Error(ex.ToTypeMessageString());
					Logger.LogError(ex);
				});
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
