using System;
using System.IO;
using Avalonia;
using Avalonia.Platform;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Tabs
{
	public class TermsAndConditionsViewModel : TextResourceViewModelBase
	{
		public TermsAndConditionsViewModel(Global global) : base(global, "Terms and Conditions", new Uri("resm:WalletWasabi.Gui.Assets.TermsAndConditions.txt"))
		{
		}
	}
}
