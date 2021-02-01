﻿using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
	public partial class PasswordFoundViewModel : RoutableViewModel
	{
		[AutoNotify] private string _password;
		[AutoNotify] private bool _success;

		public PasswordFoundViewModel(string password)
		{
			Title = "Password Finder";
			_password = password;

			NextCommand = CancelCommand;
		}
	}
}