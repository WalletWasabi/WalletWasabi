using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Media.Transformation;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Defines how a TransformOperations property should be animated.
	/// </summary>
	/// <remarks>
	/// Workaround for https://github.com/AvaloniaUI/Avalonia/issues/6494
	/// </remarks>
	public class NavBarTransformOperationsTransition : Transition<ITransform>
	{
		public NavBarTransformOperationsTransition()
		{
			Property = Visual.RenderTransformProperty;
		}

		/// <inheritdoc/>
		public override IObservable<ITransform> DoTransition(
			IObservable<double> progress,
			ITransform oldValue,
			ITransform newValue)
		{
			return progress.Select(p =>
			{
				var f = Easing.Ease(p);
				var builder = new TransformOperations.Builder(1);
				var matrix1 = (oldValue as TransformOperations)?.Value ?? Matrix.Identity;
				var matrix2 = (newValue as TransformOperations)?.Value ?? Matrix.Identity;
				var result = new Matrix(
					matrix1.M11 + (matrix2.M11 - matrix1.M11) * f,
					matrix1.M12 + (matrix2.M12 - matrix1.M12) * f,
					matrix1.M21 + (matrix2.M21 - matrix1.M21) * f,
					matrix1.M22 + (matrix2.M22 - matrix1.M22) * f,
					matrix1.M31 + (matrix2.M31 - matrix1.M31) * f,
					matrix1.M32 + (matrix2.M32 - matrix1.M32) * f);

				builder.AppendMatrix(result);

				return builder.Build();
			});
		}
	}
}