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
			Finder	TopLeftFinder,
			Finder	TopRightFinder,
			Finder	BottomLeftFinder
			)
		{
		// save three finders
		this.TopLeftFinder = TopLeftFinder;
		this.TopRightFinder = TopRightFinder;
		this.BottomLeftFinder = BottomLeftFinder;

		// top line slope
		TopLineDeltaX = TopRightFinder.Col - TopLeftFinder.Col;
		TopLineDeltaY = TopRightFinder.Row - TopLeftFinder.Row;

		// top line length
		TopLineLength = Math.Sqrt(TopLineDeltaX * TopLineDeltaX + TopLineDeltaY * TopLineDeltaY);

		// left line slope
		LeftLineDeltaX = BottomLeftFinder.Col - TopLeftFinder.Col;
		LeftLineDeltaY = BottomLeftFinder.Row - TopLeftFinder.Row;

		// left line length
		LeftLineLength = Math.Sqrt(LeftLineDeltaX * LeftLineDeltaX + LeftLineDeltaY * LeftLineDeltaY);
		return;
		}

	/////////////////////////////////////////////////////////////////////
	// Test QR corner for validity
	/////////////////////////////////////////////////////////////////////

	internal static Corner CreateCorner
			(
			Finder	TopLeftFinder,
			Finder	TopRightFinder,
			Finder	BottomLeftFinder
			)
		{
		// try all three possible permutation of three finders
		for(int Index = 0; Index < 3; Index++)
			{
			// TestCorner runs three times to test all posibilities
			// rotate top left, top right and bottom left
			if(Index != 0)
				{
				Finder Temp = TopLeftFinder;
				TopLeftFinder = TopRightFinder;
				TopRightFinder = BottomLeftFinder;
				BottomLeftFinder = Temp;
				}

			// top line slope
			double TopLineDeltaX = TopRightFinder.Col - TopLeftFinder.Col;
			double TopLineDeltaY = TopRightFinder.Row - TopLeftFinder.Row;

			// left line slope
			double LeftLineDeltaX = BottomLeftFinder.Col - TopLeftFinder.Col;
			double LeftLineDeltaY = BottomLeftFinder.Row - TopLeftFinder.Row;

			// top line length
			double TopLineLength = Math.Sqrt(TopLineDeltaX * TopLineDeltaX + TopLineDeltaY * TopLineDeltaY);

			// left line length
			double LeftLineLength = Math.Sqrt(LeftLineDeltaX * LeftLineDeltaX + LeftLineDeltaY * LeftLineDeltaY);

			// the short side must be at least 80% of the long side
			if(Math.Min(TopLineLength, LeftLineLength) < QRDecoder.CORNER_SIDE_LENGTH_DEV * Math.Max(TopLineLength, LeftLineLength)) continue;

			// top line vector
			double TopLineSin = TopLineDeltaY / TopLineLength;
			double TopLineCos = TopLineDeltaX / TopLineLength;

			// rotate lines such that top line is parallel to x axis
			// left line after rotation
			double NewLeftX = TopLineCos * LeftLineDeltaX + TopLineSin * LeftLineDeltaY;
			double NewLeftY = -TopLineSin * LeftLineDeltaX + TopLineCos * LeftLineDeltaY;

			// new left line X should be zero (or between +/- 4 deg)
			if(Math.Abs(NewLeftX / LeftLineLength) > QRDecoder.CORNER_RIGHT_ANGLE_DEV) continue;

			// swap top line with left line
			if(NewLeftY < 0)
				{
				// swap top left with bottom right
				Finder TempFinder = TopRightFinder;
				TopRightFinder = BottomLeftFinder;
				BottomLeftFinder = TempFinder;
				}

			return new Corner(TopLeftFinder, TopRightFinder, BottomLeftFinder);
			}
		return null;
		}

	/////////////////////////////////////////////////////////////////////
	// Test QR corner for validity
	/////////////////////////////////////////////////////////////////////

	internal int InitialVersionNumber()
		{
		// version number based on top line
		double TopModules = 7;

		// top line is mostly horizontal
		if(Math.Abs(TopLineDeltaX) >= Math.Abs(TopLineDeltaY))
			{
			TopModules += TopLineLength * TopLineLength /
				(Math.Abs(TopLineDeltaX) * 0.5 * (TopLeftFinder.HModule + TopRightFinder.HModule));			
			}

		// top line is mostly vertical
		else
			{
			TopModules += TopLineLength * TopLineLength /
				(Math.Abs(TopLineDeltaY) * 0.5 * (TopLeftFinder.VModule + TopRightFinder.VModule));			
			}

		// version number based on left line
		double LeftModules = 7;

		// Left line is mostly vertical
		if(Math.Abs(LeftLineDeltaY) >= Math.Abs(LeftLineDeltaX))
			{
			LeftModules += LeftLineLength * LeftLineLength /
				(Math.Abs(LeftLineDeltaY) * 0.5 * (TopLeftFinder.VModule + BottomLeftFinder.VModule));			
			}

		// left line is mostly horizontal
		else
			{
			LeftModules += LeftLineLength * LeftLineLength /
				(Math.Abs(LeftLineDeltaX) * 0.5 * (TopLeftFinder.HModule + BottomLeftFinder.HModule));			
			}

		// version (there is rounding in the calculation)
		int Version = ((int) Math.Round(0.5 * (TopModules + LeftModules)) - 15) / 4;

		// not a valid corner
		if(Version < 1 || Version > 40) throw new ApplicationException("Corner is not valid (version number must be 1 to 40)");

		// exit with version number
		return Version;
		}
	}
}
