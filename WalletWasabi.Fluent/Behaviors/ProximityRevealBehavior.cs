using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class ProximityRevealBehavior : Avalonia.Xaml.Interactions.Custom.AttachedToVisualTreeBehavior<Control>
{
    public static readonly StyledProperty<Thickness> InflateHitboxesByProperty = AvaloniaProperty.Register<ProximityRevealBehavior, Thickness>(nameof(InflateHitBoxesBy));

    public static readonly StyledProperty<bool> ForceVisibleProperty = AvaloniaProperty.Register<ProximityRevealBehavior, bool>(nameof(ForceVisible));

    [ResolveByName] public Visual? Target { get; set; }

    public Thickness InflateHitBoxesBy
    {
        get => GetValue(InflateHitboxesByProperty);
        set => SetValue(InflateHitboxesByProperty, value);
    }

    public bool ForceVisible
    {
        get => GetValue(ForceVisibleProperty);
        set => SetValue(ForceVisibleProperty, value);
    }

    protected override void OnAttachedToVisualTree(CompositeDisposable disposable)
    {
        if (AssociatedObject is null)
        {
            return;
        }

        var mainView = GetMainView();

        if (mainView is null)
        {
            return;
        }

        var pointerPos = Observable
            .FromEventPattern<PointerEventArgs>(handler => mainView.PointerMoved += handler, handler => mainView.PointerMoved -= handler)
            .Select(x => x.EventArgs.GetPosition(mainView));

        var hits = pointerPos.Select(point =>
        {
            if (Target is null)
            {
                return false;
            }

            return ContainsPoint(mainView, point, AssociatedObject) || ContainsPoint(mainView, point, Target);
        });

        var isVisibilityForced = this.WhenAnyValue(x => x.ForceVisible);

        hits.CombineLatest(isVisibilityForced, (isHit, isForced) => (isHit, isForced))
			.Select(tuple => tuple.isHit && AssociatedObject.IsEffectivelyEnabled || tuple.isForced)
			.StartWith(false)
            .Do(isVisible => Target!.IsVisible = isVisible)
            .Subscribe()
            .DisposeWith(disposable);
    }

    private static Control? GetMainView()
    {
        return Application.Current!.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime => classicDesktopStyleApplicationLifetime.MainWindow,
            ISingleViewApplicationLifetime singleViewApplicationLifetime => singleViewApplicationLifetime.MainView,
            _ => null
        };
    }

    private bool ContainsPoint(Visual reference, Point referencePoint, Visual toCheck)
    {
        var translatePoint = toCheck.TranslatePoint(new Point(), reference);

        if (translatePoint is not { } p)
        {
            return false;
        }

        var finalBounds = new Rect(p, toCheck.Bounds.Size).Inflate(InflateHitBoxesBy);
        return finalBounds.Contains(referencePoint);
    }
}
