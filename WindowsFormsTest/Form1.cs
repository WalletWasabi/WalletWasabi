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
		private Emgu.CV.Image<Rgb, byte> _testCvImage;
		private VideoCapture _videocapture;
		private Mat _frame;
		private QRCodeDetector _qrCodeDetector;
		private Timer _timer;

		public Form1()
		{
			InitializeComponent();

			_qrCodeDetector = new Emgu.CV.QRCodeDetector();
			_frame = new Mat();
			_videocapture = new VideoCapture();
			_videocapture.ImageGrabbed += VideocaptureImageGrabbed;
			_timer = new Timer();
			_timer.Tick += Timer_Tick;
			_timer.Interval = 100;
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			_videocapture.Grab();
		}

		private void VideocaptureImageGrabbed(object sender, EventArgs e)
		{
			VideoCapture videocapture = (VideoCapture)sender;

			if (_videocapture != null && _videocapture.Ptr != IntPtr.Zero)
			{
				videocapture.Retrieve(_frame, 0);
				_testCvImage = _frame.ToImage<Rgb, byte>();

				MethodInvoker methodInvokerDelegate = delegate ()
				{
					pictureBox1.Image = _testCvImage.ToBitmap();

					//_testCvImage.Save(@"c:\image\lastImage.png");
					label1.Text = GetQRcodeValueFromImage(_testCvImage);
				};

				if (InvokeRequired)
				{
					Invoke(methodInvokerDelegate);
				}
				else
				{
					methodInvokerDelegate();
				}
			}
		}

		private void Button1_Click(object sender, EventArgs e)
		{
			_videocapture.Grab();
		}

		private void ProcessFrame(object? sender, EventArgs e)
		{
			VideoCapture videocapture = (VideoCapture)sender;

			if (_videocapture != null && _videocapture.Ptr != IntPtr.Zero)
			{
				videocapture.Retrieve(_frame, 0);

				_testCvImage = _frame.ToImage<Rgb, byte>();
				pictureBox1.Image = _testCvImage.ToBitmap();
				//pictureBox1.Image.Save(@"c:\image\a.png");

				label1.Text = GetQRcodeValueFromImage(_testCvImage);
			}
		}

		private void BtnQRCodeDetection_Click(object sender, EventArgs e)
		{
			Image<Gray, byte> img = new Image<Emgu.CV.Structure.Gray, byte>(@"c:\image\lastImage1.png");
			Image<Gray, byte> img2 = new Image<Gray, byte>(25, 25);
			Bitmap temp = img.ToBitmap();
			//temp.Save(@"c:\image\temp.bmp");
			GetQRcodeValueFromImage(img);
		}

		private object _lockObject = new object();
		private bool _isLocked = false;

		private string GetQRcodeValueFromImage(IInputArray image)
		{
			string qrCodeStr = "NA";

			if (_isLocked)
			{
				return qrCodeStr;
			}

			lock (_lockObject)
			{
				_isLocked = true;

				bool succ = false;

				IOutputArray result = new Mat();
				IOutputArray resultImg = new Mat();
				succ = _qrCodeDetector.Detect(image, result);
				if (succ)
				{
					qrCodeStr = _qrCodeDetector.Decode(image, result, resultImg);
				}
				//resultImg.Save(@"c:\image\decodeResultImage.bmp");

				_isLocked = false;
			}

			return qrCodeStr;
		}

		private void CbContinous_CheckedChanged(object sender, EventArgs e)
		{
			if (cbContinous.Checked)
			{
				_timer.Start();
			}
			else
			{
				_timer.Stop();
			}
		}
	}
}
