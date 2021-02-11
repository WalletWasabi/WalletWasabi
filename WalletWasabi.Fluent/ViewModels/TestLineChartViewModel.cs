using System;
using System.Collections.Generic;
using WalletWasabi.Fluent.MathNet;

namespace WalletWasabi.Fluent.ViewModels
{
	public class TestLineChartViewModel
	{
		public double XAxisCurrentValue { get; set; } = 36;

		public double XAxisMinValue { get; set; } = 1;

		public double XAxisMaxValue { get; set; } = 1008;

		public List<string> XAxisLabels => new List<string>()
		{
			"1w",
			"3d",
			"1d",
			"12h",
			"6h",
			"3h",
			"1h",
			"30m",
			"20m",
			"fastest"
		};

		public List<double> XAxisValues => new List<double>()
		{
			1008,
			432,
			144,
			72,
			36,
			18,
			6,
			3,
			2,
			1,
		};

		public List<double> YAxisValues => new List<double>()
		{
			4,
			4,
			7,
			22,
			57,
			97,
			102,
			123,
			123,
			185
		};

		public void TestCubicSplineInterpolation()
		{
			double[] x = new double[13];
			double[] y = new double[13];
			Console.WriteLine($"X Y");
			for (int i = 0; i <= 12; i++)
			{
				x[i] = i * Math.PI / 12;
				y[i] = Math.Sin(x[i]);
				Console.WriteLine($"{x[i]} {y[i]}");
			}
			var spline = CubicSpline.InterpolateNaturalSorted(x, y);
			double testX = 1.5 * Math.PI / 12;
			double testY = spline.Interpolate(testX);
			Console.WriteLine($"Interpolated:");
			Console.WriteLine($"{testX} {testY}");
		}
	}
}
