using System;

namespace WalletWasabi.Fluent.QRCodeDecoder
{
	internal class Corner
	{
		internal Finder TopLeftFinder;
		internal Finder TopRightFinder;
		internal Finder BottomLeftFinder;

		internal double TopLineDeltaX;
		internal double TopLineDeltaY;
		internal double TopLineLength;
		internal double LeftLineDeltaX;
		internal double LeftLineDeltaY;
		internal double LeftLineLength;

		private Corner(Finder topLeftFinder, Finder topRightFinder, Finder bottomLeftFinder)
		{
			// save three finders
			TopLeftFinder = topLeftFinder;
			TopRightFinder = topRightFinder;
			BottomLeftFinder = bottomLeftFinder;

			// top line slope
			TopLineDeltaX = topRightFinder.Col - topLeftFinder.Col;
			TopLineDeltaY = topRightFinder.Row - topLeftFinder.Row;

			// top line length
			TopLineLength = Math.Sqrt(TopLineDeltaX * TopLineDeltaX + TopLineDeltaY * TopLineDeltaY);

			// left line slope
			LeftLineDeltaX = bottomLeftFinder.Col - topLeftFinder.Col;
			LeftLineDeltaY = bottomLeftFinder.Row - topLeftFinder.Row;

			// left line length
			LeftLineLength = Math.Sqrt(LeftLineDeltaX * LeftLineDeltaX + LeftLineDeltaY * LeftLineDeltaY);
		}

		internal static Corner? CreateCorner(Finder topLeftFinder, Finder topRightFinder, Finder bottomLeftFinder)
		{
			// try all three possible permutation of three finders
			for (int index = 0; index < 3; index++)
			{
				// TestCorner runs three times to test all posibilities
				// rotate top left, top right and bottom left
				if (index != 0)
				{
					Finder temp = topLeftFinder;
					topLeftFinder = topRightFinder;
					topRightFinder = bottomLeftFinder;
					bottomLeftFinder = temp;
				}

				// top line slope
				double topLineDeltaX = topRightFinder.Col - topLeftFinder.Col;
				double topLineDeltaY = topRightFinder.Row - topLeftFinder.Row;

				// left line slope
				double leftLineDeltaX = bottomLeftFinder.Col - topLeftFinder.Col;
				double leftLineDeltaY = bottomLeftFinder.Row - topLeftFinder.Row;

				double topLineLength = Math.Sqrt(topLineDeltaX * topLineDeltaX + topLineDeltaY * topLineDeltaY);

				double leftLineLength = Math.Sqrt(leftLineDeltaX * leftLineDeltaX + leftLineDeltaY * leftLineDeltaY);

				// the short side must be at least 80% of the long side
				if (Math.Min(topLineLength, leftLineLength) < QRDecoder.CORNER_SIDE_LENGTH_DEV * Math.Max(topLineLength, leftLineLength))
				{
					continue;
				}

				// top line vector
				double topLineSin = topLineDeltaY / topLineLength;
				double topLineCos = topLineDeltaX / topLineLength;

				// rotate lines such that top line is parallel to x axis
				// left line after rotation
				double newLeftX = topLineCos * leftLineDeltaX + topLineSin * leftLineDeltaY;
				double newLeftY = -topLineSin * leftLineDeltaX + topLineCos * leftLineDeltaY;

				// new left line X should be zero (or between +/- 4 deg)
				if (Math.Abs(newLeftX / leftLineLength) > QRDecoder.CORNER_RIGHT_ANGLE_DEV)
				{
					continue;
				}

				// swap top line with left line
				if (newLeftY < 0)
				{
					// swap top left with bottom right
					Finder tempFinder = topRightFinder;
					topRightFinder = bottomLeftFinder;
					bottomLeftFinder = tempFinder;
				}

				return new Corner(topLeftFinder, topRightFinder, bottomLeftFinder);
			}
			return null;
		}

		internal int InitialVersionNumber()
		{
			// version number based on top line
			double topModules = 7;

			// top line is mostly horizontal
			if (Math.Abs(TopLineDeltaX) >= Math.Abs(TopLineDeltaY))
			{
				topModules += TopLineLength * TopLineLength /
					(Math.Abs(TopLineDeltaX) * 0.5 * (TopLeftFinder.HModule + TopRightFinder.HModule));
			}
			else // top line is mostly vertical
			{
				topModules += TopLineLength * TopLineLength /
					(Math.Abs(TopLineDeltaY) * 0.5 * (TopLeftFinder.VModule + TopRightFinder.VModule));
			}

			// version number based on left line
			double leftModules = 7;

			// Left line is mostly vertical
			if (Math.Abs(LeftLineDeltaY) >= Math.Abs(LeftLineDeltaX))
			{
				leftModules += LeftLineLength * LeftLineLength /
					(Math.Abs(LeftLineDeltaY) * 0.5 * (TopLeftFinder.VModule + BottomLeftFinder.VModule));
			}
			else // left line is mostly horizontal
			{
				leftModules += LeftLineLength * LeftLineLength /
					(Math.Abs(LeftLineDeltaX) * 0.5 * (TopLeftFinder.HModule + BottomLeftFinder.HModule));
			}

			// version (there is rounding in the calculation)
			int version = ((int)Math.Round(0.5 * (topModules + leftModules)) - 15) / 4;

			// not a valid corner
			if (version < 1 || version > 40)
			{
				throw new ApplicationException("Corner is not valid (version number must be 1 to 40)");
			}

			// exit with version number
			return version;
		}
	}
}
