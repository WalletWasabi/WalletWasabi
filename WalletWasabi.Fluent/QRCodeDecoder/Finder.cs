/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code finder class.
//
//	Author: Uzi Granot
//	Original Version: 1.0
//	Date: June 30, 2018
//	Copyright (C) 2018-2019 Uzi Granot. All Rights Reserved
//	For full version history please look at QRDecoder.cs
//
//	QR Code Library C# class library and the attached test/demo
//	applications are free software.
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

namespace WalletWasabi.Fluent.QRCodeDecoder
{
	internal class Finder
	{
		// horizontal scan
		internal int Row;

		internal int Col1;
		internal int Col2;
		internal double HModule;

		// vertical scan
		internal int Col;

		internal int Row1;
		internal int Row2;
		internal double VModule;

		internal double Distance = double.MaxValue;
		internal double ModuleSize;

		internal Finder(int row, int col1, int col2, double hModule)
		{
			Row = row;
			Col1 = col1;
			Col2 = col2;
			HModule = hModule;
		}

		internal void Match(int col, int row1, int row2, double vModule)
		{
			// test if horizontal and vertical are not related
			if (col < Col1 || col >= Col2 || Row < row1 || Row >= row2)
			{
				return;
			}

			// Module sizes must be about the same
			if (Math.Min(HModule, vModule) < Math.Max(HModule, vModule) * QRDecoder.MODULE_SIZE_DEVIATION)
			{
				return;
			}

			// calculate distance
			double deltaX = col - 0.5 * (Col1 + Col2);
			double deltaY = Row - 0.5 * (row1 + row2);
			double delta = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

			// distance between two points must be less than 2 pixels
			if (delta > QRDecoder.HOR_VERT_SCAN_MAX_DISTANCE)
			{
				return;
			}

			// new result is better than last result
			if (delta < Distance)
			{
				Col = col;
				Row1 = row1;
				Row2 = row2;
				VModule = vModule;
				ModuleSize = 0.5 * (HModule + vModule);
				Distance = delta;
			}
			return;
		}

		internal bool Overlap(Finder other)
		{
			return other.Col1 < Col2 && other.Col2 >= Col1 && other.Row1 < Row2 && other.Row2 >= Row1;
		}

		public override string ToString()
		{
			if (Distance == double.MaxValue)
			{
				return string.Format("Finder: Row: {0}, Col1: {1}, Col2: {2}, HModule: {3:0.00}", Row, Col1, Col2, HModule);
			}

			return string.Format("Finder: Row: {0}, Col: {1}, Module: {2:0.00}, Distance: {3:0.00}", Row, Col, ModuleSize, Distance);
		}
	}
}
