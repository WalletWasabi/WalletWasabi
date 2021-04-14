/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code decoder.
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
/////////////////////////////////////////////////////////////////////
//
//	Version History:
//
//	Version 1.0 2018/06/30
//		Original revision
//
//	Version 1.1 2018/07/20
//		Consolidate DirectShowLib into one module removing unused code
//
//	Version 2.0 2019/05/15
//		Split the combined QRCode encoder and decoder to two solutions.
//		Add support for .net standard.
//		Add save image to png file without Bitmap class.
//	Version 2.1 2019/07/22
//		Add support for ECI Assignment Value
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WalletWasabi.Logging;

namespace QRCodeDecoderLibrary
{
	public class QRDecoder
	{
		// Gets QR Code matrix version
		public int QRCodeVersion { get; internal set; }

		// Gets QR Code matrix dimension in bits
		public int QRCodeDimension { get; internal set; }

		// Gets QR Code error correction code (L, M, Q, H)
		public ErrorCorrection ErrorCorrection { get; internal set; }

		// Error correction percent (L, M, Q, H)
#pragma warning disable IDE1006 // Naming Styles
		public int[] ErrCorrPercent = new int[] { 7, 15, 25, 30 };
#pragma warning restore IDE1006 // Naming Styles

		// Get mask code (0 to 7)
		public int MaskCode { get; internal set; }

		// ECI Assignment Value
		public int ECIAssignValue { get; internal set; }

		internal int ImageWidth;
		internal int ImageHeight;
		internal bool[,] BlackWhiteImage = new bool[0, 0];
		internal List<Finder> FinderList = new();
		internal List<Finder> AlignList = new();
		internal List<string> DataArrayList = new();
		internal int MaxCodewords;
		internal int MaxDataCodewords;
		internal int MaxDataBits;
		internal int ErrCorrCodewords;
		internal int BlocksGroup1;
		internal int DataCodewordsGroup1;
		internal int BlocksGroup2;
		internal int DataCodewordsGroup2;

		internal byte[] CodewordsArray = Array.Empty<byte>();
		internal int CodewordsPtr;
		internal uint BitBuffer;
		internal int BitBufferLen;
		internal byte[,] BaseMatrix = new byte[1, 1];
		internal byte[,] MaskMatrix = new byte[1, 1];

		internal bool Trans4Mode;

		// transformation cooefficients from QR modules to image pixels
		internal double Trans3a;

		internal double Trans3b;
		internal double Trans3c;
		internal double Trans3d;
		internal double Trans3e;
		internal double Trans3f;

		// transformation matrix based on three finders plus one more point
		internal double Trans4a;

		internal double Trans4b;
		internal double Trans4c;
		internal double Trans4d;
		internal double Trans4e;
		internal double Trans4f;
		internal double Trans4g;
		internal double Trans4h;

		internal const double SIGNATURE_MAX_DEVIATION = 0.25;
		internal const double HOR_VERT_SCAN_MAX_DISTANCE = 2.0;
		internal const double MODULE_SIZE_DEVIATION = 0.5;
		internal const double CORNER_SIDE_LENGTH_DEV = 0.8;
		internal const double CORNER_RIGHT_ANGLE_DEV = 0.25; // about Sin(4 deg)
		internal const double ALIGNMENT_SEARCH_AREA = 0.3;

		// QRCode image decoder
		public IEnumerable<string> SearchQrCodes(Bitmap inputImage)
		{
			try
			{
				// empty data string output
				DataArrayList = new List<string>();

				// save image dimension
				ImageWidth = inputImage.Width;
				ImageHeight = inputImage.Height;

				// convert input image to black and white boolean image
				// horizontal search for finders
				if (!ConvertImageToBlackAndWhite(inputImage) || !HorizontalFindersSearch())
				{
					return Enumerable.Empty<string>();
				}

				// vertical search for finders
				VerticalFindersSearch();

				// remove unused finders
				if (!RemoveUnusedFinders())
				{
					return Enumerable.Empty<string>();
				}
			}
			catch (Exception e)
			{
				Logger.LogDebug(e);
			}

			// look for all possible 3 finder patterns
			int index1End = FinderList.Count - 2;
			int index2End = FinderList.Count - 1;
			int index3End = FinderList.Count;
			for (int index1 = 0; index1 < index1End; index1++)
			{
				for (int index2 = index1 + 1; index2 < index2End; index2++)
				{
					for (int index3 = index2 + 1; index3 < index3End; index3++)
					{
						try
						{
							// find 3 finders arranged in L shape
							Corner? corner = Corner.CreateCorner(FinderList[index1], FinderList[index2], FinderList[index3]);

							// not a valid corner
							// qr code version 1 has no alignment mark in other words decode failed
							if (corner is null || QRCodeVersion == 1)
							{
								continue;
							}

							// get corner info (version, error code and mask) continue if failed
							if (!GetQRCodeCornerInfo(corner))
							{
								continue;
							}

							// decode corner using three finders
							// continue if successful
							if (DecodeQRCodeCorner(corner))
							{
								continue;
							}

							// find bottom right alignment mark
							// continue if failed
							if (!FindAlignmentMark(corner))
							{
								continue;
							}

							// decode using 4 points
							if (AlignList is { })
							{
								foreach (Finder align in AlignList)
								{
									// calculate transformation based on 3 finders and bottom right alignment mark
									SetTransMatrix(corner, align.Row, align.Col);

									// decode corner using three finders and one alignment mark
									if (DecodeQRCodeCorner(corner))
									{
										break;
									}
								}
							}
						}
						catch (Exception e)
						{
							Logger.LogError(e);
						}
					}
				}
			}

			// not found exit
			if (DataArrayList.Count == 0)
			{
				return Enumerable.Empty<string>();
			}

			// successful exit
			return DataArrayList.ToArray();
		}

