using AvalonStudio.Commands;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Linq;
using WalletWasabi.Gui.Tabs.WalletManager;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class WalletCommands
	{
		public WalletCommands()
		{
			GenerateWalletCommand = new CommandDefinition(
				"Generate Wallet", null, ReactiveCommand.Create(OnGenerateWallet));

			RecoverWalletCommand = new CommandDefinition(
				"Recover Wallet", null, ReactiveCommand.Create(OnRecoverWallet));

			LoadWallet = new CommandDefinition(
				"Load Wallet", null, ReactiveCommand.Create(OnLoadWallet));
		}

		private void OnGenerateWallet()
		{
			IShell tabs = IoC.Get<IShell>();
			IDocumentTabViewModel doc = tabs.Documents.FirstOrDefault(x => x is WalletManagerViewModel);
			if (doc != default)
			{
				var document = doc as WalletManagerViewModel;
				tabs.SelectedDocument = doc;
				document.SelectedCategory = document.Categories.First(x => x is GenerateWalletViewModel);
			}
			else
			{
				var document = new WalletManagerViewModel();
				tabs.AddDocument(document);
				document.SelectedCategory = document.Categories.First(x => x is GenerateWalletViewModel);
			}
		}

		private void OnRecoverWallet()
		{
			IShell tabs = IoC.Get<IShell>();
			IDocumentTabViewModel doc = tabs.Documents.FirstOrDefault(x => x is WalletManagerViewModel);
			if (doc != default)
			{
				var document = doc as WalletManagerViewModel;
				tabs.SelectedDocument = doc;
				document.SelectedCategory = document.Categories.First(x => x is RecoverWalletViewModel);
			}
			else
			{
				var document = new WalletManagerViewModel();
				tabs.AddDocument(document);
				document.SelectedCategory = document.Categories.First(x => x is RecoverWalletViewModel);
			}
		}

		private void OnLoadWallet()
		{
			IShell tabs = IoC.Get<IShell>();
			IDocumentTabViewModel doc = tabs.Documents.FirstOrDefault(x => x is WalletManagerViewModel);
			if (doc != default)
			{
				var document = doc as WalletManagerViewModel;
				tabs.SelectedDocument = doc;
				document.SelectedCategory = document.Categories.First(x => x is LoadWalletViewModel);
			}
			else
			{
				var document = new WalletManagerViewModel();
				tabs.AddDocument(document);
				document.SelectedCategory = document.Categories.First(x => x is LoadWalletViewModel);
			}
		}

		[ExportCommandDefinition("File.GenerateWallet")]
		public CommandDefinition GenerateWalletCommand { get; }

		[ExportCommandDefinition("File.RecoverWallet")]
		public CommandDefinition RecoverWalletCommand { get; }

		[ExportCommandDefinition("File.LoadWallet")]
		public CommandDefinition LoadWallet { get; }
	}
}
