using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalIssuesViewModel : TextResourceViewModelBase
	{
		public LegalIssuesViewModel() : base("Legal Issues", new Uri("avares://WalletWasabi.Gui/Assets/LegalIssues.txt"))
		{
		}
	}
}
