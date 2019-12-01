using AvalonStudio.Commands;
using System.Linq;
using System;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Composition;
using WalletWasabi.Gui.Tabs.WalletManager;
using Avalonia;
using System.Reactive.Linq;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class WalletCommands
	{
		[ImportingConstructor]
		public WalletCommands(CommandIconService commandIconService)
		{
			GenerateWalletCommand = new CommandDefinition(
				"Generate Wallet",
				commandIconService.GetCompletionKindImage("GenerateWallet"),
				ReactiveCommand.Create(OnGenerateWallet));

			RecoverWalletCommand = new CommandDefinition(
				"Recover Wallet",
				commandIconService.GetCompletionKindImage("RecoverWallet"),
				ReactiveCommand.Create(OnRecoverWallet));

			LoadWallet = new CommandDefinition(
				"Load Wallet",
				commandIconService.GetCompletionKindImage("LoadWallet"),
				ReactiveCommand.Create(OnLoadWallet));

			Observable
				.Merge(GenerateWalletCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(RecoverWalletCommand.GetReactiveCommand().ThrownExceptions)
				.Merge(LoadWallet.GetReactiveCommand().ThrownExceptions)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private void OnGenerateWallet()
		{
			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectGenerateWallet();
		}

		private void OnRecoverWallet()
		{
			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectRecoverWallet();
		}

		private void OnLoadWallet()
		{
			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectLoadWallet();
		}

		[ExportCommandDefinition("File.GenerateWallet")]
		public CommandDefinition GenerateWalletCommand { get; }

		[ExportCommandDefinition("File.RecoverWallet")]
		public CommandDefinition RecoverWalletCommand { get; }

		[ExportCommandDefinition("File.LoadWallet")]
		public CommandDefinition LoadWallet { get; }
	}
}
