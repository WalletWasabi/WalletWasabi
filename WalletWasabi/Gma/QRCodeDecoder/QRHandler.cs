/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
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

using QRCodeDecoderLibrary;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace QRCodeDecoder
{
	/// <summary>
	/// Test QR Code Decoder
	/// </summary>
	public partial class QRHandler
	{
		private QRDecoder? _qRCodeDecoder;
		private Bitmap? _qRCodeInputImage;

		/// <summary>
		/// Constructor
		/// </summary>
		public QRHandler()
		{
		}

		private void OnLoadButtonClick(object sender, EventArgs e)
		{
			_qRCodeDecoder = new QRDecoder();
			// Get the image file to decode
			OpenFileDialog dialog = new OpenFileDialog
			{
				Filter = "Image Files(*.png;*.jpg;*.gif;*.tif)|*.png;*.jpg;*.gif;*.tif;*.bmp)|All files (*.*)|*.*",
				Title = "Load QR Code Image",
				InitialDirectory = Directory.GetCurrentDirectory(),
				RestoreDirectory = true,
				FileName = string.Empty
			};

			// display dialog box
			if (dialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			// load image to bitmap
			_qRCodeInputImage = new Bitmap(dialog.FileName);

			// decode image IMPORTANT
			byte[][]? dataByteArray = _qRCodeDecoder.ImageDecoder(_qRCodeInputImage);

			if (dataByteArray is null)
			{
				AddressLabel.Text = "No data could be displayed";
				return;
			}
			// convert results to text IMPORTANT
			AddressLabel.Text = QRCodeResult(dataByteArray);

			return;
		}

		/// <summary>
		/// Format result for display
		/// </summary>
		/// <param name="dataByteArray"></param>
		/// <returns></returns>
		private static string QRCodeResult
				(
				byte[][] dataByteArray
				)
		{
			// no QR code
			if (dataByteArray == null)
			{
				return string.Empty;
			}

			// image has one QR code
			if (dataByteArray.Length == 1)
			{
				return ForDisplay(QRDecoder.ByteArrayToStr(dataByteArray[0]));
			}

			// image has more than one QR code
			StringBuilder str = new StringBuilder();
			for (int index = 0; index < dataByteArray.Length; index++)
			{
				if (index != 0)
				{
					str.Append("\r\n");
				}

				str.AppendFormat("QR Code {0}\r\n", index + 1);
				str.Append(ForDisplay(QRDecoder.ByteArrayToStr(dataByteArray[index])));
			}
			return str.ToString();
		}

		private static string ForDisplay
				(
				string result
				)
		{
			int index;
			for (index = 0; index < result.Length && (result[index] >= ' ' && result[index] <= '~' || result[index] >= 160); index++)
			{
				;
			}

			if (index == result.Length)
			{
				return result;
			}

			StringBuilder display = new StringBuilder(result.Substring(0, index));
			for (; index < result.Length; index++)
			{
				char oneChar = result[index];
				if (oneChar >= ' ' && oneChar <= '~' || oneChar >= 160)
				{
					display.Append(oneChar);
					continue;
				}

				if (oneChar == '\r')
				{
					display.Append("\r\n");
					if (index + 1 < result.Length && result[index + 1] == '\n')
					{
						index++;
					}

					continue;
				}

				if (oneChar == '\n')
				{
					display.Append("\r\n");
					continue;
				}

				display.Append('¿');
			}
			return display.ToString();
		}
	}
}
