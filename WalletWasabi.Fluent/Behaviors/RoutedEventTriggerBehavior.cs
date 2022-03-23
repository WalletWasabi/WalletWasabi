using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors
{
	/// <summary>
	/// A behavior that listens for a <see cref="RoutedEvent"/> event on its source and executes its actions when that event is fired.
	/// </summary>
	public class RoutedEventTriggerBehavior : Trigger<Interactive>
	{
		/// <summary>
		/// Identifies the <seealso cref="RoutedEvent"/> avalonia property.
		/// </summary>
		public static readonly StyledProperty<RoutedEvent?> RoutedEventProperty =
			AvaloniaProperty.Register<RoutedEventTriggerBehavior, RoutedEvent?>(nameof(RoutedEvent));

		/// <summary>
		/// Identifies the <seealso cref="RoutingStrategies"/> avalonia property.
		/// </summary>
		public static readonly StyledProperty<RoutingStrategies> RoutingStrategiesProperty =
			AvaloniaProperty.Register<RoutedEventTriggerBehavior, RoutingStrategies>(nameof(RoutingStrategies), RoutingStrategies.Direct | RoutingStrategies.Bubble);

		/// <summary>
		/// Identifies the <seealso cref="SourceInteractive"/> avalonia property.
		/// </summary>
		public static readonly StyledProperty<Interactive?> SourceInteractiveProperty =
			AvaloniaProperty.Register<RoutedEventTriggerBehavior, Interactive?>(nameof(SourceInteractive));

		private bool _isInitialized;
		private bool _isAttached;

		/// <summary>
		/// Gets or sets routing event to listen for. This is a avalonia property.
		/// </summary>
		public RoutedEvent? RoutedEvent
		{
			get => GetValue(RoutedEventProperty);
			set => SetValue(RoutedEventProperty, value);
		}

		/// <summary>
		/// Gets or sets the routing event <see cref="RoutingStrategies"/>. This is a avalonia property.
		/// </summary>
		public RoutingStrategies RoutingStrategies
		{
			get => GetValue(RoutingStrategiesProperty);
			set => SetValue(RoutingStrategiesProperty, value);
		}

		/// <summary>
		/// Gets or sets the source object from which this behavior listens for events.
		/// If <seealso cref="SourceInteractive"/> is not set, the source will default to <seealso cref="Behavior.AssociatedObject"/>. This is a avalonia property.
		/// </summary>
		public Interactive? SourceInteractive
		{
			get => GetValue(SourceInteractiveProperty);
			set => SetValue(SourceInteractiveProperty, value);
		}

		static RoutedEventTriggerBehavior()
		{
			RoutedEventProperty.Changed.Subscribe(OnValueChanged);
			RoutingStrategiesProperty.Changed.Subscribe(OnValueChanged);
			SourceInteractiveProperty.Changed.Subscribe(OnValueChanged);
		}

		private static void OnValueChanged(AvaloniaPropertyChangedEventArgs args)
		{
			if (args.Sender is not RoutedEventTriggerBehavior behavior || behavior.AssociatedObject is null)
			{
				return;
			}

			if (behavior._isInitialized && behavior._isAttached)
			{
				behavior.RemoveHandler();
				behavior.AddHandler();
			}
		}

		/// <inheritdoc />
		protected override void OnAttachedToVisualTree()
		{
			_isAttached = true;
			AddHandler();
		}

		/// <inheritdoc />
		protected override void OnDetachedFromVisualTree()
		{
			_isAttached = false;
			RemoveHandler();
		}

		private void AddHandler()
		{
			var interactive = ComputeResolvedSourceInteractive();
			if (interactive is { } && RoutedEvent is { })
			{
				interactive.AddHandler(RoutedEvent, Handler, RoutingStrategies);
				_isInitialized = true;
			}
		}

		private void RemoveHandler()
		{
			var interactive = ComputeResolvedSourceInteractive();
			if (interactive is { } && RoutedEvent is { } && _isInitialized)
			{
				interactive.RemoveHandler(RoutedEvent, Handler);
				_isInitialized = false;
			}
		}

		private Interactive? ComputeResolvedSourceInteractive()
		{
			return GetValue(SourceInteractiveProperty) is { } ? SourceInteractive : AssociatedObject;
		}

		private void Handler(object? sender, RoutedEventArgs e)
		{
			var interactive = ComputeResolvedSourceInteractive();
			if (interactive is { })
			{
				Interaction.ExecuteActions(interactive, Actions, e);
			}
		}
	}
}