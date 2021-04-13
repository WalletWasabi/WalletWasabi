/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code three finders corner class.
//
//	Author: Uzi Granot
//	Original Version: 1.0
//	Date: June 30, 2018
//	Copyright (C) 2018-2019 Uzi Granot. All Rights Reserved
//	For full version history please look at QRDecoder.cs
//
//	QR Code Library C# class library and the attached test/demo
//  applications are free software.
//	Software developed by this author is licensed under CPOL 1.02.
//	Some portions of the QRCodeVideoDecoder are licensed under GNU Lesser
//	General Public License v3.0.
//
//	The solution is made of 3 projects:
//	1. QRCodeDecoderLibrary: QR code decoding.
//	3. QRCodeDecoderDemo: Decode QR code image files.
//	4. QRCodeVideoDecoder: Decode QR code using web camera.
//		This demo program is using some of the source modules of
//		Camera_Net project published at CodeProject.com:
//		https://www.codeproject.com/Articles/671407/Camera_Net-Library
//		and at GitHub: https://github.com/free5lot/Camera_Net.
//		This project is based on DirectShowLib.
//		http://sourceforge.net/projects/directshownet/
//		This project includes a modified subset of the source modules.
//
//	The main points of CPOL 1.02 subject to the terms of the License are:
//
//	Source Code and Executable Files can be used in commercial applications;
//	Source Code and Executable Files can be redistributed; and
//	Source Code can be modified to create derivative works.
//	No claim of suitability, guarantee, or any warranty whatsoever is
//	provided. The software is provided "as-is".
//	The Article accompanying the Work may not be distributed or republished
//	without the Author's consent
//
//	For version history please refer to QRDecoder.cs
/////////////////////////////////////////////////////////////////////

using System;

namespace QRCodeDecoderLibrary
{
	/////////////////////////////////////////////////////////////////////
	// QR corner three finders pattern class
	/////////////////////////////////////////////////////////////////////

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

		/////////////////////////////////////////////////////////////////////
		// QR corner constructor
		/////////////////////////////////////////////////////////////////////

		private Corner
				(
				Finder topLeftFinder,
				Finder topRightFinder,
				Finder bottomLeftFinder
				)
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
			return;
		}

		/////////////////////////////////////////////////////////////////////
		// Test QR corner for validity
		/////////////////////////////////////////////////////////////////////

		internal static Corner? CreateCorner
				(
				Finder topLeftFinder,
				Finder topRightFinder,
				Finder bottomLeftFinder
				)
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

		/////////////////////////////////////////////////////////////////////
		// Test QR corner for validity
		/////////////////////////////////////////////////////////////////////

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

			// top line is mostly vertical
			else
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

			// left line is mostly horizontal
			else
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
