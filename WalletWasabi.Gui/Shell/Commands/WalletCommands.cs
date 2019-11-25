using AvalonStudio.Commands;
using System.Linq;
using System;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Composition;
using WalletWasabi.Gui.Tabs.WalletManager;
using Avalonia;

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
		}

		private void OnGenerateWallet()
		{
			AvalonStudioShellExtensions.GetOrCreate<WalletManagerViewModel>(IoC.Get<IShell>()).SelectGenerateWallet();
		}

		private void OnRecoverWallet()
		{
			AvalonStudioShellExtensions.GetOrCreate<WalletManagerViewModel>(IoC.Get<IShell>()).SelectRecoverWallet();
		}

		private void OnLoadWallet()
		{
			AvalonStudioShellExtensions.GetOrCreate<WalletManagerViewModel>(IoC.Get<IShell>()).SelectLoadWallet();
		}

		[ExportCommandDefinition("File.GenerateWallet")]
		public CommandDefinition GenerateWalletCommand { get; }

		[ExportCommandDefinition("File.RecoverWallet")]
		public CommandDefinition RecoverWalletCommand { get; }

		[ExportCommandDefinition("File.LoadWallet")]
		public CommandDefinition LoadWallet { get; }
	}
}
