using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace WalletWasabi.Fluent.Controls
{
	public class ProgressRing : TemplatedControl
	{
		public static readonly StyledProperty<bool> IsIndeterminateProperty =
			AvaloniaProperty.Register<ProgressRing, bool>("IsIndeterminate");

		public static readonly StyledProperty<double> PercentageProperty =
			AvaloniaProperty.Register<ProgressRing, double>("Percentage");

		public static readonly StyledProperty<double> AngleProperty =
			AvaloniaProperty.Register<ProgressRing, double>("Angle");

		public static readonly StyledProperty<double> StrokeThicknessProperty =
			AvaloniaProperty.Register<ProgressRing, double>("StrokeThickness");

		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			UpdatePseudoClasses();
		}

		private void UpdatePseudoClasses()
		{
			PseudoClasses.Set(":indeterminate", IsIndeterminate);
		}

		public bool IsIndeterminate
		{
			get => GetValue(IsIndeterminateProperty);
			set => SetValue(IsIndeterminateProperty, value);
		}

		public double Percentage
		{
			get => GetValue(PercentageProperty);
			set => SetValue(PercentageProperty, value);
		}

		public double Angle
		{
			get => GetValue(AngleProperty);
			set => SetValue(AngleProperty, value);
		}

		public double StrokeThickness
		{
			get => GetValue(StrokeThicknessProperty);
			set => SetValue(StrokeThicknessProperty, value);
		}
	}
}
