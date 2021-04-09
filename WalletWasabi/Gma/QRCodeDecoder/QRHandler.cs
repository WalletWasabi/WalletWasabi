/////////////////////////////////////////////////////////////////////
//
//	QR Code Library
//
//	QR Code Decoder test/demo application.
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
	public partial class QRHandler : Form
	{
		private QRDecoder QRCodeDecoder;
		private Bitmap QRCodeInputImage;
		private Rectangle ImageArea = new Rectangle();

		/// <summary>
		/// Constructor
		/// </summary>
		public QRHandler()
		{
			InitializeComponent();
			return;
		}

		/// <summary>
		/// Test decode program initialization
		/// </summary>
		/// <param name="sender">Sender</param>
		/// <param name="e">Event arguments</param>
		private void OnLoad(object sender, EventArgs e)
		{
			// program title
			Text = "QRCodeDecoder " + QRDecoder.VersionNumber + " \u00a9 2018-2019 Uzi Granot. All rights reserved.";

#if DEBUG
			// current directory
			string CurDir = Environment.CurrentDirectory;
			string WorkDir = CurDir.Replace("bin\\Debug", "Work");
			if (WorkDir != CurDir && Directory.Exists(WorkDir)) Environment.CurrentDirectory = WorkDir;

			// open trace file
			QRCodeTrace.Open("QRCodeDecoderTrace.txt");
			QRCodeTrace.Write("QRCodeDecoderDemo");
#endif

			// create decoder IMPORTANT
			QRCodeDecoder = new QRDecoder();

			// resize window
			OnResize(sender, e);
			return;
		}

		private void OnLoadImage(object sender, EventArgs e)
		{
			// get file name to decode
			OpenFileDialog Dialog = new OpenFileDialog
			{
				Filter = "Image Files(*.png;*.jpg;*.gif;*.tif)|*.png;*.jpg;*.gif;*.tif;*.bmp)|All files (*.*)|*.*",
				Title = "Load QR Code Image",
				InitialDirectory = Directory.GetCurrentDirectory(),
				RestoreDirectory = true,
				FileName = string.Empty
			};

			// display dialog box
			if (Dialog.ShowDialog() != DialogResult.OK) return;

			// clear parameters
			ImageFileLabel.Text = Dialog.FileName;

			// disable buttons
			LoadImageButton.Enabled = false;

			// dispose previous image
			if (QRCodeInputImage != null) QRCodeInputImage.Dispose();

			// load image to bitmap
			QRCodeInputImage = new Bitmap(Dialog.FileName);

			// trace
#if DEBUG
			QRCodeTrace.Format("****");
			QRCodeTrace.Format("Decode image: {0} ", Dialog.FileName);
			QRCodeTrace.Format("Image width: {0}, Height: {1}", QRCodeInputImage.Width, QRCodeInputImage.Height);
#endif

			// decode image IMPORTANT
			byte[][] DataByteArray = QRCodeDecoder.ImageDecoder(QRCodeInputImage);

			// display ECI value
			//ECIValueLabel.Text = QRCodeDecoder.ECIAssignValue >= 0 ? QRCodeDecoder.ECIAssignValue.ToString() : null;

			// convert results to text IMPORTANT
			DataTextBox.Text = QRCodeResult(DataByteArray);

			// enable buttons
			LoadImageButton.Enabled = true;

			// force repaint
			Invalidate();
			return;
		}

		/// <summary>
		/// Format result for display
		/// </summary>
		/// <param name="DataByteArray"></param>
		/// <returns></returns>
		private static string QRCodeResult
				(
				byte[][] DataByteArray
				)
		{
			// no QR code
			if (DataByteArray == null) return string.Empty;

			// image has one QR code
			if (DataByteArray.Length == 1) return ForDisplay(QRDecoder.ByteArrayToStr(DataByteArray[0]));

			// image has more than one QR code
			StringBuilder Str = new StringBuilder();
			for (int Index = 0; Index < DataByteArray.Length; Index++)
			{
				if (Index != 0) Str.Append("\r\n");
				Str.AppendFormat("QR Code {0}\r\n", Index + 1);
				Str.Append(ForDisplay(QRDecoder.ByteArrayToStr(DataByteArray[Index])));
			}
			return Str.ToString();
		}

		private static string ForDisplay
				(
				string Result
				)
		{
			int Index;
			for (Index = 0; Index < Result.Length && (Result[Index] >= ' ' && Result[Index] <= '~' || Result[Index] >= 160); Index++) ;
			if (Index == Result.Length) return Result;

			StringBuilder Display = new StringBuilder(Result.Substring(0, Index));
			for (; Index < Result.Length; Index++)
			{
				char OneChar = Result[Index];
				if (OneChar >= ' ' && OneChar <= '~' || OneChar >= 160)
				{
					Display.Append(OneChar);
					continue;
				}

				if (OneChar == '\r')
				{
					Display.Append("\r\n");
					if (Index + 1 < Result.Length && Result[Index + 1] == '\n') Index++;
					continue;
				}

				if (OneChar == '\n')
				{
					Display.Append("\r\n");
					continue;
				}

				Display.Append('¿');
			}
			return Display.ToString();
		}

		////////////////////////////////////////////////////////////////////
		// paint QR Code image
		////////////////////////////////////////////////////////////////////

		//private void OnPaint(object sender, PaintEventArgs e)
		//{
		//	// no image
		//	if (QRCodeInputImage == null) return;

		//	// calculate image area width and height to preserve aspect ratio
		//	Rectangle ImageRect = new Rectangle
		//	{
		//		Height = (ImageArea.Width * QRCodeInputImage.Height) / QRCodeInputImage.Width
		//	};

		//	if (ImageRect.Height <= ImageArea.Height)
		//	{
		//		ImageRect.Width = ImageArea.Width;
		//	}
		//	else
		//	{
		//		ImageRect.Width = (ImageArea.Height * QRCodeInputImage.Width) / QRCodeInputImage.Height;
		//		ImageRect.Height = ImageArea.Height;
		//	}

		//	// calculate position
		//	ImageRect.X = ImageArea.X + (ImageArea.Width - ImageRect.Width) / 2;
		//	ImageRect.Y = ImageArea.Y + (ImageArea.Height - ImageRect.Height) / 2;
		//	e.Graphics.DrawImage(QRCodeInputImage, ImageRect);
		//	return;
		//}

		////////////////////////////////////////////////////////////////////
		// Resize Encoder Control
		////////////////////////////////////////////////////////////////////

		//private void OnResize(object sender, EventArgs e)
		//{
		//	// minimize
		//	if (ClientSize.Width == 0) return;

		//	// center header label
		//	HeaderLabel.Left = (ClientSize.Width - HeaderLabel.Width) / 2;

		//	// put button at bottom left
		//	LoadImageButton.Top = ClientSize.Height - LoadImageButton.Height - 8;

		//	// image file label
		//	ImageFileLabel.Top = LoadImageButton.Top + (LoadImageButton.Height - ImageFileLabel.Height) / 2;
		//	ImageFileLabel.Width = ClientSize.Width - ImageFileLabel.Left - 16;

		//	// data text box
		//	DataTextBox.Top = LoadImageButton.Top - DataTextBox.Height - 8;
		//	DataTextBox.Width = ClientSize.Width - 8 - SystemInformation.VerticalScrollBarWidth;

		//	// decoded data label
		//	DecodedDataLabel.Top = DataTextBox.Top - DecodedDataLabel.Height - 8;

		//	// ECI
		//	ECIAssignLabel.Top = DecodedDataLabel.Top;
		//	ECIValueLabel.Top = ECIAssignLabel.Top;
		//	ECIValueLabel.Left = ImageFileLabel.Right - ECIValueLabel.Width;
		//	ECIAssignLabel.Left = ECIValueLabel.Left - ECIAssignLabel.Width - 4;

		//	// image area
		//	ImageArea.X = 4;
		//	ImageArea.Y = HeaderLabel.Bottom + 4;
		//	ImageArea.Width = ClientSize.Width - ImageArea.X - 4;
		//	ImageArea.Height = DecodedDataLabel.Top - ImageArea.Y - 4;

		//	if (QRCodeInputImage != null) Invalidate();
		//	return;
		//}
	}
}
