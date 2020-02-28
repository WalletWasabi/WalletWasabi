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

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class TextResourceViewModelBase : WasabiDocumentTabViewModel
	{
		private string _text;

		protected TextResourceViewModelBase(string title, Uri target) : base(title)
		{
			Text = "";
			Target = target;
		}

		public string Text
		{
			get => _text;
			set => this.RaiseAndSetIfChanged(ref _text, value);
		}

		public Uri Target { get; }

		private async Task<string> LoadDocumentAsync(Uri target)
		{
			var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();

			using var stream = assetLocator.Open(target);
			using var reader = new StreamReader(stream);
			return await reader.ReadToEndAsync();
		}

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);
			
			LoadDocumentAsync(Target)
				.ToObservable(RxApp.TaskpoolScheduler)
				.Take(1)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Text = x)
				.DisposeWith(disposables);
		}
	}
}
