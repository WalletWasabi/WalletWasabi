using AvalonStudio.Commands;
using ReactiveUI;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class WalletCommands
	{
		public WalletCommands()
		{
			GenerateWalletCommand = new CommandDefinition(
				"Generate Wallet", null, ReactiveCommand.Create(OnGenerateWallet));

			RecoverWalletCommand = new CommandDefinition(
				"Recover Wallet", null, ReactiveCommand.Create(() => { }));

			LoadWallet = new CommandDefinition(
				"Load Wallet", null, ReactiveCommand.Create(() => { }));
		}

		private void OnGenerateWallet()
		{
		}

		[ExportCommandDefinition("File.GenerateWallet")]
		public CommandDefinition GenerateWalletCommand { get; }

		[ExportCommandDefinition("File.RecoverWallet")]
		public CommandDefinition RecoverWalletCommand { get; }

		[ExportCommandDefinition("File.LoadWallet")]
		public CommandDefinition LoadWallet { get; }
	}
}