		// Convert image to black and white boolean matrix
		internal bool ConvertImageToBlackAndWhite(Bitmap inputImage)
		{
			// lock image bits
			BitmapData bitmapData = inputImage.LockBits(new Rectangle(0, 0, ImageWidth, ImageHeight),
				ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

			// address of first line
			IntPtr bitArrayPtr = bitmapData.Scan0;

			// length in bytes of one scan line
			int scanLineWidth = bitmapData.Stride;
			if (scanLineWidth < 0)
			{
				return false;
			}

			// image total bytes
			int totalBytes = scanLineWidth * ImageHeight;
			byte[] bitmapArray = new byte[totalBytes];

			// Copy the RGB values into the array.
			Marshal.Copy(bitArrayPtr, bitmapArray, 0, totalBytes);

			// unlock image
			inputImage.UnlockBits(bitmapData);

			// allocate gray image
			byte[,] grayImage = new byte[ImageHeight, ImageWidth];
			int[] grayLevel = new int[256];

			// convert to gray
			int delta = scanLineWidth - 3 * ImageWidth;
			int bitmapPtr = 0;
			for (int row = 0; row < ImageHeight; row++)
			{
				for (int col = 0; col < ImageWidth; col++)
				{
					int module = (30 * bitmapArray[bitmapPtr] + 59 * bitmapArray[bitmapPtr + 1] + 11 * bitmapArray[bitmapPtr + 2]) / 100;
					grayLevel[module]++;
					grayImage[row, col] = (byte)module;
					bitmapPtr += 3;
				}
				bitmapPtr += delta;
			}

			// gray level cutoff between black and white
			int levelStart = 0;
			int levelEnd = 255;

			while (levelStart < 256 && grayLevel[levelStart] == 0)
			{
				levelStart++;
			}

			while (levelEnd >= levelStart && grayLevel[levelEnd] == 0)
			{
				levelEnd--;
			}

			levelEnd++;
			if (levelEnd - levelStart < 2)
			{
				return false;
			}

			int cutoffLevel = (levelStart + levelEnd) / 2;

			// create boolean image white = false, black = true
			BlackWhiteImage = new bool[ImageHeight, ImageWidth];
			for (int row = 0; row < ImageHeight; row++)
			{
				for (int col = 0; col < ImageWidth; col++)
				{
					BlackWhiteImage[row, col] = grayImage[row, col] < cutoffLevel;
				}
			}

			return true;
		}

		// search row by row for finders blocks
		internal bool HorizontalFindersSearch()
		{
			// create empty finders list
			FinderList = new List<Finder>();

			// look for finder patterns
			int[] colPos = new int[ImageWidth + 1];
			int posPtr = 0;

			// scan one row at a time
			for (int row = 0; row < ImageHeight; row++)
			{
				// look for first black pixel
				int col = 0;
				while (col < ImageWidth && !BlackWhiteImage[row, col])
				{
					col++;
				}

				// first black
				posPtr = 0;
				colPos[posPtr++] = col;

				// loop for pairs
				while (true)
				{
					// look for next white
					// if black is all the way to the edge, set next white after the edge
					while (col < ImageWidth && BlackWhiteImage[row, col])
					{
						col++;
					}

					colPos[posPtr++] = col;
					if (col == ImageWidth)
					{
						break;
					}

					// look for next black
					while (col < ImageWidth && !BlackWhiteImage[row, col])
					{
						col++;
					}

					if (col == ImageWidth)
					{
						break;
					}

					colPos[posPtr++] = col;
				}

				// we must have at least 6 positions
				if (posPtr < 6)
				{
					continue;
				}

				// build length array
				int posLen = posPtr - 1;
				int[] len = new int[posLen];
				for (int ptr = 0; ptr < posLen; ptr++)
				{
					len[ptr] = colPos[ptr + 1] - colPos[ptr];
				}

				// test signature
				int sigLen = posPtr - 5;
				for (int sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
				{
					if (TestFinderSig(colPos, len, sigPtr, out double moduleSize))
					{
						FinderList.Add(new Finder(row, colPos[sigPtr + 2], colPos[sigPtr + 3], moduleSize));
					}
				}
			}

			// no finders found
			if (FinderList.Count < 3)
			{
				return false;
			}

			return true;
		}

		// search row by row for alignment blocks
		internal bool HorizontalAlignmentSearch(int areaLeft, int areaTop, int areaWidth, int areaHeight)
		{
			// create empty finders list
			AlignList = new List<Finder>();

			// look for finder patterns
			int[] colPos = new int[areaWidth + 1];
			int posPtr = 0;

			// area right and bottom
			int areaRight = areaLeft + areaWidth;
			int areaBottom = areaTop + areaHeight;

			// scan one row at a time
			for (int row = areaTop; row < areaBottom; row++)
			{
				// look for first black pixel
				int col = areaLeft;
				while (col < areaRight && !BlackWhiteImage[row, col])
				{
					col++;
				}

				if (col == areaRight)
				{
					continue;
				}

				// first black
				posPtr = 0;
				colPos[posPtr++] = col;

				// loop for pairs
				while (true)
				{
					// look for next white
					// if black is all the way to the edge, set next white after the edge
					while (col < areaRight && BlackWhiteImage[row, col])
					{
						col++;
					}

					colPos[posPtr++] = col;
					if (col == areaRight)
					{
						break;
					}

					// look for next black
					while (col < areaRight && !BlackWhiteImage[row, col])
					{
						col++;
					}

					if (col == areaRight)
					{
						break;
					}

					colPos[posPtr++] = col;
				}

				// we must have at least 6 positions
				if (posPtr < 6)
				{
					continue;
				}

				// build length array
				int posLen = posPtr - 1;
				int[] len = new int[posLen];
				for (int ptr = 0; ptr < posLen; ptr++)
				{
					len[ptr] = colPos[ptr + 1] - colPos[ptr];
				}

				// test signature
				int sigLen = posPtr - 5;
				for (int sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
				{
					if (TestAlignSig(colPos, len, sigPtr, out double moduleSize))
					{
						AlignList.Add(new Finder(row, colPos[sigPtr + 2], colPos[sigPtr + 3], moduleSize));
					}
				}
			}

			return AlignList.Count != 0;
		}

		// search column by column for finders blocks
		internal void VerticalFindersSearch()
		{
			// active columns
			bool[] activeColumn = new bool[ImageWidth];
			foreach (Finder hF in FinderList)
			{
				for (int col = hF.Col1; col < hF.Col2; col++)
				{
					activeColumn[col] = true;
				}
			}

			// look for finder patterns
			int[] rowPos = new int[ImageHeight + 1];
			int posPtr = 0;

			// scan one column at a time
			for (int col = 0; col < ImageWidth; col++)
			{
				// not active column
				if (!activeColumn[col])
				{
					continue;
				}

				// look for first black pixel
				int row = 0;
				while (row < ImageHeight && !BlackWhiteImage[row, col])
				{
					row++;
				}

				if (row == ImageWidth)
				{
					continue;
				}

				// first black
				posPtr = 0;
				rowPos[posPtr++] = row;

				// loop for pairs
				while (true)
				{
					// look for next white
					// if black is all the way to the edge, set next white after the edge
					while (row < ImageHeight && BlackWhiteImage[row, col])
					{
						row++;
					}

					rowPos[posPtr++] = row;
					if (row == ImageHeight)
					{
						break;
					}

					// look for next black
					while (row < ImageHeight && !BlackWhiteImage[row, col])
					{
						row++;
					}

					if (row == ImageHeight)
					{
						break;
					}

					rowPos[posPtr++] = row;
				}

				// we must have at least 6 positions
				if (posPtr < 6)
				{
					continue;
				}

				// build length array
				int posLen = posPtr - 1;
				int[] len = new int[posLen];
				for (int ptr = 0; ptr < posLen; ptr++)
				{
					len[ptr] = rowPos[ptr + 1] - rowPos[ptr];
				}

				// test signature
				int sigLen = posPtr - 5;
				for (int sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
				{
					if (!TestFinderSig(rowPos, len, sigPtr, out double moduleSize))
					{
						continue;
					}

					foreach (Finder hF in FinderList)
					{
						hF.Match(col, rowPos[sigPtr + 2], rowPos[sigPtr + 3], moduleSize);
					}
				}
			}
		}

		// Search column by column for finders blocks
		internal void VerticalAlignmentSearch(int areaLeft, int areaTop, int areaWidth, int areaHeight)
		{
			// active columns
			bool[] activeColumn = new bool[areaWidth];
			foreach (Finder hF in AlignList)
			{
				for (int col = hF.Col1; col < hF.Col2; col++)
				{
					activeColumn[col - areaLeft] = true;
				}
			}

			// look for finder patterns
			int[] rowPos = new int[areaHeight + 1];
			int posPtr = 0;

			// area right and bottom
			int areaRight = areaLeft + areaWidth;
			int areaBottom = areaTop + areaHeight;

			// scan one column at a time
			for (int col = areaLeft; col < areaRight; col++)
			{
				// not active column
				if (!activeColumn[col - areaLeft])
				{
					continue;
				}

				// look for first black pixel
				int row = areaTop;
				while (row < areaBottom && !BlackWhiteImage[row, col])
				{
					row++;
				}

				if (row == areaBottom)
				{
					continue;
				}

				// first black
				posPtr = 0;
				rowPos[posPtr++] = row;

				// loop for pairs
				while (true)
				{
					// look for next white
					// if black is all the way to the edge, set next white after the edge
					while (row < areaBottom && BlackWhiteImage[row, col])
					{
						row++;
					}

					rowPos[posPtr++] = row;
					if (row == areaBottom)
					{
						break;
					}

					// look for next black
					while (row < areaBottom && !BlackWhiteImage[row, col])
					{
						row++;
					}

					if (row == areaBottom)
					{
						break;
					}

					rowPos[posPtr++] = row;
				}

				// we must have at least 6 positions
				if (posPtr < 6)
				{
					continue;
				}

				// build length array
				int posLen = posPtr - 1;
				int[] len = new int[posLen];
				for (int ptr = 0; ptr < posLen; ptr++)
				{
					len[ptr] = rowPos[ptr + 1] - rowPos[ptr];
				}

				// test signature
				int sigLen = posPtr - 5;
				for (int sigPtr = 0; sigPtr < sigLen; sigPtr += 2)
				{
					if (!TestAlignSig(rowPos, len, sigPtr, out double moduleSize))
					{
						continue;
					}

					foreach (Finder hF in AlignList)
					{
						hF.Match(col, rowPos[sigPtr + 2], rowPos[sigPtr + 3], moduleSize);
					}
				}
			}
		}

		// search column by column for finders blocks
		internal bool RemoveUnusedFinders()
		{
			// remove all entries without a match
			for (int index = 0; index < FinderList.Count; index++)
			{
				if (FinderList[index].Distance == double.MaxValue)
				{
					FinderList.RemoveAt(index);
					index--;
				}
			}

			// list is now empty or has less than three finders
			if (FinderList.Count < 3)
			{
				return false;
			}

			// keep best entry for each overlapping area
			for (int index = 0; index < FinderList.Count; index++)
			{
				Finder finder = FinderList[index];
				for (int index1 = index + 1; index1 < FinderList.Count; index1++)
				{
					Finder finder1 = FinderList[index1];
					if (!finder.Overlap(finder1))
					{
						continue;
					}

					if (finder1.Distance < finder.Distance)
					{
						finder = finder1;
						FinderList[index] = finder;
					}
					FinderList.RemoveAt(index1);
					index1--;
				}
			}

			// list is now empty or has less than three finders
			if (FinderList.Count < 3)
			{
				return false;
			}

			return true;
		}

		// search column by column for finders blocks
		internal bool RemoveUnusedAlignMarks()
		{
			// remove all entries without a match
			for (int index = 0; index < AlignList.Count; index++)
			{
				if (AlignList[index].Distance == double.MaxValue)
				{
					AlignList.RemoveAt(index);
					index--;
				}
			}

			// keep best entry for each overlapping area
			for (int index = 0; index < AlignList.Count; index++)
			{
				Finder finder = AlignList[index];
				for (int index1 = index + 1; index1 < AlignList.Count; index1++)
				{
					Finder finder1 = AlignList[index1];
					if (!finder.Overlap(finder1))
					{
						continue;
					}

					if (finder1.Distance < finder.Distance)
					{
						finder = finder1;
						AlignList[index] = finder;
					}
					AlignList.RemoveAt(index1);
					index1--;
				}
			}

			return AlignList.Count != 0;
		}

		// test finder signature 1 1 3 1 1
		internal bool TestFinderSig(int[] pos, int[] len, int index, out double module)
		{
			module = (pos[index + 5] - pos[index]) / 7.0;
			double maxDev = SIGNATURE_MAX_DEVIATION * module;
			if (Math.Abs(len[index] - module) > maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 1] - module) > maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 2] - 3 * module) > maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 3] - module) > maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 4] - module) > maxDev)
			{
				return false;
			}

			return true;
		}

		// test alignment signature n 1 1 1 n
		internal bool TestAlignSig(int[] pos, int[] len, int index, out double module)
		{
			module = (pos[index + 4] - pos[index + 1]) / 3.0;
			double maxDev = SIGNATURE_MAX_DEVIATION * module;
			if (len[index] < module - maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 1] - module) > maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 2] - module) > maxDev)
			{
				return false;
			}

			if (Math.Abs(len[index + 3] - module) > maxDev)
			{
				return false;
			}

			if (len[index + 4] < module - maxDev)
			{
				return false;
			}

			return true;
		}

		internal bool GetQRCodeCornerInfo(Corner corner)
		{
			try
			{
				// initial version number
				QRCodeVersion = corner.InitialVersionNumber();

				// qr code dimension
				QRCodeDimension = 17 + 4 * QRCodeVersion;

				// set transformation matrix
				SetTransMatrix(corner);

				// if version number is 7 or more, get version code
				if (QRCodeVersion >= 7)
				{
					int version = GetVersionOne();
					if (version == 0)
					{
						version = GetVersionTwo();
						if (version == 0)
						{
							return false;
						}
					}

					// QR Code version number is different than initial version
					if (version != QRCodeVersion)
					{
						// initial version number and dimension
						QRCodeVersion = version;

						// qr code dimension
						QRCodeDimension = 17 + 4 * QRCodeVersion;

						// set transformation matrix
						SetTransMatrix(corner);
					}
				}

				// get format info arrays
				int formatInfo = GetFormatInfoOne();
				if (formatInfo < 0)
				{
					formatInfo = GetFormatInfoTwo();
					if (formatInfo < 0)
					{
						return false;
					}
				}

				// set error correction code and mask code
				ErrorCorrection = FormatInfoToErrCode(formatInfo >> 3);
				MaskCode = formatInfo & 7;
			}
			catch (Exception e)
			{
				Logger.LogError(e);
				return false;
			}

			return true;
		}

		// Search for QR Code version
		internal bool DecodeQRCodeCorner(Corner corner)
		{
			try
			{
				// create base matrix
				BuildBaseMatrix();

				// create data matrix and test fixed modules
				ConvertImageToMatrix();

				// based on version and format information
				// set number of data and error correction codewords length
				SetDataCodewordsLength();

				// apply mask as per get format information step
				ApplyMask(MaskCode);

				// unload data from binary matrix to byte format
				UnloadDataFromMatrix();

				// restore blocks (undo interleave)
				RestoreBlocks();

				// calculate error correction
				// in case of error try to correct it
				CalculateErrorCorrection();

				// decode data
				byte[] dataArray = DecodeData();
				var decoded = Encoding.UTF8.GetString(dataArray);
				DataArrayList.Add(decoded);

				return true;
			}
			catch (Exception e)
			{
				Logger.LogDebug(e);
				return false;
			}
		}

		internal void SetTransMatrix(Corner corner)
		{
			int bottomRightPos = QRCodeDimension - 4;

			// transformation matrix based on three finders
			double[,] matrix1 = new double[3, 4];
			double[,] matrix2 = new double[3, 4];

			// build matrix 1 for horizontal X direction
			matrix1[0, 0] = 3;
			matrix1[0, 1] = 3;
			matrix1[0, 2] = 1;
			matrix1[0, 3] = corner.TopLeftFinder.Col;

			matrix1[1, 0] = bottomRightPos;
			matrix1[1, 1] = 3;
			matrix1[1, 2] = 1;
			matrix1[1, 3] = corner.TopRightFinder.Col;

			matrix1[2, 0] = 3;
			matrix1[2, 1] = bottomRightPos;
			matrix1[2, 2] = 1;
			matrix1[2, 3] = corner.BottomLeftFinder.Col;

			// build matrix 2 for Vertical Y direction
			matrix2[0, 0] = 3;
			matrix2[0, 1] = 3;
			matrix2[0, 2] = 1;
			matrix2[0, 3] = corner.TopLeftFinder.Row;

			matrix2[1, 0] = bottomRightPos;
			matrix2[1, 1] = 3;
			matrix2[1, 2] = 1;
			matrix2[1, 3] = corner.TopRightFinder.Row;

			matrix2[2, 0] = 3;
			matrix2[2, 1] = bottomRightPos;
			matrix2[2, 2] = 1;
			matrix2[2, 3] = corner.BottomLeftFinder.Row;

			// solve matrix1
			SolveMatrixOne(matrix1);
			Trans3a = matrix1[0, 3];
			Trans3c = matrix1[1, 3];
			Trans3e = matrix1[2, 3];

			// solve matrix2
			SolveMatrixOne(matrix2);
			Trans3b = matrix2[0, 3];
			Trans3d = matrix2[1, 3];
			Trans3f = matrix2[2, 3];

			// reset trans 4 mode
			Trans4Mode = false;
		}

		internal void SolveMatrixOne(double[,] matrix)
		{
			for (int row = 0; row < 3; row++)
			{
				// If the element is zero, make it non zero by adding another row
				if (matrix[row, row] == 0)
				{
					int row1 = row + 1;
					while (row1 < 3 && matrix[row1, row] == 0)
					{
						row1++;
					}

					if (row1 == 3)
					{
						throw new ApplicationException("Solve linear equations failed");
					}

					for (int col = row; col < 4; col++)
					{
						matrix[row, col] += matrix[row1, col];
					}
				}

				// make the diagonal element 1.0
				for (int col = 3; col > row; col--)
				{
					matrix[row, col] /= matrix[row, row];
				}

				// subtract current row from next rows to eliminate one value
				for (int row1 = row + 1; row1 < 3; row1++)
				{
					for (int col = 3; col > row; col--)
					{
						matrix[row1, col] -= matrix[row, col] * matrix[row1, row];
					}
				}
			}

			// go up from last row and eliminate all solved values
			matrix[1, 3] -= matrix[1, 2] * matrix[2, 3];
			matrix[0, 3] -= matrix[0, 2] * matrix[2, 3];
			matrix[0, 3] -= matrix[0, 1] * matrix[1, 3];
		}

		// Get image pixel color
		internal bool GetModule(int row, int col)
		{
			// get module based on three finders
			if (!Trans4Mode)
			{
				int trans3Col = (int)Math.Round(Trans3a * col + Trans3c * row + Trans3e, 0, MidpointRounding.AwayFromZero);
				int trans3Row = (int)Math.Round(Trans3b * col + Trans3d * row + Trans3f, 0, MidpointRounding.AwayFromZero);

				return BlackWhiteImage[trans3Row, trans3Col];
			}

			// get module based on three finders plus one alignment mark
			double w = Trans4g * col + Trans4h * row + 1.0;
			int trans4Col = (int)Math.Round((Trans4a * col + Trans4b * row + Trans4c) / w, 0, MidpointRounding.AwayFromZero);
			int trans4Row = (int)Math.Round((Trans4d * col + Trans4e * row + Trans4f) / w, 0, MidpointRounding.AwayFromZero);

			return BlackWhiteImage[trans4Row, trans4Col];
		}

		// search row by row for finders blocks
		internal bool FindAlignmentMark(Corner corner)
		{
			// alignment mark estimated position
			int alignRow = QRCodeDimension - 7;
			int alignCol = QRCodeDimension - 7;
			int imageCol = (int)Math.Round(Trans3a * alignCol + Trans3c * alignRow + Trans3e, 0, MidpointRounding.AwayFromZero);
			int imageRow = (int)Math.Round(Trans3b * alignCol + Trans3d * alignRow + Trans3f, 0, MidpointRounding.AwayFromZero);

			// search area
			int side = (int)Math.Round(ALIGNMENT_SEARCH_AREA * (corner.TopLineLength + corner.LeftLineLength), 0, MidpointRounding.AwayFromZero);

			int areaLeft = imageCol - side / 2;
			int areaTop = imageRow - side / 2;
			int areaWidth = side;
			int areaHeight = side;

			// horizontal search for finders
			if (!HorizontalAlignmentSearch(areaLeft, areaTop, areaWidth, areaHeight))
			{
				return false;
			}

			// vertical search for finders
			VerticalAlignmentSearch(areaLeft, areaTop, areaWidth, areaHeight);

			// remove unused alignment entries
			if (!RemoveUnusedAlignMarks())
			{
				return false;
			}

			return true;
		}

		internal void SetTransMatrix(Corner corner, double imageAlignRow, double imageAlignCol)
		{
			// top right and bottom left QR code position
			int farFinder = QRCodeDimension - 4;
			int farAlign = QRCodeDimension - 7;

			double[,] matrix = new double[8, 9];

			matrix[0, 0] = 3.0;
			matrix[0, 1] = 3.0;
			matrix[0, 2] = 1.0;
			matrix[0, 6] = -3.0 * corner.TopLeftFinder.Col;
			matrix[0, 7] = -3.0 * corner.TopLeftFinder.Col;
			matrix[0, 8] = corner.TopLeftFinder.Col;

			matrix[1, 0] = farFinder;
			matrix[1, 1] = 3.0;
			matrix[1, 2] = 1.0;
			matrix[1, 6] = -farFinder * corner.TopRightFinder.Col;
			matrix[1, 7] = -3.0 * corner.TopRightFinder.Col;
			matrix[1, 8] = corner.TopRightFinder.Col;

			matrix[2, 0] = 3.0;
			matrix[2, 1] = farFinder;
			matrix[2, 2] = 1.0;
			matrix[2, 6] = -3.0 * corner.BottomLeftFinder.Col;
			matrix[2, 7] = -farFinder * corner.BottomLeftFinder.Col;
			matrix[2, 8] = corner.BottomLeftFinder.Col;

			matrix[3, 0] = farAlign;
			matrix[3, 1] = farAlign;
			matrix[3, 2] = 1.0;
			matrix[3, 6] = -farAlign * imageAlignCol;
			matrix[3, 7] = -farAlign * imageAlignCol;
			matrix[3, 8] = imageAlignCol;

			matrix[4, 3] = 3.0;
			matrix[4, 4] = 3.0;
			matrix[4, 5] = 1.0;
			matrix[4, 6] = -3.0 * corner.TopLeftFinder.Row;
			matrix[4, 7] = -3.0 * corner.TopLeftFinder.Row;
			matrix[4, 8] = corner.TopLeftFinder.Row;

			matrix[5, 3] = farFinder;
			matrix[5, 4] = 3.0;
			matrix[5, 5] = 1.0;
			matrix[5, 6] = -farFinder * corner.TopRightFinder.Row;
			matrix[5, 7] = -3.0 * corner.TopRightFinder.Row;
			matrix[5, 8] = corner.TopRightFinder.Row;

			matrix[6, 3] = 3.0;
			matrix[6, 4] = farFinder;
			matrix[6, 5] = 1.0;
			matrix[6, 6] = -3.0 * corner.BottomLeftFinder.Row;
			matrix[6, 7] = -farFinder * corner.BottomLeftFinder.Row;
			matrix[6, 8] = corner.BottomLeftFinder.Row;

			matrix[7, 3] = farAlign;
			matrix[7, 4] = farAlign;
			matrix[7, 5] = 1.0;
			matrix[7, 6] = -farAlign * imageAlignRow;
			matrix[7, 7] = -farAlign * imageAlignRow;
			matrix[7, 8] = imageAlignRow;

			for (int row = 0; row < 8; row++)
			{
				// If the element is zero, make it non zero by adding another row
				if (matrix[row, row] == 0)
				{
					int row1 = row + 1;
					while (row1 < 8 && matrix[row1, row] == 0)
					{
						row1++;
					}

					if (row1 == 8)
					{
						throw new ApplicationException("Solve linear equations failed");
					}

					for (int col = row; col < 9; col++)
					{
						matrix[row, col] += matrix[row1, col];
					}
				}

				// make the diagonal element 1.0
				for (int col = 8; col > row; col--)
				{
					matrix[row, col] /= matrix[row, row];
				}

				// subtract current row from next rows to eliminate one value
				for (int row1 = row + 1; row1 < 8; row1++)
				{
					for (int col = 8; col > row; col--)
					{
						matrix[row1, col] -= matrix[row, col] * matrix[row1, row];
					}
				}
			}

			// go up from last row and eliminate all solved values
			for (int col = 7; col > 0; col--)
			{
				for (int row = col - 1; row >= 0; row--)
				{
					matrix[row, 8] -= matrix[row, col] * matrix[col, 8];
				}
			}

			Trans4a = matrix[0, 8];
			Trans4b = matrix[1, 8];
			Trans4c = matrix[2, 8];
			Trans4d = matrix[3, 8];
			Trans4e = matrix[4, 8];
			Trans4f = matrix[5, 8];
			Trans4g = matrix[6, 8];
			Trans4h = matrix[7, 8];

			// set trans 4 mode
			Trans4Mode = true;
		}

		// Get version code bits top right
		internal int GetVersionOne()
		{
			int versionCode = 0;
			for (int index = 0; index < 18; index++)
			{
				if (GetModule(index / 3, QRCodeDimension - 11 + (index % 3)))
				{
					versionCode |= 1 << index;
				}
			}
			return TestVersionCode(versionCode);
		}

		// Get version code bits bottom left
		internal int GetVersionTwo()
		{
			int versionCode = 0;
			for (int index = 0; index < 18; index++)
			{
				if (GetModule(QRCodeDimension - 11 + (index % 3), index / 3))
				{
					versionCode |= 1 << index;
				}
			}
			return TestVersionCode(versionCode);
		}

		// Test version code bits
		internal int TestVersionCode(int versionCode)
		{
			// format info
			int code = versionCode >> 12;

			// test for exact match
			if (code >= 7 && code <= 40 && StaticTables.VersionCodeArray[code - 7] == versionCode)
			{
				return code;
			}

			// look for a match
			int bestInfo = 0;
			int error = int.MaxValue;
			for (int index = 0; index < 34; index++)
			{
				// test for exact match
				int errorBits = StaticTables.VersionCodeArray[index] ^ versionCode;
				if (errorBits == 0)
				{
					return versionCode >> 12;
				}

				// count errors
				int errorCount = CountBits(errorBits);

				// save best result
				if (errorCount < error)
				{
					error = errorCount;
					bestInfo = index;
				}
			}

			return error <= 3 ? bestInfo + 7 : 0;
		}

		// Get format info around top left corner
		public int GetFormatInfoOne()
		{
			int info = 0;
			for (int index = 0; index < 15; index++)
			{
				if (GetModule(StaticTables.FormatInfoOne[index, 0], StaticTables.FormatInfoOne[index, 1]))
				{
					info |= 1 << index;
				}
			}
			return TestFormatInfo(info);
		}

		// Get format info around top right and bottom left corners
		internal int GetFormatInfoTwo()
		{
			int info = 0;
			for (int index = 0; index < 15; index++)
			{
				int row = StaticTables.FormatInfoTwo[index, 0];
				if (row < 0)
				{
					row += QRCodeDimension;
				}

				int col = StaticTables.FormatInfoTwo[index, 1];
				if (col < 0)
				{
					col += QRCodeDimension;
				}

				if (GetModule(row, col))
				{
					info |= 1 << index;
				}
			}
			return TestFormatInfo(info);
		}

		// Test format info bits
		internal int TestFormatInfo(int formatInfo)
		{
			// format info
			int info = (formatInfo ^ 0x5412) >> 10;

			// test for exact match
			if (StaticTables.FormatInfoArray[info] == formatInfo)
			{
				return info;
			}

			// look for a match
			int bestInfo = 0;
			int error = int.MaxValue;
			for (int index = 0; index < 32; index++)
			{
				int errorCount = CountBits(StaticTables.FormatInfoArray[index] ^ formatInfo);
				if (errorCount < error)
				{
					error = errorCount;
					bestInfo = index;
				}
			}
			return error <= 3 ? bestInfo : -1;
		}

		internal int CountBits(int value)
		{
			int count = 0;
			for (int mask = 0x4000; mask != 0; mask >>= 1)
			{
				if ((value & mask) != 0)
				{
					count++;
				}
			}

			return count;
		}

		// Convert image to qr code matrix and test fixed modules
		internal void ConvertImageToMatrix()
		{
			// loop for all modules
			int fixedCount = 0;
			int errorCount = 0;
			for (int row = 0; row < QRCodeDimension; row++)
			{
				for (int col = 0; col < QRCodeDimension; col++)
				{
					// the module (Row, Col) is not a fixed module
					if ((BaseMatrix[row, col] & StaticTables.Fixed) == 0)
					{
						if (GetModule(row, col))
						{
							BaseMatrix[row, col] |= StaticTables.Black;
						}
					}

					// fixed module
					else
					{
						// total fixed modules
						fixedCount++;

						// test for error
						if ((GetModule(row, col) ? StaticTables.Black : StaticTables.White) != (BaseMatrix[row, col] & 1))
						{
							errorCount++;
						}
					}
				}
			}

			if (errorCount > fixedCount * ErrCorrPercent[(int)ErrorCorrection] / 100)
			{
				throw new ApplicationException("Fixed modules error");
			}
		}

		// Unload matrix data from base matrix
		internal void UnloadDataFromMatrix()
		{
			// input array pointer initialization
			int ptr = 0;
			int ptrEnd = 8 * MaxCodewords;
			CodewordsArray = new byte[MaxCodewords];

			// bottom right corner of output matrix
			int row = QRCodeDimension - 1;
			int col = QRCodeDimension - 1;

			// step state
			int state = 0;
			while (true)
			{
				// current module is data
				if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
				{
					// unload current module with
					if ((MaskMatrix[row, col] & 1) != 0)
					{
						CodewordsArray[ptr >> 3] |= (byte)(1 << (7 - (ptr & 7)));
					}

					if (++ptr == ptrEnd)
					{
						break;
					}
				}

				// current module is non data and vertical timing line condition is on
				else if (col == 6)
				{
					col--;
				}

				// update matrix position to next module
				switch (state)
				{
					// going up: step one to the left
					case 0:
						col--;
						state = 1;
						continue;

					// going up: step one row up and one column to the right
					case 1:
						col++;
						row--;
						// we are not at the top, go to state 0
						if (row >= 0)
						{
							state = 0;
							continue;
						}
						// we are at the top, step two columns to the left and start going down
						col -= 2;
						row = 0;
						state = 2;
						continue;

					// going down: step one to the left
					case 2:
						col--;
						state = 3;
						continue;

					// going down: step one row down and one column to the right
					case 3:
						col++;
						row++;
						// we are not at the bottom, go to state 2
						if (row < QRCodeDimension)
						{
							state = 2;
							continue;
						}
						// we are at the bottom, step two columns to the left and start going up
						col -= 2;
						row = QRCodeDimension - 1;
						state = 0;
						continue;
				}
			}
		}

		// Restore interleave data and error correction blocks
		internal void RestoreBlocks()
		{
			// allocate temp codewords array
			byte[] tempArray = new byte[MaxCodewords];

			// total blocks
			int totalBlocks = BlocksGroup1 + BlocksGroup2;

			// create array of data blocks starting point
			int[] start = new int[totalBlocks];
			for (int index = 1; index < totalBlocks; index++)
			{
				start[index] = start[index - 1] + (index <= BlocksGroup1 ? DataCodewordsGroup1 : DataCodewordsGroup2);
			}

			// step one. iterleave base on group one length
			int ptrEnd = DataCodewordsGroup1 * totalBlocks;

			// restore group one and two
			int ptr;
			int block = 0;
			for (ptr = 0; ptr < ptrEnd; ptr++)
			{
				tempArray[start[block]] = CodewordsArray[ptr];
				start[block]++;
				block++;
				if (block == totalBlocks)
				{
					block = 0;
				}
			}

			// restore group two
			if (DataCodewordsGroup2 > DataCodewordsGroup1)
			{
				// step one. iterleave base on group one length
				ptrEnd = MaxDataCodewords;

				block = BlocksGroup1;
				for (; ptr < ptrEnd; ptr++)
				{
					tempArray[start[block]] = CodewordsArray[ptr];
					start[block]++;
					block++;
					if (block == totalBlocks)
					{
						block = BlocksGroup1;
					}
				}
			}

			// create array of error correction blocks starting point
			start[0] = MaxDataCodewords;
			for (int index = 1; index < totalBlocks; index++)
			{
				start[index] = start[index - 1] + ErrCorrCodewords;
			}

			// restore all groups
			ptrEnd = MaxCodewords;
			block = 0;
			for (; ptr < ptrEnd; ptr++)
			{
				tempArray[start[block]] = CodewordsArray[ptr];
				start[block]++;
				block++;
				if (block == totalBlocks)
				{
					block = 0;
				}
			}

			// save result
			CodewordsArray = tempArray;
		}

		protected void CalculateErrorCorrection()
		{
			int totalErrorCount = 0;

			// set generator polynomial array
			byte[] generator = StaticTables.GenArray[ErrCorrCodewords - 7];

			// error correcion calculation buffer
			int bufSize = Math.Max(DataCodewordsGroup1, DataCodewordsGroup2) + ErrCorrCodewords;
			byte[] errCorrBuff = new byte[bufSize];

			// initial number of data codewords
			int dataCodewords = DataCodewordsGroup1;
			int buffLen = dataCodewords + ErrCorrCodewords;

			// codewords pointer
			int dataCodewordsPtr = 0;

			// codewords buffer error correction pointer
			int codewordsArrayErrCorrPtr = MaxDataCodewords;

			// loop one block at a time
			int totalBlocks = BlocksGroup1 + BlocksGroup2;
			for (int blockNumber = 0; blockNumber < totalBlocks; blockNumber++)
			{
				// switch to group2 data codewords
				if (blockNumber == BlocksGroup1)
				{
					dataCodewords = DataCodewordsGroup2;
					buffLen = dataCodewords + ErrCorrCodewords;
				}

				// copy next block of codewords to the buffer and clear the remaining part
				Array.Copy(CodewordsArray, dataCodewordsPtr, errCorrBuff, 0, dataCodewords);
				Array.Copy(CodewordsArray, codewordsArrayErrCorrPtr, errCorrBuff, dataCodewords, ErrCorrCodewords);

				// make a duplicate
				byte[] correctionBuffer = (byte[])errCorrBuff.Clone();

				// error correction polynomial division
				ReedSolomon.PolynominalDivision(errCorrBuff, buffLen, generator, ErrCorrCodewords);

				// test for error
				int index = 0;

				while (index < ErrCorrCodewords && errCorrBuff[dataCodewords + index] == 0)
				{
					index++;
				}

				if (index < ErrCorrCodewords)
				{
					// correct the error
					int errorCount = ReedSolomon.CorrectData(correctionBuffer, buffLen, ErrCorrCodewords);
					if (errorCount <= 0)
					{
						throw new ApplicationException("Data is damaged. Error correction failed");
					}

					totalErrorCount += errorCount;

					// fix the data
					Array.Copy(correctionBuffer, 0, CodewordsArray, dataCodewordsPtr, dataCodewords);
				}

				// update codewords array to next buffer
				dataCodewordsPtr += dataCodewords;

				// update pointer
				codewordsArrayErrCorrPtr += ErrCorrCodewords;
			}
		}

		// Convert bit array to byte array
		internal byte[] DecodeData()
		{
			// bit buffer initial condition
			BitBuffer = (uint)((CodewordsArray[0] << 24) | (CodewordsArray[1] << 16) | (CodewordsArray[2] << 8) | CodewordsArray[3]);
			BitBufferLen = 32;
			CodewordsPtr = 4;

			// allocate data byte list
			List<byte> dataSeg = new();

			// reset ECI assignment value
			ECIAssignValue = -1;

			// data might be made of blocks
			while (true)
			{
				// first 4 bits is mode indicator
				EncodingMode encodingMode = (EncodingMode)ReadBitsFromCodewordsArray(4);

				// end of data
				if (encodingMode <= 0)
				{
					break;
				}

				// test for encoding ECI assignment number
				if (encodingMode == EncodingMode.ECI)
				{
					// one byte assinment value
					ECIAssignValue = ReadBitsFromCodewordsArray(8);
					if ((ECIAssignValue & 0x80) == 0)
					{
						continue;
					}

					// two bytes assinment value
					ECIAssignValue = (ECIAssignValue << 8) | ReadBitsFromCodewordsArray(8);
					if ((ECIAssignValue & 0x4000) == 0)
					{
						ECIAssignValue &= 0x3fff;
						continue;
					}

					// three bytes assinment value
					ECIAssignValue = (ECIAssignValue << 8) | ReadBitsFromCodewordsArray(8);
					if ((ECIAssignValue & 0x200000) == 0)
					{
						ECIAssignValue &= 0x1fffff;
						continue;
					}
					throw new ApplicationException("ECI encoding assinment number in error");
				}

				// read data length
				int dataLength = ReadBitsFromCodewordsArray(DataLengthBits(encodingMode));
				if (dataLength < 0)
				{
					throw new ApplicationException("Premature end of data (DataLengh)");
				}

				// save start of segment
				int segStart = dataSeg.Count;

				// switch based on encode mode
				// numeric code indicator is 0001, alpha numeric 0010, byte 0100
				switch (encodingMode)
				{
					// numeric mode
					case EncodingMode.Numeric:
						// encode digits in groups of 2
						int numericEnd = (dataLength / 3) * 3;
						for (int index = 0; index < numericEnd; index += 3)
						{
							int temp = ReadBitsFromCodewordsArray(10);
							if (temp < 0)
							{
								throw new ApplicationException("Premature end of data (Numeric 1)");
							}
							dataSeg.Add(StaticTables.DecodingTable[temp / 100]);
							dataSeg.Add(StaticTables.DecodingTable[(temp % 100) / 10]);
							dataSeg.Add(StaticTables.DecodingTable[temp % 10]);
						}

						// we have one character remaining
						if (dataLength - numericEnd == 1)
						{
							int temp = ReadBitsFromCodewordsArray(4);
							if (temp < 0)
							{
								throw new ApplicationException("Premature end of data (Numeric 2)");
							}
							dataSeg.Add(StaticTables.DecodingTable[temp]);
						}

						// we have two character remaining
						else if (dataLength - numericEnd == 2)
						{
							int temp = ReadBitsFromCodewordsArray(7);
							if (temp < 0)
							{
								throw new ApplicationException("Premature end of data (Numeric 3)");
							}
							dataSeg.Add(StaticTables.DecodingTable[temp / 10]);
							dataSeg.Add(StaticTables.DecodingTable[temp % 10]);
						}
						break;

					// alphanumeric mode
					case EncodingMode.AlphaNumeric:
						// encode digits in groups of 2
						int alphaNumEnd = (dataLength / 2) * 2;
						for (int index = 0; index < alphaNumEnd; index += 2)
						{
							int temp = ReadBitsFromCodewordsArray(11);
							if (temp < 0)
							{
								throw new ApplicationException("Premature end of data (Alpha Numeric 1)");
							}
							dataSeg.Add(StaticTables.DecodingTable[temp / 45]);
							dataSeg.Add(StaticTables.DecodingTable[temp % 45]);
						}

						// we have one character remaining
						if (dataLength - alphaNumEnd == 1)
						{
							int temp = ReadBitsFromCodewordsArray(6);
							if (temp < 0)
							{
								throw new ApplicationException("Premature end of data (Alpha Numeric 2)");
							}
							dataSeg.Add(StaticTables.DecodingTable[temp]);
						}
						break;

					// byte mode
					case EncodingMode.Byte:
						// append the data after mode and character count
						for (int index = 0; index < dataLength; index++)
						{
							int temp = ReadBitsFromCodewordsArray(8);
							if (temp < 0)
							{
								throw new ApplicationException("Premature end of data (byte mode)");
							}
							dataSeg.Add((byte)temp);
						}
						break;

					default:
						throw new ApplicationException(string.Format("Encoding mode not supported {0}", encodingMode.ToString()));
				}

				if (dataLength != dataSeg.Count - segStart)
				{
					throw new ApplicationException("Data encoding length in error");
				}
			}

			return dataSeg.ToArray();
		}

		internal int ReadBitsFromCodewordsArray(int bits)
		{
			if (bits > BitBufferLen)
			{
				return -1;
			}

			int data = (int)(BitBuffer >> (32 - bits));
			BitBuffer <<= bits;
			BitBufferLen -= bits;
			while (BitBufferLen <= 24 && CodewordsPtr < MaxDataCodewords)
			{
				BitBuffer |= (uint)(CodewordsArray[CodewordsPtr++] << (24 - BitBufferLen));
				BitBufferLen += 8;
			}
			return data;
		}

		// Set encoded data bits length
		internal int DataLengthBits(EncodingMode encodingMode)
		{
			// Data length bits
			return encodingMode switch
			{
				EncodingMode.Numeric => QRCodeVersion < 10 ? 10 : (QRCodeVersion < 27 ? 12 : 14),
				EncodingMode.AlphaNumeric => QRCodeVersion < 10 ? 9 : (QRCodeVersion < 27 ? 11 : 13),
				EncodingMode.Byte => QRCodeVersion < 10 ? 8 : 16,
				_ => throw new ApplicationException("Unsupported encoding mode " + encodingMode.ToString()),
			};
		}

		// Set data and error correction codewords length
		internal void SetDataCodewordsLength()
		{
			// index shortcut
			int blockInfoIndex = (QRCodeVersion - 1) * 4 + (int)ErrorCorrection;

			// Number of blocks in group 1
			BlocksGroup1 = StaticTables.ECBlockInfo[blockInfoIndex, StaticTables.BLOCKS_GROUP1];

			// Number of data codewords in blocks of group 1
			DataCodewordsGroup1 = StaticTables.ECBlockInfo[blockInfoIndex, StaticTables.DATA_CODEWORDS_GROUP1];

			// Number of blocks in group 2
			BlocksGroup2 = StaticTables.ECBlockInfo[blockInfoIndex, StaticTables.BLOCKS_GROUP2];

			// Number of data codewords in blocks of group 2
			DataCodewordsGroup2 = StaticTables.ECBlockInfo[blockInfoIndex, StaticTables.DATA_CODEWORDS_GROUP2];

			// Total number of data codewords for this version and EC level
			MaxDataCodewords = BlocksGroup1 * DataCodewordsGroup1 + BlocksGroup2 * DataCodewordsGroup2;
			MaxDataBits = 8 * MaxDataCodewords;

			// total data plus error correction bits
			MaxCodewords = StaticTables.MaxCodewordsArray[QRCodeVersion];

			// Error correction codewords per block
			ErrCorrCodewords = (MaxCodewords - MaxDataCodewords) / (BlocksGroup1 + BlocksGroup2);
		}

		internal ErrorCorrection FormatInfoToErrCode(int info)
		{
			return (ErrorCorrection)(info ^ 1);
		}

		internal void BuildBaseMatrix()
		{
			// allocate base matrix
			BaseMatrix = new byte[QRCodeDimension + 5, QRCodeDimension + 5];

			// top left finder patterns
			for (int row = 0; row < 9; row++)
			{
				for (int col = 0; col < 9; col++)
				{
					BaseMatrix[row, col] = StaticTables.FinderPatternTopLeft[row, col];
				}
			}

			// top right finder patterns
			int pos = QRCodeDimension - 8;
			for (int row = 0; row < 9; row++)
			{
				for (int col = 0; col < 8; col++)
				{
					BaseMatrix[row, pos + col] = StaticTables.FinderPatternTopRight[row, col];
				}
			}

			// bottom left finder patterns
			for (int row = 0; row < 8; row++)
			{
				for (int col = 0; col < 9; col++)
				{
					BaseMatrix[pos + row, col] = StaticTables.FinderPatternBottomLeft[row, col];
				}
			}

			// Timing pattern
			for (int z = 8; z < QRCodeDimension - 8; z++)
			{
				BaseMatrix[z, 6] = BaseMatrix[6, z] = (z & 1) == 0 ? StaticTables.FixedBlack : StaticTables.FixedWhite;
			}

			// alignment pattern
			if (QRCodeVersion > 1)
			{
				byte[] alignPos = StaticTables.AlignmentPositionArray[QRCodeVersion];
				int alignmentDimension = alignPos.Length;
				for (int row = 0; row < alignmentDimension; row++)
				{
					for (int col = 0; col < alignmentDimension; col++)
					{
						if (col == 0 && row == 0 || col == alignmentDimension - 1 && row == 0 || col == 0 && row == alignmentDimension - 1)
						{
							continue;
						}

						int posRow = alignPos[row];
						int posCol = alignPos[col];
						for (int aRow = -2; aRow < 3; aRow++)
						{
							for (int aCol = -2; aCol < 3; aCol++)
							{
								BaseMatrix[posRow + aRow, posCol + aCol] = StaticTables.AlignmentPattern[aRow + 2, aCol + 2];
							}
						}
					}
				}
			}

			// reserve version information
			if (QRCodeVersion >= 7)
			{
				// position of 3 by 6 rectangles
				pos = QRCodeDimension - 11;

				// top right
				for (int row = 0; row < 6; row++)
				{
					for (int col = 0; col < 3; col++)
					{
						BaseMatrix[row, pos + col] = StaticTables.FormatWhite;
					}
				}

				// bottom right
				for (int col = 0; col < 6; col++)
				{
					for (int row = 0; row < 3; row++)
					{
						BaseMatrix[pos + row, col] = StaticTables.FormatWhite;
					}
				}
			}
		}

		// Apply Mask
		internal void ApplyMask(int mask)
		{
			MaskMatrix = (byte[,])BaseMatrix.Clone();
			switch (mask)
			{
				case 0:
					ApplyMask0();
					break;

				case 1:
					ApplyMask1();
					break;

				case 2:
					ApplyMask2();
					break;

				case 3:
					ApplyMask3();
					break;

				case 4:
					ApplyMask4();
					break;

				case 5:
					ApplyMask5();
					break;

				case 6:
					ApplyMask6();
					break;

				case 7:
					ApplyMask7();
					break;
			}
			return;
		}

		// Apply Mask 0
		// (row + column) % 2 == 0
		internal void ApplyMask0()
		{
			for (int row = 0; row < QRCodeDimension; row += 2)
			{
				for (int col = 0; col < QRCodeDimension; col += 2)
				{
					if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 1] ^= 1;
					}
				}
			}
		}

		// Apply Mask 1
		// row % 2 == 0
		internal void ApplyMask1()
		{
			for (int row = 0; row < QRCodeDimension; row += 2)
			{
				for (int col = 0; col < QRCodeDimension; col++)
				{
					if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col] ^= 1;
					}
				}
			}
		}

		// Apply Mask 2
		// column % 3 == 0
		internal void ApplyMask2()
		{
			for (int row = 0; row < QRCodeDimension; row++)
			{
				for (int col = 0; col < QRCodeDimension; col += 3)
				{
					if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col] ^= 1;
					}
				}
			}
		}

		// Apply Mask 3
		// (row + column) % 3 == 0
		internal void ApplyMask3()
		{
			for (int row = 0; row < QRCodeDimension; row += 3)
			{
				for (int col = 0; col < QRCodeDimension; col += 3)
				{
					if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 1] ^= 1;
					}
				}
			}
		}

		// Apply Mask 4
		// ((row / 2) + (column / 3)) % 2 == 0
		internal void ApplyMask4()
		{
			for (int row = 0; row < QRCodeDimension; row += 4)
			{
				for (int col = 0; col < QRCodeDimension; col += 6)
				{
					if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col] ^= 1;
					}

					if ((MaskMatrix[row, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col + 1] ^= 1;
					}

					if ((MaskMatrix[row, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 1, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 1] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 5] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 5] ^= 1;
					}
				}
			}
		}

		// Apply Mask 5
		// ((row * column) % 2) + ((row * column) % 3) == 0
		internal void ApplyMask5()
		{
			for (int row = 0; row < QRCodeDimension; row += 6)
			{
				for (int col = 0; col < QRCodeDimension; col += 6)
				{
					for (int delta = 0; delta < 6; delta++)
					{
						if ((MaskMatrix[row, col + delta] & StaticTables.NonData) == 0)
						{
							MaskMatrix[row, col + delta] ^= 1;
						}
					}

					for (int delta = 1; delta < 6; delta++)
					{
						if ((MaskMatrix[row + delta, col] & StaticTables.NonData) == 0)
						{
							MaskMatrix[row + delta, col] ^= 1;
						}
					}

					if ((MaskMatrix[row + 2, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 4, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col + 3] ^= 1;
					}
				}
			}
		}

		// Apply Mask 6
		// (((row * column) % 2) + ((row * column) mod 3)) mod 2 == 0
		internal void ApplyMask6()
		{
			for (int row = 0; row < QRCodeDimension; row += 6)
			{
				for (int col = 0; col < QRCodeDimension; col += 6)
				{
					for (int delta = 0; delta < 6; delta++)
					{
						if ((MaskMatrix[row, col + delta] & StaticTables.NonData) == 0)
						{
							MaskMatrix[row, col + delta] ^= 1;
						}
					}

					for (int delta = 1; delta < 6; delta++)
					{
						if ((MaskMatrix[row + delta, col] & StaticTables.NonData) == 0)
						{
							MaskMatrix[row + delta, col] ^= 1;
						}
					}

					if ((MaskMatrix[row + 1, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 1] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 1] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 4, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 4, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 4, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col + 5] ^= 1;
					}

					if ((MaskMatrix[row + 5, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 5, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 5, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 5, col + 5] ^= 1;
					}
				}
			}
		}

		// Apply Mask 7
		// (((row + column) % 2) + ((row * column) mod 3)) mod 2 == 0
		internal void ApplyMask7()
		{
			for (int row = 0; row < QRCodeDimension; row += 6)
			{
				for (int col = 0; col < QRCodeDimension; col += 6)
				{
					if ((MaskMatrix[row, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col] ^= 1;
					}

					if ((MaskMatrix[row, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col + 2] ^= 1;
					}

					if ((MaskMatrix[row, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 1, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 1, col + 5] ^= 1;
					}

					if ((MaskMatrix[row + 2, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 4] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 4] ^= 1;
					}

					if ((MaskMatrix[row + 2, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 2, col + 5] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 1] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 3] ^= 1;
					}

					if ((MaskMatrix[row + 3, col + 5] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 3, col + 5] ^= 1;
					}

					if ((MaskMatrix[row + 4, col] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col] ^= 1;
					}

					if ((MaskMatrix[row + 4, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col + 1] ^= 1;
					}

					if ((MaskMatrix[row + 4, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 4, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 5, col + 1] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 5, col + 1] ^= 1;
					}

					if ((MaskMatrix[row + 5, col + 2] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 5, col + 2] ^= 1;
					}

					if ((MaskMatrix[row + 5, col + 3] & StaticTables.NonData) == 0)
					{
						MaskMatrix[row + 5, col + 3] ^= 1;
					}
				}
			}
		}
	}
}
