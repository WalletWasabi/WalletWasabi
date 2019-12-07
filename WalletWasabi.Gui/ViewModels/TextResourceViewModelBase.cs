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
		protected CompositeDisposable Disposables { get; private set; }

		private string _text;

		protected TextResourceViewModelBase(Global global, string title, Uri avaloniaTarget = null, string filePath = null) : base(global, title)
		{
			Text = "";
			if (avaloniaTarget is null && string.IsNullOrWhiteSpace(filePath))
			{
				throw new ArgumentNullException($"{avaloniaTarget} and {filePath}, both cannot be null.");
			}
			else if (avaloniaTarget is { } && filePath is { })
			{
				throw new ArgumentException($"{avaloniaTarget} and {filePath}, one of them must be null.");
			}

			AvaloniaTarget = avaloniaTarget;
			FilePath = filePath;
		}

		public string Text
		{
			get => _text;
			set => this.RaiseAndSetIfChanged(ref _text, value);
		}

		public Uri AvaloniaTarget { get; }
		public string FilePath { get; }

		private async Task<string> LoadDocumentAsync(Uri target)
		{
			var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
			using var stream = assetLocator.Open(target);
			using var reader = new StreamReader(stream);
			return await reader.ReadToEndAsync();
		}

		private async Task<string> LoadDocumentAsync(string path)
		{
			using var stream = File.OpenRead(path);
			using var reader = new StreamReader(stream);
			return await reader.ReadToEndAsync();
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = new CompositeDisposable();
			if (AvaloniaTarget is { })
			{
				LoadDocumentAsync(AvaloniaTarget)
					.ToObservable(RxApp.TaskpoolScheduler)
					.Take(1)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(x => Text = x)
					.DisposeWith(Disposables);
			}
			else
			{
				LoadDocumentAsync(FilePath)
					.ToObservable(RxApp.TaskpoolScheduler)
					.Take(1)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(x => Text = x)
					.DisposeWith(Disposables);
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
