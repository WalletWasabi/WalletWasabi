using AvalonStudio.Commands;
using ReactiveUI;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class WalletCommands
	{
		[ExportCommandDefinition("File.GenerateWallet")]
		public CommandDefinition GenerateWalletCommand { get; }

		public WalletCommands()
		{
			GenerateWalletCommand = new CommandDefinition(
				"Generate Wallet", null, ReactiveCommand.Create(OnGenerateWallet));
		}

		private void OnGenerateWallet()
		{
		}
	}
}
