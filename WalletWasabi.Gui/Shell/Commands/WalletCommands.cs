using AvalonStudio.Commands;
using System.Linq;
using System;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System.Composition;
using WalletWasabi.Gui.Tabs.WalletManager;
using Avalonia;
using WalletWasabi.Helpers;
using WalletWasabi.Gui.Tabs.LegalDocs;
using WalletWasabi.Services;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class WalletCommands
	{
		public Global Global { get; }

		[ImportingConstructor]
		public WalletCommands(CommandIconService commandIconService, AvaloniaGlobalComponent global)
		{
			Global = global.Global;

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
			if (!Global.LegalDocsManager.IsLegalDocsAgreed)
			{
				IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectLegalIssues();
				return;
			}

			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectGenerateWallet();
		}

		private void OnRecoverWallet()
		{
			if (!Global.LegalDocsManager.IsLegalDocsAgreed)
			{
				IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectLegalIssues();
				return;
			}

			IoC.Get<IShell>().GetOrCreate<WalletManagerViewModel>().SelectRecoverWallet();
		}

		private void OnLoadWallet()
		{
			if (!Global.LegalDocsManager.IsLegalDocsAgreed)
			{
				IoC.Get<IShell>().GetOrCreate<LegalDocsViewModel>().SelectLegalIssues();
				return;
			}

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
