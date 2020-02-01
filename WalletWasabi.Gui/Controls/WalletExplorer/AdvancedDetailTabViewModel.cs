using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reflection;
using Splat;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class AdvancedDetailTabViewModel : WasabiDocumentTabViewModel
	{
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }

		public AdvancedDetailTabViewModel(string Title, IEnumerable<(PropertyInfo, AdvancedDetailAttribute)> getAttr) : base(Title)
		{
			Global = Locator.Current.GetService<Global>();
		}

		public override void OnOpen()
		{
			Disposables = Disposables is null ?
				new CompositeDisposable() :
				throw new NotSupportedException($"Cannot open {GetType().Name} before closing it.");
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;
			return base.OnClose();
		}
	}
}