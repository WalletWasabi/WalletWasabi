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
				"Customer Support", null, ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddDocument(new CustomerSupportViewModel());
				}));

			ReportBugCommand = new CommandDefinition(
				"Report Bug", null, ReactiveCommand.Create(() =>
				{
					IoC.Get<IShell>().AddDocument(new ReportBugViewModel());
				}));

			PrivacyPolicyCommand = new CommandDefinition(
				"Privacy Policy", null, ReactiveCommand.Create(() => { }));

			TermsAndConditionsCommand = new CommandDefinition(
				"Terms and Conditions", null, ReactiveCommand.Create(() => { }));

			DevToolsCommand = new CommandDefinition("Dev Tools", null, ReactiveCommand.Create(() =>
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

		[ExportCommandDefinition("Help.DevTools")]
		public CommandDefinition DevToolsCommand { get; }
	}
}
