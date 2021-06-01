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
