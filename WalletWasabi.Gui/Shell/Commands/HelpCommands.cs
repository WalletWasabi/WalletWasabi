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
using Avalonia;
using System.IO;
using System.Reactive.Disposables;
using System;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class HelpCommands : IDisposable
	{
		private CompositeDisposable Disposables { get; }

		[ImportingConstructor]
		public HelpCommands(CommandIconService commandIconService)
		{
			Disposables = new CompositeDisposable();

			AboutCommand = new CommandDefinition(
				"About",
				commandIconService.GetCompletionKindImage("About"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new AboutViewModel());
				}).DisposeWith(Disposables));

			CustomerSupportCommand = new CommandDefinition(
				"Customer Support",
				commandIconService.GetCompletionKindImage("CustomerSupport"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new CustomerSupportViewModel());
				}).DisposeWith(Disposables));

			ReportBugCommand = new CommandDefinition(
				"Report Bug",
				commandIconService.GetCompletionKindImage("ReportBug"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new ReportBugViewModel().DisposeWith(Disposables));
				}).DisposeWith(Disposables));

			PrivacyPolicyCommand = new CommandDefinition(
				"Privacy Policy",
				commandIconService.GetCompletionKindImage("PrivacyPolicy"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new PrivacyPolicyViewModel());
				}).DisposeWith(Disposables));

			TermsAndConditionsCommand = new CommandDefinition(
				"Terms and Conditions",
				commandIconService.GetCompletionKindImage("TermsAndConditions"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new TermsAndConditionsViewModel());
				}).DisposeWith(Disposables));

			LegalIssuesCommand = new CommandDefinition(
				"Legal Issues",
				commandIconService.GetCompletionKindImage("LegalIssues"),
				ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddOrSelectDocument(() => new LegalIssuesViewModel());
				}).DisposeWith(Disposables));

#if DEBUG
			DevToolsCommand = new CommandDefinition(
				"Dev Tools",
				commandIconService.GetCompletionKindImage("DevTools"),
				ReactiveCommand.Create(() =>
				{
					var devTools = new DevTools(Application.Current.Windows.FirstOrDefault());

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
				}).DisposeWith(Disposables));
#endif
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

#if DEBUG

		[ExportCommandDefinition("Help.DevTools")]
		public CommandDefinition DevToolsCommand { get; }

#endif

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
