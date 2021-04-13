using System;
using Avalonia;

namespace WalletWasabi.Fluent.Helpers
{
	public class StraightLineFormula
	{
		private double _m; // Gradient
		private double _c; // Intercept
		private double _y; // Used for calculating the intercept, NOT for the getXforY and getYforX functions

		#region Constructors

		/// <summary>
		/// Standard constructor, sets M & C to zero.
		/// </summary>
		public StraightLineFormula()
		{
			M = 1;
			C = 0;
			Y = 1;
		}

		/// <summary>
		/// Standard constructor, sets M & C specified values.
		/// </summary>
		public StraightLineFormula(double m, double c)
		{
			M = m;
			C = c;
			Y = 1;
		}

		/// <summary>
		/// Standard constructor, sets M & C specified values.
		/// </summary>
		public StraightLineFormula(double m, double c, double y)
		{
			M = m;
			C = c;
			Y = y;
		}

		#endregion

		#region Getters & Setters

		/// <summary>
		/// Used for intersects
		/// </summary>
		private double Y
		{
			get { return _y; }
			set { _y = value; }
		}

		/// <summary>
		/// The gradient
		/// </summary>
		public double M
		{
			get { return _m; }
			set { _m = value; }
		}

		/// <summary>
		/// The Y intercept
		/// </summary>
		public double C
		{
			get { return _c; }
			set { _c = value; }
		}

		#endregion

		public static StraightLineFormula operator *(StraightLineFormula f, double multiplier)
		{
			return new StraightLineFormula((multiplier * f.M), (multiplier * f.C), (multiplier * f.Y));
		}

		public static StraightLineFormula operator -(StraightLineFormula f, StraightLineFormula g)
		{
			return new StraightLineFormula((f.M - g.M), (f.C - g.C), (f.Y - g.Y));
		}

		public static StraightLineFormula operator /(StraightLineFormula f, double divisor)
		{
			return new StraightLineFormula((f.M / divisor), (f.C / divisor), (f.Y / divisor));
		}

		public static Point IntersectionBetween(StraightLineFormula f, StraightLineFormula g)
		{
			return new Point((int) XIntersectionBetween(f, g), (int) YIntersectionBetween(f, g));
		}

		public static double YIntersectionBetween(StraightLineFormula f, StraightLineFormula g)
		{
			StraightLineFormula tempA = (f * g.M) - (g * f.M);
			tempA = tempA / tempA.Y;
			return tempA.C;
		}

		public override string ToString()
		{
			return "y = " + M.ToString("#00.00") + "x + " + C.ToString("#00.00");
		}

		public static double XIntersectionBetween(StraightLineFormula f, StraightLineFormula g)
		{
			double y = YIntersectionBetween(f, g);
			return (y - f.C) / f.M;
		}

		/// <summary>
		/// Calculates M & C from passed parameters
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="x"></param>
		public void CalculateFrom(Point a, Point b, int x)
		{
			CalculateFrom(a.X, b.X, a.Y, b.Y, x);
		}

		public void CalculateFrom(double x1, double x2, double y1, double y2)
		{
			CalculateFrom(x1, x2, y1, y2, x1);
		}

		/// <summary>
		/// Calculates M & C from passed parameters
		/// </summary>
		/// <param name="x1">X coord 1</param>
		/// <param name="x2">X coord 2</param>
		/// <param name="y1">Y coord 1</param>
		/// <param name="y2">Y coord 2</param>
		/// <param name="x"></param>
		public void CalculateFrom(double x1, double x2, double y1, double y2, double x)
		{
			CalculateMFrom(x1, x2, y1, y2);

			// c = y - (m*x)
			_c = y1 - (_m * x);
		}

		/// <summary>
		/// Calculates M from passed parameters
		/// </summary>
		/// <param name="x1"></param>
		/// <param name="x2"></param>
		/// <param name="y1"></param>
		/// <param name="y2"></param>
		public void CalculateMFrom(double x1, double x2, double y1, double y2)
		{
			// calculating straight line formula
			if ((y2 - y1) != 0 && (x2 - x1) != 0)
			{
				_m = (y2 - y1) / (x2 - x1);
			}
		}

		/// <summary>
		/// Gets Y for this formula
		/// </summary>
		/// <param name="x">Value for X</param>
		/// <returns>Calculated value for Y</returns>
		public double GetYforX(double x)
		{
			return ((_m * x) + _c);
		}

		/// <summary>
		/// Gets Y for this formula
		/// </summary>
		/// <param name="x">Value for X</param>
		/// <returns>Calculated value for Y</returns>
		public double GetYforX(int x)
		{
			return GetYforX((double) x);
		}

		public void DoRegression(double[] values)
		{
			double xAvg = 0;
			double yAvg = 0;

			for (int x = 0; x < values.Length; x++)
			{
				xAvg += x;
				yAvg += values[x];
			}

			xAvg = xAvg / values.Length;
			yAvg = yAvg / values.Length;

			double v1 = 0;
			double v2 = 0;

			for (int x = 0; x < values.Length; x++)
			{
				v1 += (x - xAvg) * (values[x] - yAvg);
				v2 += Math.Pow(x - xAvg, 2);
			}

			M = v1 / v2;
			C = yAvg - M * xAvg;
		}

		/// <summary>
		/// Gets X for this formula
		/// </summary>
		/// <param name="y">Value for Y</param>
		/// <returns>Calculated value for X</returns>
		public double GetXforY(double y)
		{
			return (y - _c) / M;
		}

		/// <summary>
		/// Gets X for this formula
		/// </summary>
		/// <param name="y">Value for Y</param>
		/// <returns>Calculated value for X</returns>
		public double GetXforY(int y)
		{
			return GetXforY((double) y);
		}
	}
}