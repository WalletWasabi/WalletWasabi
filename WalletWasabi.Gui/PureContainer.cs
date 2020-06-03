using System;
using System.Collections.Generic;
using WalletWasabi.Gui.CommandLine;
using WalletWasabi.Gui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WalletWasabi.Gui.Tabs.WalletManager;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui
{
	class PureContainer
	{
		private readonly Global Global;
		private readonly WalletManager WalletManager;
		private readonly UiConfig UiConfig;
		private readonly Daemon Daemon;
		private readonly MixerCommand MixerCommand;
		private readonly PasswordFinderCommand PasswordFinderCommand;
		private readonly CommandInterpreter CommandInterpreter;
		private readonly StatusBarViewModel StatusBarViewModel;
		private readonly WalletManagerViewModel WalletManagerViewModel;
		private readonly MainWindowViewModel MainWindowViewModel;

		public PureContainer(Global global)
		{
			Global = global;
			WalletManager = global.WalletManager;
			UiConfig = Global.UiConfig;

			Daemon = new Daemon(Global);
			MixerCommand = new MixerCommand(Daemon);
			PasswordFinderCommand = new PasswordFinderCommand(WalletManager);
			CommandInterpreter = new CommandInterpreter(PasswordFinderCommand, MixerCommand);

			StatusBarViewModel = new StatusBarViewModel(Global);
			WalletManagerViewModel = new WalletManagerViewModel(WalletManager);
			MainWindowViewModel = new MainWindowViewModel(WalletManager, UiConfig, StatusBarViewModel, WalletManagerViewModel);
		}

		public CommandInterpreter GetSingletonCommandInterpreter()
		{
			return CommandInterpreter;
		}

		public MainWindowViewModel GetSingletonMainWindowViewModel()
		{
			return MainWindowViewModel;
		}
	}
}
