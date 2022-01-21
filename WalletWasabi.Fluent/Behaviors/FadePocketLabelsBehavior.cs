using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.VisualTree;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Controls;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;

namespace WalletWasabi.Fluent.Behaviors;

public class FadePocketLabelsBehavior : AttachedToVisualTreeBehavior<TagsBox>
{
	private TagControl[] _currentTags = Array.Empty<TagControl>();
	private CompositeDisposable? _disposable;
	private bool _isRunning = true;

	public static readonly StyledProperty<PocketViewModel[]> PocketsProperty =
		AvaloniaProperty.Register<FadePocketLabelsBehavior, PocketViewModel[]>(nameof(Pockets));

	public PocketViewModel[] Pockets
	{
		get => GetValue(PocketsProperty);
		set => SetValue(PocketsProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject is null)
		{
			return;
		}

		RxApp.MainThreadScheduler.Schedule(async () =>
		{
			while (_isRunning)
			{
				var tagItems = AssociatedObject.GetVisualDescendants().OfType<TagControl>().ToArray();

				if (tagItems.Any() && tagItems.Length != _currentTags.Length)
				{
					_currentTags = tagItems;
					_disposable?.Dispose();
					_disposable = new CompositeDisposable();

					foreach (var tagControl in _currentTags)
					{
						tagControl
							.WhenAnyValue(x => x.IsPointerOver)
							.Skip(1)
							.Subscribe(x =>
							{
								var tagControlLabel = tagControl.DataContext;
								var affectedPockets = Pockets.Where(x => x.Labels.Contains(tagControlLabel));
								var remainingPockets = Pockets.Except(affectedPockets);
								var tagsToFade = _currentTags.Where(x => !remainingPockets.Any(y => y.Labels.Contains(x.DataContext)));

								foreach (var control in tagsToFade)
								{
									control.Opacity = x ? 0.3 : 1;
								}
							})
							.DisposeWith(_disposable);
					}
				}

				await Task.Delay(500);
			}
		});
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();

		_isRunning = false;
	}
}
