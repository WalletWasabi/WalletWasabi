using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Xaml.Interactions.Custom;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Transactions;

namespace WalletWasabi.Fluent.Behaviors;

public class PrivacyWarningFadeOutBehavior : AttachedToVisualTreeBehavior<Control>
{
	private const string FadeOutClassName = "fadeout";

	public static readonly StyledProperty<IEnumerable<PrivacyWarning>> PreviewWarningsProperty =
		AvaloniaProperty.Register<PrivacyWarningFadeOutBehavior, IEnumerable<PrivacyWarning>>(nameof(PreviewWarnings));

	public IEnumerable<PrivacyWarning> PreviewWarnings
	{
		get => GetValue(PreviewWarningsProperty);
		set => SetValue(PreviewWarningsProperty, value);
	}

	protected override IDisposable OnAttachedToVisualTreeOverride()
	{
		if (AssociatedObject?.DataContext is not PrivacyWarning current)
		{
			return Disposable.Empty;
		}

		return this.WhenAnyValue(x => x.PreviewWarnings)
			.WhereNotNull()
			.Do(_ =>
			{
				var fadeOut = !PreviewWarnings.Any(p => p == current);
				if (fadeOut && !AssociatedObject.Classes.Contains(FadeOutClassName))
				{
					AssociatedObject.Classes.Add(FadeOutClassName);
				}
				else if (!fadeOut && AssociatedObject.Classes.Contains(FadeOutClassName))
				{
					AssociatedObject.Classes.Remove(FadeOutClassName);
				}
			})
			.Subscribe();
	}
}
