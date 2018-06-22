using AvalonStudio.Commands;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.Tabs;

namespace WalletWasabi.Gui.Shell.Commands
{
	internal class ToolCommands
	{
		public ToolCommands()
		{
			WalletManagerCommand = new CommandDefinition(
				"Wallet Manager", null, ReactiveCommand.Create(OnWalletManager));

			SettingsCommand = new CommandDefinition("Settings", null, ReactiveCommand.Create(() => { }));
		}

		private void OnWalletManager()
		{
			IoC.Get<IShell>().AddDocument(new WalletManagerViewModel());
		}

		[ExportCommandDefinition("Tools.WalletManager")]
		public CommandDefinition WalletManagerCommand { get; }

		[ExportCommandDefinition("Tools.Settings")]
		public CommandDefinition SettingsCommand { get; }
	}
}
