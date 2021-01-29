﻿using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public class ContainsSymbolsViewModel : RoutableViewModel
	{
		public ContainsSymbolsViewModel(PasswordFinderOptions options)
		{
			Options = options;
			Title = "Password Finder";

			YesCommand = ReactiveCommand.Create(() => SetAnswer(true));
			NoCommand = ReactiveCommand.Create(() => SetAnswer(false));
		}

		public PasswordFinderOptions Options { get; }

		public ICommand YesCommand { get; }

		public ICommand NoCommand { get; }

		private void SetAnswer(bool ans)
		{
			Options.UseSymbols = ans;
			Navigate().To(new SearchPasswordViewModel(Options));
		}
	}
}