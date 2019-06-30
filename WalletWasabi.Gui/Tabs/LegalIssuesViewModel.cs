using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalIssuesViewModel : TextViewModelBase
	{
		public LegalIssuesViewModel(Global global) : base(global, "Legal Issues", new Uri("resm:WalletWasabi.Gui.Assets.LegalIssues.txt"))
		{
		}
	}
}
