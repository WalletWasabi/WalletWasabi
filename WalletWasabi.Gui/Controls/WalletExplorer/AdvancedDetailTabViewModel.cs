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
		public AdvancedDetailTabViewModel(string title, object targetVM, IEnumerable<AdvancedDetailPair> bindingPairs) : base(title)
		{
			Global = Locator.Current.GetService<Global>();
			BindingPairs = bindingPairs;
			TargetVM = targetVM;
		}
		
		public object TargetVM { get; }
		public IEnumerable<AdvancedDetailPair> BindingPairs { get; }
		private CompositeDisposable Disposables { get; set; }
		private Global Global { get; }

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