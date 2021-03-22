using Avalonia;
using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.Composition;
using System.IO;
using System.Reactive.Linq;
using WalletWasabi.Gui.Helpers;
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

			UserSupportCommand = new CommandDefinition(
				"User Support",
				commandIconService.GetCompletionKindImage("UserSupport"),
				ReactiveCommand.CreateFromTask(async () =>
					{
						try
						{
							await IoHelpers.OpenBrowserAsync("https://github.com/zkSNACKs/WalletWasabi/discussions/5185");
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

			Observable
				.Merge(AboutCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(UserSupportCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(ReportBugCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(DocsCommand.GetReactiveCommand().ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});
		}

		[ExportCommandDefinition("Help.About")]
		public CommandDefinition AboutCommand { get; }

		[ExportCommandDefinition("Help.UserSupport")]
		public CommandDefinition UserSupportCommand { get; }

		[ExportCommandDefinition("Help.ReportBug")]
		public CommandDefinition ReportBugCommand { get; }

		[ExportCommandDefinition("Help.Documentation")]
		public CommandDefinition DocsCommand { get; }
	}
}
