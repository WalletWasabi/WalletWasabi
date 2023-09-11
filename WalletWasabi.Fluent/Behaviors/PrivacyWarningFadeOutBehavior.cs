using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Transactions;

namespace WalletWasabi.Fluent.Behaviors;

public class PrivacyWarningFadeOutBehavior : AttachedToVisualTreeBehavior<Control>
{
	public static readonly StyledProperty<ObservableCollection<PrivacyWarning>> WarningsProperty =
		AvaloniaProperty.Register<PrivacyWarningFadeOutBehavior, ObservableCollection<PrivacyWarning>>(nameof(Warnings));

	public static readonly StyledProperty<ObservableCollection<PrivacyWarning>> PreviewWarningsProperty =
		AvaloniaProperty.Register<PrivacyWarningFadeOutBehavior, ObservableCollection<PrivacyWarning>>(nameof(PreviewWarnings));

	public ObservableCollection<PrivacyWarning> Warnings
	{
		get => GetValue(WarningsProperty);
		set => SetValue(WarningsProperty, value);
	}

	public ObservableCollection<PrivacyWarning> PreviewWarnings
	{
		get => GetValue(PreviewWarningsProperty);
		set => SetValue(PreviewWarningsProperty, value);
	}

	protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
	{
		if (AssociatedObject?.DataContext is not PrivacyWarning current)
		{
			return;
		}

		var warnings = Observable.FromEventPattern(Warnings, nameof(Warnings.CollectionChanged)).ToSignal();
		var previewWarnings = Observable.FromEventPattern(PreviewWarnings, nameof(PreviewWarnings.CollectionChanged)).ToSignal();

		warnings.Merge(previewWarnings)
				.Do(_ =>
				{
					var fadeOut = !PreviewWarnings.Any(p => p == current);
					if (fadeOut)
					{
						AssociatedObject.Classes.Add("fadeout");
					}
					else
					{
						AssociatedObject.Classes.Remove("fadeout");
					}
				})
				.Subscribe()
				.DisposeWith(disposable);
	}
}
