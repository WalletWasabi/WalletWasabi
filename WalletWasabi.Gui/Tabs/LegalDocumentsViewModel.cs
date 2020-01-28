using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalDocumentsViewModel : TextResourceViewModelBase
	{
		public LegalDocumentsViewModel() : base("Legal Documents", new Uri("avares://WalletWasabi.Gui/Assets/LegalDocuments.txt"))
		{
		}
	}
}
