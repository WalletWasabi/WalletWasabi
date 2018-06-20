using System;
using System.Collections.Generic;
using System.Text;

namespace WalletWasabi.Gui.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		public StatusBarViewModel StatusBar { get; }

		public MainWindowViewModel(StatusBarViewModel statusBar)
		{
			StatusBar = statusBar;
		}
	}
}
