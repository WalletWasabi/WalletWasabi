using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Styling;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using WalletWasabi.Gui.Models;

namespace WalletWasabi.Gui.Controls
{
	public class Shields : TemplatedControl, IStyleable
	{
		public static readonly StyledProperty<bool> IsPrivacyCriticalVisibleProperty =
			AvaloniaProperty.Register<Shields, bool>(nameof(IsPrivacyCriticalVisible), defaultBindingMode: BindingMode.OneWay);

		public static readonly StyledProperty<bool> IsPrivacySomeVisibleProperty =
			AvaloniaProperty.Register<Shields, bool>(nameof(IsPrivacySomeVisible), defaultBindingMode: BindingMode.OneWay);

		public static readonly StyledProperty<bool> IsPrivacyFineVisibleProperty =
			AvaloniaProperty.Register<Shields, bool>(nameof(IsPrivacyFineVisible), defaultBindingMode: BindingMode.OneWay);

		public static readonly StyledProperty<bool> IsPrivacyStrongVisibleProperty =
			AvaloniaProperty.Register<Shields, bool>(nameof(IsPrivacyStrongVisible), defaultBindingMode: BindingMode.OneWay);

		public static readonly StyledProperty<bool> IsPrivacySaiyanVisibleProperty =
			AvaloniaProperty.Register<Shields, bool>(nameof(IsPrivacySaiyanVisible), defaultBindingMode: BindingMode.OneWay);

		public static readonly StyledProperty<ShieldState> ShieldStateProperty =
			AvaloniaProperty.Register<Shields, ShieldState>(nameof(ShieldState), defaultBindingMode: BindingMode.OneWay);

		public Shields()
		{
			this.GetObservable(ShieldStateProperty)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x => ShieldState = x);

			this.WhenAnyValue(x => x.ShieldState)
				.Where(x => x != null)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(st =>
				{
					IsPrivacyCriticalVisible = st.IsPrivacyCriticalVisible;
					IsPrivacySomeVisible = st.IsPrivacySomeVisible;
					IsPrivacyFineVisible = st.IsPrivacyFineVisible;
					IsPrivacyStrongVisible = st.IsPrivacyStrongVisible;
					IsPrivacySaiyanVisible = st.IsPrivacySaiyanVisible;
				});
		}

		Type IStyleable.StyleKey => typeof(Shields);

		public bool IsPrivacyCriticalVisible
		{
			get => GetValue(IsPrivacyCriticalVisibleProperty);
			set => SetValue(IsPrivacyCriticalVisibleProperty, value);
		}

		public bool IsPrivacySomeVisible
		{
			get => GetValue(IsPrivacySomeVisibleProperty);
			set => SetValue(IsPrivacySomeVisibleProperty, value);
		}

		public bool IsPrivacyFineVisible
		{
			get => GetValue(IsPrivacyFineVisibleProperty);
			set => SetValue(IsPrivacyFineVisibleProperty, value);
		}

		public bool IsPrivacyStrongVisible
		{
			get => GetValue(IsPrivacyStrongVisibleProperty);
			set => SetValue(IsPrivacyStrongVisibleProperty, value);
		}

		public bool IsPrivacySaiyanVisible
		{
			get => GetValue(IsPrivacySaiyanVisibleProperty);
			set => SetValue(IsPrivacySaiyanVisibleProperty, value);
		}

		public ShieldState ShieldState
		{
			get => GetValue(ShieldStateProperty);
			set => SetValue(ShieldStateProperty, value);
		}
	}
}
