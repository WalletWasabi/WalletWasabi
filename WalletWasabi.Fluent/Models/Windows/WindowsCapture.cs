using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia; // Size
using Avalonia.Media; // PixelFormats
using Avalonia.Media.Imaging;
using Avalonia.Platform;

#pragma warning disable IDE0011 // Add braces
#pragma warning disable IDE0019 // Use pattern matching
#pragma warning disable IDE1006 // Naming Styles

namespace WalletWasabi.Fluent.Models.Windows;

// [How to use]
// string[] devices = UsbCamera.FindDevices();
// if (devices.Length == 0) return; // no camera.
//
// check format.
// int cameraIndex = 0;
// UsbCamera.VideoFormat[] formats = UsbCamera.GetVideoFormat(cameraIndex);
// for(int i=0; i<formats.Length; i++) Console.WriteLine("{0}:{1}", i, formats[i]);
//
// create usb camera and start.
// var camera = new UsbCamera(cameraIndex, formats[0]);
// camera.Start();
//
// get image.
// Immediately after starting the USB camera,
// GetBitmap() fails because image buffer is not prepared yet.
// var bmp = camera.GetBitmap();
//
// adjust properties.
// UsbCamera.PropertyItems.Property prop;
// prop = camera.Properties[DirectShow.CameraControlProperty.Exposure];
// if (prop.Available)
// {
//     prop.SetValue(DirectShow.CameraControlFlags.Manual, prop.Default);
// }
//
// prop = camera.Properties[DirectShow.VideoProcAmpProperty.WhiteBalance];
// if (prop.Available && prop.CanAuto)
// {
//     prop.SetValue(DirectShow.CameraControlFlags.Auto, 0);
// }

// [Note]
// By default, GetBitmap() returns image of System.Drawing.Bitmap.
// If WPF, define 'USBCAMERA_WPF' symbol that makes GetBitmap() returns image of Bitmap.

public class WindowsCapture
{
	/// <summary>Usb camera image size.</summary>
	public Size Size { get; private set; }

	/// <summary>Start.</summary>
	public Action Start { get; private set; }

	/// <summary>Stop.</summary>
	public Action Stop { get; private set; }

	/// <summary>Release resource.</summary>
	public Action Release { get; private set; }

	/// <summary>Get image.</summary>
	/// <remarks>Immediately after starting, fails because image buffer is not prepared yet.</remarks>
	public Func<Bitmap> GetBitmap { get; private set; }

	/// <summary>
	/// Get available USB camera list.
	/// </summary>
	/// <returns>Array of camera name, or if no device found, zero length array.</returns>
	public static string[] FindDevices()
	{
		return DirectShow.GetFilters(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory).ToArray();
	}

