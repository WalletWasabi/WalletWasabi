using Avalonia;
using Avalonia.Platform;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.ViewModels;

namespace WalletWasabi.Gui.Tabs
{
	public abstract class TextViewModelBase : WasabiDocumentTabViewModel
	{
		protected CompositeDisposable Disposables { get; private set; } = new CompositeDisposable();

		public string _text;

		public TextViewModelBase(Global global, string title, Uri target) : base(global, title)
		{
			Text = "";

			LoadDocumentAsync(target)
				.ToObservable()
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Text = x)
				.DisposeWith(Disposables);
		}

		public string Text
		{
			get => _text;
			set => this.RaiseAndSetIfChanged(ref _text, value);
		}

		private async Task<string> LoadDocumentAsync(Uri target)
		{
			var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();

			using (var stream = assetLocator.Open(target))
			using (var reader = new StreamReader(stream))
			{
				return await reader.ReadToEndAsync();
			}
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;
			return base.OnClose();
		}
	}
}
