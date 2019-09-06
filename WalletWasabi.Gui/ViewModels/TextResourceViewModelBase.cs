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
	public abstract class TextResourceViewModelBase : CategoryViewModel, IDisposable
	{
		protected CompositeDisposable Disposables { get; private set; }

		private string _text;

		public TextResourceViewModelBase(Global global, string title, Uri target) : base(title)
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
			try
			{
				Stream stream;
				if (target.IsFile)
				{
					stream = new FileStream(target.AbsolutePath, FileMode.Open);
				}
				else
				{
					var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();
					stream = assetLocator.Open(target);
				}

				using (stream)
				using (var reader = new StreamReader(stream))
				{
					return await reader.ReadToEndAsync();
				}
			}
			catch (Exception ex)
			{
				return await Task.FromResult<string>($"Could not load document - {ex.ToTypeMessageString()}");
			}
		}

		public override void OnCategorySelected()
		{
			base.OnCategorySelected();
			if (Disposables != null)
			{
				return;
			}

			Disposables = new CompositeDisposable();
			LoadDocumentAsync(Target)
				.ToObservable(RxApp.TaskpoolScheduler)
				.Take(1)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => Text = x)
				.DisposeWith(Disposables);
		}

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
