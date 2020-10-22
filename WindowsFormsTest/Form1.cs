using Emgu.CV;
using Emgu.CV.Structure;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsTest
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private Emgu.CV.Image<Rgb, byte> _testCvImage;
		private VideoCapture _videocapture;
		private Mat _frame;

		private void Button1_Click(object sender, EventArgs e)
		{
			pictureBox1.Image.Save(@"c:\a.png");
			bool succ;

			_frame = new Mat();
			using (_videocapture = new VideoCapture())
			{
				_videocapture.ImageGrabbed += ProcessFrame;
				succ = _videocapture.IsOpened;
				succ = _videocapture.Grab();
			}
			//var a = _videocapture.QueryFrame();

			//_videocapture.Start();
		}

		private void ProcessFrame(object? sender, EventArgs e)
		{
			VideoCapture videocapture = (VideoCapture)sender;

			if (_videocapture != null && _videocapture.Ptr != IntPtr.Zero)
			{
				videocapture.Retrieve(_frame, 0);

				_testCvImage = _frame.ToImage<Rgb, byte>();
				pictureBox1.Image = _testCvImage.ToBitmap();
			}
		}
	}
}