	/// <summary>
	/// Get video formats.
	/// </summary>
	public static VideoFormat[] GetVideoFormat(int cameraIndex)
	{
		var filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory, cameraIndex);
		var pin = DirectShow.FindPin(filter, 0, DirectShow.PIN_DIRECTION.PINDIR_OUTPUT);
		return GetVideoOutputFormat(pin);
	}

	/// <summary>
	/// Create USB Camera. If device do not support the size, default size will applied.
	/// </summary>
	/// <param name="cameraIndex">Camera index in FindDevices() result.</param>
	/// <param name="size">
	/// Size you want to create. Normally use Size property of VideoFormat in GetVideoFormat() result.
	/// </param>
	public WindowsCapture(int cameraIndex, Size size) : this(cameraIndex, new VideoFormat() { Size = size })
	{
	}

	/// <summary>
	/// Create USB Camera. If device do not support the format, default format will applied.
	/// </summary>
	/// <param name="cameraIndex">Camera index in FindDevices() result.</param>
	/// <param name="format">
	/// Normally use GetVideoFormat() result.
	/// You can change TimePerFrame value from Caps.MinFrameInterval to Caps.MaxFrameInterval.
	/// TimePerFrame = 10,000,000 / frame duration. (ex: 333333 in case 30fps).
	/// You can change Size value in case Caps.MaxOutputSize > Caps.MinOutputSize and OutputGranularityX/Y is not zero.
	/// Size = any value from Caps.MinOutputSize to Caps.MaxOutputSize step with OutputGranularityX/Y.
	/// </param>
	public WindowsCapture(int cameraIndex, VideoFormat format)
	{
		var camera_list = FindDevices();
		if (cameraIndex >= camera_list.Length)
			throw new ArgumentException("USB camera is not available.", "cameraIndex");
		Init(cameraIndex, format);
	}

	private void Init(int index, VideoFormat format)
	{
		//----------------------------------
		// Create Filter Graph
		//----------------------------------
		// +--------------------+  +----------------+  +---------------+
		// |Video Capture Source|→| Sample Grabber |→| Null Renderer |
		// +--------------------+  +----------------+  +---------------+
		//                                 ↓GetBitmap()

		var graph = DirectShow.CreateGraph();

		//----------------------------------
		// VideoCaptureSource
		//----------------------------------
		var vcap_source = CreateVideoCaptureSource(index, format);
		graph.AddFilter(vcap_source, "VideoCapture");

		//------------------------------
		// SampleGrabber
		//------------------------------
		var grabber = CreateSampleGrabber();
		graph.AddFilter(grabber, "SampleGrabber");
		var i_grabber = (DirectShow.ISampleGrabber)grabber;
		i_grabber.SetBufferSamples(true); //サンプルグラバでのサンプリングを開始

		//---------------------------------------------------
		// Null Renderer
		//---------------------------------------------------
		var renderer = DirectShow.CoCreateInstance(DirectShow.DsGuid.CLSID_NullRenderer) as DirectShow.IBaseFilter;
		graph.AddFilter(renderer, "NullRenderer");

		//---------------------------------------------------
		// Create Filter Graph
		//---------------------------------------------------
		var builder =
			DirectShow.CoCreateInstance(DirectShow.DsGuid.CLSID_CaptureGraphBuilder2) as
				DirectShow.ICaptureGraphBuilder2;
		builder.SetFiltergraph(graph);
		var pinCategory = DirectShow.DsGuid.PIN_CATEGORY_CAPTURE;
		var mediaType = DirectShow.DsGuid.MEDIATYPE_Video;
		builder.RenderStream(ref pinCategory, ref mediaType, vcap_source, grabber, renderer);

		// SampleGrabber Format.
		{
			var mt = new DirectShow.AM_MEDIA_TYPE();
			i_grabber.GetConnectedMediaType(mt);
			var header =
				(DirectShow.VIDEOINFOHEADER)Marshal.PtrToStructure(mt.pbFormat,
					typeof(DirectShow.VIDEOINFOHEADER));
			var width = header.bmiHeader.biWidth;
			var height = header.bmiHeader.biHeight;
			var stride = width * (header.bmiHeader.biBitCount / 8);
			DirectShow.DeleteMediaType(ref mt);

			Size = new Size(width, height);

			// fix screen tearing problem(issue #2)
			// you can use previous method if you swap the comment line below.
			// GetBitmap = () => GetBitmapFromSampleGrabberBuffer(i_grabber, width, height, stride);
			GetBitmap = GetBitmapFromSampleGrabberCallback(i_grabber, width, height, stride);
		}

		// Assign Delegates.
		Start = () => DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Running);
		Stop = () => DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Stopped);
		Release = () =>
		{
			Stop();

			DirectShow.ReleaseInstance(ref i_grabber);
			DirectShow.ReleaseInstance(ref builder);
			DirectShow.ReleaseInstance(ref graph);
		};

		// Properties.
		Properties = new PropertyItems(vcap_source);
	}

	/// <summary>Properties user can adjust.</summary>
	internal PropertyItems Properties { get; private set; }

	internal class PropertyItems
	{
		public PropertyItems(DirectShow.IBaseFilter vcap_source)
		{
			// Pan, Tilt, Roll, Zoom, Exposure, Iris, Focus
			CameraControl = Enum.GetValues(typeof(DirectShow.CameraControlProperty))
				.Cast<DirectShow.CameraControlProperty>()
				.Select(item =>
				{
					Property prop = null;
					try
					{
						var cam_ctrl = vcap_source as DirectShow.IAMCameraControl;
						if (cam_ctrl == null)
							throw new NotSupportedException("no IAMCameraControl Interface."); // will catched.
						int min = 0, max = 0, step = 0, def = 0, flags = 0;
						cam_ctrl.GetRange(item, ref min, ref max, ref step, ref def,
							ref flags); // COMException if not supports.

						Action<DirectShow.CameraControlFlags, int> set = (flag, value) =>
						cam_ctrl.Set(item, value, (int)flag);
						Func<int> get = () =>
						{
							var value = 0;
							cam_ctrl.Get(item, ref value, ref flags);
							return value;
						};
						prop = new Property(min, max, step, def, flags, set, get);
					}
					catch (Exception)
					{
						prop = new Property();
					} // available = false

					return new { Key = item, Value = prop };
				}).ToDictionary(x => x.Key, x => x.Value);

			// Brightness, Contrast, Hue, Saturation, Sharpness, Gamma, ColorEnable, WhiteBalance, BacklightCompensation, Gain
			VideoProcAmp = Enum.GetValues(typeof(DirectShow.VideoProcAmpProperty))
				.Cast<DirectShow.VideoProcAmpProperty>()
				.Select(item =>
				{
					Property prop = null;
					try
					{
						var vid_ctrl = vcap_source as DirectShow.IAMVideoProcAmp;
						if (vid_ctrl == null)
							throw new NotSupportedException("no IAMVideoProcAmp Interface."); // will catched.
						int min = 0, max = 0, step = 0, def = 0, flags = 0;
						vid_ctrl.GetRange(item, ref min, ref max, ref step, ref def,
							ref flags); // COMException if not supports.

						Action<DirectShow.CameraControlFlags, int> set = (flag, value) =>
						vid_ctrl.Set(item, value, (int)flag);
						Func<int> get = () =>
						{
							var value = 0;
							vid_ctrl.Get(item, ref value, ref flags);
							return value;
						};
						prop = new Property(min, max, step, def, flags, set, get);
					}
					catch (Exception)
					{
						prop = new Property();
					} // available = false

					return new { Key = item, Value = prop };
				}).ToDictionary(x => x.Key, x => x.Value);
		}

		/// <summary>Camera Control properties.</summary>
		private Dictionary<DirectShow.CameraControlProperty, Property> CameraControl;

		/// <summary>Video Processing Amplifier properties.</summary>
		private Dictionary<DirectShow.VideoProcAmpProperty, Property> VideoProcAmp;

		/// <summary>Get CameraControl Property. Check Available before use.</summary>
		public Property this[DirectShow.CameraControlProperty item]
		{
			get { return CameraControl[item]; }
		}

		/// <summary>Get VideoProcAmp Property. Check Available before use.</summary>
		public Property this[DirectShow.VideoProcAmpProperty item]
		{
			get { return VideoProcAmp[item]; }
		}

		public class Property
		{
			public int Min { get; private set; }
			public int Max { get; private set; }
			public int Step { get; private set; }
			public int Default { get; private set; }
			public DirectShow.CameraControlFlags Flags { get; private set; }
			public Action<DirectShow.CameraControlFlags, int> SetValue { get; private set; }
			public Func<int> GetValue { get; private set; }
			public bool Available { get; private set; }
			public bool CanAuto { get; private set; }

			public Property()
			{
				SetValue = (flag, value) => { };
				Available = false;
			}

			public Property(int min, int max, int step, int @default, int flags,
				Action<DirectShow.CameraControlFlags, int> set, Func<int> get)
			{
				Min = min;
				Max = max;
				Step = step;
				Default = @default;
				Flags = (DirectShow.CameraControlFlags)flags;
				CanAuto = (Flags & DirectShow.CameraControlFlags.Auto) == DirectShow.CameraControlFlags.Auto;
				SetValue = set;
				GetValue = get;
				Available = true;
			}

			public override string ToString()
			{
				return
					$"Available={Available}, Min={Min}, Max={Max}, Step={Step}, Default={Default}, Flags={Flags}";
			}
		}
	}

	private class SampleGrabberCallback : DirectShow.ISampleGrabberCB
	{
		private byte[] Buffer;
		private object BufferLock = new object();

		public Bitmap
			GetBitmap(int width, int height, int stride)
		{
			if (Buffer == null) return EmptyBitmap(width, height);

			lock (BufferLock)
			{
				return BufferToBitmap(Buffer, width, height, stride);
			}
		}

		// called when each sample completed.
		// The data processing thread blocks until the callback method returns. If the callback does not return quickly, it can interfere with playback.
		public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
		{
			if (Buffer == null || Buffer.Length != BufferLen)
			{
				Buffer = new byte[BufferLen];
			}

			lock (BufferLock)
			{
				Marshal.Copy(pBuffer, Buffer, 0, BufferLen);
			}

			return 0;
		}

		// never called.
		public int SampleCB(double SampleTime, DirectShow.IMediaSample pSample)
		{
			throw new NotImplementedException();
		}
	}

	private Func<Bitmap> GetBitmapFromSampleGrabberCallback(DirectShow.ISampleGrabber i_grabber, int width,
		int height, int stride)
	{
		var sampler = new SampleGrabberCallback();
		i_grabber.SetCallback(sampler, 1); // WhichMethodToCallback = BufferCB
		return () => sampler.GetBitmap(width, height, stride);
	}

	/// <summary>Get Bitmap from Sample Grabber Current Buffer</summary>
	private Bitmap
		GetBitmapFromSampleGrabberBuffer(DirectShow.ISampleGrabber i_grabber, int width, int height, int stride)
	{
		try
		{
			return GetBitmapFromSampleGrabberBufferMain(i_grabber, width, height, stride);
		}
		catch (COMException ex)
		{
			const uint VFW_E_WRONG_STATE = 0x80040227;
			if ((uint)ex.ErrorCode == VFW_E_WRONG_STATE)
			{
				// image data is not ready yet. return empty bitmap.
				return EmptyBitmap(width, height);
			}

			throw;
		}
	}

	/// <summary>Get Bitmap from Sample Grabber Current Buffer</summary>
	private Bitmap
		GetBitmapFromSampleGrabberBufferMain(DirectShow.ISampleGrabber i_grabber, int width, int height, int stride)
	{
		// サンプルグラバから画像を取得するためには
		// まずサイズ0でGetCurrentBufferを呼び出しバッファサイズを取得し
		// バッファ確保して再度GetCurrentBufferを呼び出す。
		// 取得した画像は逆になっているので反転させる必要がある。
		var sz = 0;
		i_grabber.GetCurrentBuffer(ref sz, IntPtr.Zero); // IntPtr.Zeroで呼び出してバッファサイズ取得
		if (sz == 0) return null;

		// メモリ確保し画像データ取得
		var ptr = Marshal.AllocCoTaskMem(sz);
		i_grabber.GetCurrentBuffer(ref sz, ptr);

		// 画像データをbyte配列に入れなおす
		var data = new byte[sz];
		Marshal.Copy(ptr, data, 0, sz);

		// 画像を作成
		var result = BufferToBitmap(data, width, height, stride);

		Marshal.FreeCoTaskMem(ptr);

		return result;
	}

	private static Dictionary<long, byte[]> ArrayBuffer = new Dictionary<long, byte[]>();

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct BgraColor
	{
		[FieldOffset(3)] public readonly byte A;

		[FieldOffset(2)] public readonly byte R;

		[FieldOffset(1)] public readonly byte G;

		[FieldOffset(0)] public readonly byte B;

		/// <summary>
		/// A struct that represents a ARGB color and is aligned as
		/// a BGRA bytefield in memory.
		/// </summary>
		/// <param name="r">Red</param>
		/// <param name="g">Green</param>
		/// <param name="b">Blue</param>
		/// <param name="a">Alpha</param>
		public BgraColor(byte r, byte g, byte b, byte a = byte.MaxValue)
		{
			A = a;
			R = r;
			G = g;
			B = b;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int PixCoord(int x, int y, int w) => x + y * w;

	private static Bitmap BufferToBitmap(byte[] buffer, int width, int height, int stride)
	{
		const double dpi = 96.0;

		var result = new WriteableBitmap(new PixelSize(width, height), new Vector(dpi, dpi),
			PixelFormat.Bgra8888, AlphaFormat.Premul);

		var lenght = height * stride;

		var bgraArray = new BgraColor[height * width];

		for (var y = 0; y < height; y++)
		{
			var src_idx = buffer.Length - stride * (y + 1);

			var curx = 0;
			for (var x = 0; x < stride; x += 3)
			{
				var b24 = buffer[src_idx + x];
				var g24 = buffer[src_idx + x + 1];
				var r24 = buffer[src_idx + x + 2];
				bgraArray[PixCoord(curx, y, width)] = new BgraColor(r24, g24, b24);
				curx++;
			}
		}

		using (var lockedBitmap = result.Lock())
		{
			var _backBufferBytes = height * width * 4;
			unsafe
			{
				fixed (void* src = &bgraArray[0])
					Buffer.MemoryCopy(src, lockedBitmap.Address.ToPointer(), (uint)_backBufferBytes,
						(uint)_backBufferBytes);
			}
		}

		return result;
	}

	private static Bitmap EmptyBitmap(int width, int height)
	{
		return new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888,
			AlphaFormat.Opaque);
	}

	/// <summary>
	/// サンプルグラバを作成する
	/// </summary>
	private DirectShow.IBaseFilter CreateSampleGrabber()
	{
		var filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_SampleGrabber);
		var ismp = filter as DirectShow.ISampleGrabber;

		// サンプル グラバを最初に作成したときは、優先メディア タイプは設定されていない。
		// これは、グラフ内のほぼすべてのフィルタに接続はできるが、受け取るデータ タイプを制御できないとうことである。
		// したがって、残りのグラフを作成する前に、ISampleGrabber::SetMediaType メソッドを呼び出して、
		// サンプル グラバに対してメディア タイプを設定すること。

		// サンプル グラバは、接続した時に他のフィルタが提供するメディア タイプとこの設定されたメディア タイプとを比較する。
		// 調べるフィールドは、メジャー タイプ、サブタイプ、フォーマット タイプだけである。
		// これらのフィールドでは、値 GUID_NULL は "あらゆる値を受け付ける" という意味である。
		// 通常は、メジャー タイプとサブタイプを設定する。

		// https://msdn.microsoft.com/ja-jp/library/cc370616.aspx
		// https://msdn.microsoft.com/ja-jp/library/cc369546.aspx
		// サンプル グラバ フィルタはトップダウン方向 (負の biHeight) のビデオ タイプ、または
		// FORMAT_VideoInfo2 のフォーマット タイプのビデオ タイプはすべて拒否する。

		var mt = new DirectShow.AM_MEDIA_TYPE();
		mt.MajorType = DirectShow.DsGuid.MEDIATYPE_Video;
		mt.SubType = DirectShow.DsGuid.MEDIASUBTYPE_RGB24;
		ismp.SetMediaType(mt);
		return filter;
	}

	/// <summary>
	/// Video Capture Sourceフィルタを作成する
	/// </summary>
	private DirectShow.IBaseFilter CreateVideoCaptureSource(int index, VideoFormat format)
	{
		var filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory, index);
		var pin = DirectShow.FindPin(filter, 0, DirectShow.PIN_DIRECTION.PINDIR_OUTPUT);
		SetVideoOutputFormat(pin, format);
		return filter;
	}

	/// <summary>
	/// ビデオキャプチャデバイスの出力形式を選択する。
	/// </summary>
	private static void SetVideoOutputFormat(DirectShow.IPin pin, VideoFormat format)
	{
		var formats = GetVideoOutputFormat(pin);

		// 仕様ではVideoCaptureDeviceはメディア タイプごとに一定範囲の出力フォーマットをサポートできる。例えば以下のように。
		// [0]:YUY2 最小:160x120, 最大:320x240, X軸4STEP, Y軸2STEPごと
		// [1]:RGB8 最小:640x480, 最大:640x480, X軸0STEP, Y軸0STEPごと
		// SetFormatで出力サイズとフレームレートをこの範囲内で設定可能。
		// ただし試した限り、手持ちのUSBカメラはすべてサイズ固定(最大・最小が同じ)で返してきた。

		// https://msdn.microsoft.com/ja-jp/windows/dd407352(v=vs.80)
		// VIDEO_STREAM_CONFIG_CAPSの以下を除くほとんどのメンバーはdeprecated(非推奨)である。
		// アプリケーションはその他のメンバーの利用を避けること。かわりにIAMStreamConfig::GetFormatを利用すること。
		// - Guid:FORMAT_VideoInfo or FORMAT_VideoInfo2など。
		// - VideoStandard:アナログTV信号のフォーマット(NTSC, PALなど)をAnalogVideoStandard列挙体で指定する。
		// - MinFrameInterval, MaxFrameInterval:ビデオキャプチャデバイスがサポートするフレームレートの範囲。100ナノ秒単位。

		// 上記によると、VIDEO_STREAM_CONFIG_CAPSは現在はdeprecated(非推奨)であるらしい。かわりにIAMStreamConfig::GetFormatを使用することらしい。
		// 上記仕様を守ったデバイスは出力サイズを固定で返すが、守ってない古いデバイスは出力サイズを可変で返す、と考えられる。
		// 参考までに、VIDEO_STREAM_CONFIG_CAPSで解像度・クロップサイズ・フレームレートなどを変更する手順は以下の通り。

		// ①フレームレート(これは非推奨ではない)
		// VIDEO_STREAM_CONFIG_CAPS のメンバ MinFrameInterval と MaxFrameInterval は各ビデオ フレームの最小の長さと最大の長さである。
		// 次の式を使って、これらの値をフレーム レートに変換できる。
		// frames per second = 10,000,000 / frame duration

		// 特定のフレーム レートを要求するには、メディア タイプにある構造体 VIDEOINFOHEADER か VIDEOINFOHEADER2 の AvgTimePerFrame の値を変更する。
		// デバイスは最小値と最大値の間で可能なすべての値はサポートしていないことがあるため、ドライバは使用可能な最も近い値を使う。

		// ②Cropping(画像の一部切り抜き)
		// MinCroppingSize = (160, 120) // Cropping最小サイズ。
		// MaxCroppingSize = (320, 240) // Cropping最大サイズ。
		// CropGranularityX = 4         // 水平方向細分度。
		// CropGranularityY = 8         // 垂直方向細分度。
		// CropAlignX = 2               // the top-left corner of the source rectangle can sit.
		// CropAlignY = 4               // the top-left corner of the source rectangle can sit.

		// ③出力サイズ
		// https://msdn.microsoft.com/ja-jp/library/cc353344.aspx
		// https://msdn.microsoft.com/ja-jp/library/cc371290.aspx
		// VIDEO_STREAM_CONFIG_CAPS 構造体は、このメディア タイプに使える最小と最大の幅と高さを示す。
		// また、"ステップ" サイズ"も示す。ステップ サイズは、幅または高さを調整できるインクリメントの値を定義する。
		// たとえば、デバイスは次の値を返すことがある。
		// MinOutputSize: 160 × 120
		// MaxOutputSize: 320 × 240
		// OutputGranularityX:8 ピクセル (水平ステップ サイズ)
		// OutputGranularityY:8 ピクセル (垂直ステップ サイズ)
		// これらの数値が与えられると、幅は範囲内 (160、168、176、... 304、312、320) の任意の値に、
		// 高さは範囲内 (120、128、136、... 224、232、240) の任意の値に設定できる。

		// 出力サイズの可変のUSBカメラがないためデバッグするには以下のコメントを外す。
		// I have no USB camera of variable output size, uncomment below to debug.
		//size = new Size(168, 126);
		//vformat[0].Caps = new DirectShow.VIDEO_STREAM_CONFIG_CAPS()
		//{
		//    Guid = DirectShow.DsGuid.FORMAT_VideoInfo,
		//    MinOutputSize = new DirectShow.SIZE() { cx = 160, cy = 120 },
		//    MaxOutputSize = new DirectShow.SIZE() { cx = 320, cy = 240 },
		//    OutputGranularityX = 4,
		//    OutputGranularityY = 2
		//};

		// VIDEO_STREAM_CONFIG_CAPSは現在では非推奨。まずは固定サイズを探す
		// VIDEO_STREAM_CONFIG_CAPS is deprecated. First, find just the fixed size.
		for (var i = 0; i < formats.Length; i++)
		{
			var item = formats[i];

			// VideoInfoのみ対応する。(VideoInfo2はSampleGrabber未対応のため)
			// VideoInfo only... (SampleGrabber do not support VideoInfo2)
			// https://msdn.microsoft.com/ja-jp/library/cc370616.aspx
			if (item.MajorType != DirectShow.DsGuid.GetNickname(DirectShow.DsGuid.MEDIATYPE_Video)) continue;
			if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType) continue;
			if (item.Caps.Guid != DirectShow.DsGuid.FORMAT_VideoInfo) continue;

			if (item.Size.Width == format.Size.Width && item.Size.Height == format.Size.Height)
			{
				SetVideoOutputFormat(pin, i, format.Size, format.TimePerFrame);
				return;
			}
		}

		// 固定サイズが見つからなかった。可変サイズの範囲を探す。
		// Not found fixed size, search for variable size.
		for (var i = 0; i < formats.Length; i++)
		{
			var item = formats[i];

			// VideoInfoのみ対応する。(VideoInfo2はSampleGrabber未対応のため)
			// VideoInfo only... (SampleGrabber do not support VideoInfo2)
			// https://msdn.microsoft.com/ja-jp/library/cc370616.aspx
			if (item.MajorType != DirectShow.DsGuid.GetNickname(DirectShow.DsGuid.MEDIATYPE_Video)) continue;
			if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType) continue;
			if (item.Caps.Guid != DirectShow.DsGuid.FORMAT_VideoInfo) continue;

			if (item.Caps.OutputGranularityX == 0) continue;
			if (item.Caps.OutputGranularityY == 0) continue;

			for (var w = item.Caps.MinOutputSize.cx;
				w < item.Caps.MaxOutputSize.cx;
				w += item.Caps.OutputGranularityX)
			{
				for (var h = item.Caps.MinOutputSize.cy;
					h < item.Caps.MaxOutputSize.cy;
					h += item.Caps.OutputGranularityY)
				{
					if (w == format.Size.Width && h == format.Size.Height)
					{
						SetVideoOutputFormat(pin, i, format.Size, format.TimePerFrame);
						return;
					}
				}
			}
		}

		// サイズが見つかなかった場合はデフォルトサイズとする。
		// Not found, use default size.
		SetVideoOutputFormat(pin, 0, Size.Empty, 0);
	}

	/// <summary>
	/// ビデオキャプチャデバイスがサポートするメディアタイプ・サイズを取得する。
	/// </summary>
	private static VideoFormat[] GetVideoOutputFormat(DirectShow.IPin pin)
	{
		// IAMStreamConfigインタフェース取得
		if (!(pin is DirectShow.IAMStreamConfig config))
		{
			throw new InvalidOperationException("no IAMStreamConfig interface.");
		}

		// フォーマット個数取得
		int cap_count = 0, cap_size = 0;
		config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
		if (cap_size != Marshal.SizeOf(typeof(DirectShow.VIDEO_STREAM_CONFIG_CAPS)))
		{
			throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
		}

		// 返却値の確保
		var result = new VideoFormat[cap_count];

		// データ用領域確保
		var cap_data = Marshal.AllocHGlobal(cap_size);

		// 列挙
		for (var i = 0; i < cap_count; i++)
		{
			var entry = new VideoFormat();

			// x番目のフォーマット情報取得
			DirectShow.AM_MEDIA_TYPE mt = null;
			config.GetStreamCaps(i, ref mt, cap_data);
			entry.Caps = PtrToStructure<DirectShow.VIDEO_STREAM_CONFIG_CAPS>(cap_data);

			// フォーマット情報の読み取り
			entry.MajorType = DirectShow.DsGuid.GetNickname(mt.MajorType);
			entry.SubType = DirectShow.DsGuid.GetNickname(mt.SubType);

			if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo)
			{
				var vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER>(mt.pbFormat);
				entry.Size = new Size(vinfo.bmiHeader.biWidth, vinfo.bmiHeader.biHeight);
				entry.TimePerFrame = vinfo.AvgTimePerFrame;
			}
			else if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo2)
			{
				var vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER2>(mt.pbFormat);
				entry.Size = new Size(vinfo.bmiHeader.biWidth, vinfo.bmiHeader.biHeight);
				entry.TimePerFrame = vinfo.AvgTimePerFrame;
			}

			// 解放
			DirectShow.DeleteMediaType(ref mt);

			result[i] = entry;
		}

		// 解放
		Marshal.FreeHGlobal(cap_data);

		return result;
	}

	/// <summary>
	/// ビデオキャプチャデバイスの出力形式を選択する。
	/// 事前にGetVideoOutputFormatでメディアタイプ・サイズを得ておき、その中から希望のindexを指定する。
	/// 同時に出力サイズとフレームレートを変更することができる。
	/// </summary>
	/// <param name="index">希望のindexを指定する</param>
	/// <param name="size">Empty以外を指定すると出力サイズを変更する。事前にVIDEO_STREAM_CONFIG_CAPSで取得した可能範囲内を指定すること。</param>
	/// <param name="timePerFrame">0以上を指定するとフレームレートを変更する。事前にVIDEO_STREAM_CONFIG_CAPSで取得した可能範囲内を指定すること。</param>
	private static void SetVideoOutputFormat(DirectShow.IPin pin, int index, Size size, long timePerFrame)
	{
		// IAMStreamConfigインタフェース取得
		var config = pin as DirectShow.IAMStreamConfig;
		if (config == null)
		{
			throw new InvalidOperationException("no IAMStreamConfig interface.");
		}

		// フォーマット個数取得
		int cap_count = 0, cap_size = 0;
		config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
		if (cap_size != Marshal.SizeOf(typeof(DirectShow.VIDEO_STREAM_CONFIG_CAPS)))
		{
			throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
		}

		// データ用領域確保
		var cap_data = Marshal.AllocHGlobal(cap_size);

		// idx番目のフォーマット情報取得
		DirectShow.AM_MEDIA_TYPE mt = null;
		config.GetStreamCaps(index, ref mt, cap_data);
		var cap = PtrToStructure<DirectShow.VIDEO_STREAM_CONFIG_CAPS>(cap_data);

		if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo)
		{
			var vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER>(mt.pbFormat);
			if (!size.IsDefault)
			{
				vinfo.bmiHeader.biWidth = (int)size.Width;
				vinfo.bmiHeader.biHeight = (int)size.Height;
			}

			if (timePerFrame > 0)
			{
				vinfo.AvgTimePerFrame = timePerFrame;
			}

			Marshal.StructureToPtr(vinfo, mt.pbFormat, true);
		}
		else if (mt.FormatType == DirectShow.DsGuid.FORMAT_VideoInfo2)
		{
			var vinfo = PtrToStructure<DirectShow.VIDEOINFOHEADER2>(mt.pbFormat);
			if (!size.IsDefault)
			{
				vinfo.bmiHeader.biWidth = (int)size.Width;
				vinfo.bmiHeader.biHeight = (int)size.Height;
			}

			if (timePerFrame > 0)
			{
				vinfo.AvgTimePerFrame = timePerFrame;
			}

			Marshal.StructureToPtr(vinfo, mt.pbFormat, true);
		}

		// フォーマットを選択
		config.SetFormat(mt);

		// 解放
		if (cap_data != IntPtr.Zero) Marshal.FreeHGlobal(cap_data);
		if (mt != null) DirectShow.DeleteMediaType(ref mt);
	}

	private static T PtrToStructure<T>(IntPtr ptr)
	{
		return (T)Marshal.PtrToStructure(ptr, typeof(T));
	}

	public class VideoFormat
	{
		public string MajorType { get; set; } // [Video]など
		public string SubType { get; set; } // [YUY2], [MJPG]など
		public Size Size { get; set; } // ビデオサイズ
		public long TimePerFrame { get; set; } // ビデオフレームの平均表示時間を100ナノ秒単位で。30fpsのとき「333333」
		public DirectShow.VIDEO_STREAM_CONFIG_CAPS Caps { get; set; }

		public override string ToString()
		{
			return $"{MajorType}, {SubType}, {Size}, {TimePerFrame}, {CapsString()}";
		}

		private string CapsString()
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{0}, ", DirectShow.DsGuid.GetNickname(Caps.Guid));
			foreach (var info in Caps.GetType().GetFields())
			{
				sb.AppendFormat("{0}={1}, ", info.Name, info.GetValue(Caps));
			}

			return sb.ToString();
		}
	}
}

#pragma warning restore IDE0011 // Add braces
#pragma warning restore IDE0019 // Use pattern matching
#pragma warning restore IDE1006 // Naming Styles
