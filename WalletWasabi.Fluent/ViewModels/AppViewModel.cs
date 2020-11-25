using System;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Fluent.ViewModels
{
	public class AppViewModel : ViewModelBase
	{
		public static IObservable<bool>? PrivacyMode { get; set; }
	}
}