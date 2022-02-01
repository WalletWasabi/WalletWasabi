using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WalletWasabi.Fluent.Models.Windows;
#pragma warning disable IDE0019 // Use pattern matching
#pragma warning disable IDE1006 // Naming Styles

public class WindowsCapture
{
	private static Dictionary<long, byte[]> ArrayBuffer = new();

	public WindowsCapture(int cameraIndex, Size size) : this(cameraIndex, new VideoFormat { Size = size })
	{
	}

	public WindowsCapture(int cameraIndex, VideoFormat format)
	{
		var camera_list = FindDevices();
		if (cameraIndex >= camera_list.Length)
		{
			throw new ArgumentException("USB camera is not available.", "cameraIndex");
		}

		Init(cameraIndex, format);
	}

	public Size Size { get; private set; }

	public Action Start { get; private set; }

	public Action Stop { get; private set; }

	public Action Release { get; private set; }

	public Func<Bitmap> GetBitmap { get; private set; }

	internal PropertyItems Properties { get; private set; }

	public static string[] FindDevices()
	{
		return DirectShow.GetFilters(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory).ToArray();
	}

	public static VideoFormat[] GetVideoFormat(int cameraIndex)
	{
		var filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory, cameraIndex);
		var pin = DirectShow.FindPin(filter, 0, DirectShow.PIN_DIRECTION.PINDIR_OUTPUT);
		return GetVideoOutputFormat(pin);
	}

	private void Init(int index, VideoFormat format)
	{
		var graph = DirectShow.CreateGraph();

		var vcap_source = CreateVideoCaptureSource(index, format);
		graph.AddFilter(vcap_source, "VideoCapture");

		var grabber = CreateSampleGrabber();
		graph.AddFilter(grabber, "SampleGrabber");
		var i_grabber = (DirectShow.ISampleGrabber)grabber;
		i_grabber.SetBufferSamples(true);

		var renderer = DirectShow.CoCreateInstance(DirectShow.DsGuid.CLSID_NullRenderer) as DirectShow.IBaseFilter;
		graph.AddFilter(renderer, "NullRenderer");

		var builder =
			DirectShow.CoCreateInstance(DirectShow.DsGuid.CLSID_CaptureGraphBuilder2) as
				DirectShow.ICaptureGraphBuilder2;
		builder.SetFiltergraph(graph);
		var pinCategory = DirectShow.DsGuid.PIN_CATEGORY_CAPTURE;
		var mediaType = DirectShow.DsGuid.MEDIATYPE_Video;
		builder.RenderStream(ref pinCategory, ref mediaType, vcap_source, grabber, renderer);

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

		GetBitmap = GetBitmapFromSampleGrabberCallback(i_grabber, width, height, stride);

		Start = () => DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Running);
		Stop = () => DirectShow.PlayGraph(graph, DirectShow.FILTER_STATE.Stopped);
		Release = () =>
		{
			Stop();

			DirectShow.ReleaseInstance(ref i_grabber);
			DirectShow.ReleaseInstance(ref builder);
			DirectShow.ReleaseInstance(ref graph);
		};

		Properties = new PropertyItems(vcap_source);
	}

	private Func<Bitmap> GetBitmapFromSampleGrabberCallback(DirectShow.ISampleGrabber i_grabber, int width,
		int height, int stride)
	{
		var sampler = new SampleGrabberCallback();
		i_grabber.SetCallback(sampler, 1);
		return () => sampler.GetBitmap(width, height, stride);
	}

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
				return EmptyBitmap(width, height);
			}

			throw;
		}
	}

	private Bitmap
		GetBitmapFromSampleGrabberBufferMain(DirectShow.ISampleGrabber i_grabber, int width, int height, int stride)
	{
		var sz = 0;
		i_grabber.GetCurrentBuffer(ref sz, IntPtr.Zero);
		if (sz == 0)
		{
			return null;
		}

		var ptr = Marshal.AllocCoTaskMem(sz);
		i_grabber.GetCurrentBuffer(ref sz, ptr);

		var data = new byte[sz];
		Marshal.Copy(ptr, data, 0, sz);

		var result = BufferToBitmap(data, width, height, stride);

		Marshal.FreeCoTaskMem(ptr);

		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int PixCoord(int x, int y, int w)
	{
		return x + y * w;
	}

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
				{
					Buffer.MemoryCopy(src, lockedBitmap.Address.ToPointer(), (uint)_backBufferBytes,
						(uint)_backBufferBytes);
				}
			}
		}

		return result;
	}

	private static Bitmap EmptyBitmap(int width, int height)
	{
		return new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888,
			AlphaFormat.Opaque);
	}

	private DirectShow.IBaseFilter CreateSampleGrabber()
	{
		var filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_SampleGrabber);
		var ismp = filter as DirectShow.ISampleGrabber;

		var mt = new DirectShow.AM_MEDIA_TYPE();
		mt.MajorType = DirectShow.DsGuid.MEDIATYPE_Video;
		mt.SubType = DirectShow.DsGuid.MEDIASUBTYPE_RGB24;
		ismp.SetMediaType(mt);
		return filter;
	}

	private DirectShow.IBaseFilter CreateVideoCaptureSource(int index, VideoFormat format)
	{
		var filter = DirectShow.CreateFilter(DirectShow.DsGuid.CLSID_VideoInputDeviceCategory, index);
		var pin = DirectShow.FindPin(filter, 0, DirectShow.PIN_DIRECTION.PINDIR_OUTPUT);
		SetVideoOutputFormat(pin, format);
		return filter;
	}

	private static void SetVideoOutputFormat(DirectShow.IPin pin, VideoFormat format)
	{
		var formats = GetVideoOutputFormat(pin);

		for (var i = 0; i < formats.Length; i++)
		{
			var item = formats[i];

			if (item.MajorType != DirectShow.DsGuid.GetNickname(DirectShow.DsGuid.MEDIATYPE_Video))
			{
				continue;
			}

			if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType)
			{
				continue;
			}

			if (item.Caps.Guid != DirectShow.DsGuid.FORMAT_VideoInfo)
			{
				continue;
			}

			if (item.Size.Width == format.Size.Width && item.Size.Height == format.Size.Height)
			{
				SetVideoOutputFormat(pin, i, format.Size, format.TimePerFrame);
				return;
			}
		}

		for (var i = 0; i < formats.Length; i++)
		{
			var item = formats[i];

			if (item.MajorType != DirectShow.DsGuid.GetNickname(DirectShow.DsGuid.MEDIATYPE_Video))
			{
				continue;
			}

			if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType)
			{
				continue;
			}

			if (item.Caps.Guid != DirectShow.DsGuid.FORMAT_VideoInfo)
			{
				continue;
			}

			if (item.Caps.OutputGranularityX == 0)
			{
				continue;
			}

			if (item.Caps.OutputGranularityY == 0)
			{
				continue;
			}

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

		SetVideoOutputFormat(pin, 0, Size.Empty, 0);
	}

	private static VideoFormat[] GetVideoOutputFormat(DirectShow.IPin pin)
	{
		if (!(pin is DirectShow.IAMStreamConfig config))
		{
			throw new InvalidOperationException("no IAMStreamConfig interface.");
		}

		int cap_count = 0, cap_size = 0;
		config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
		if (cap_size != Marshal.SizeOf(typeof(DirectShow.VIDEO_STREAM_CONFIG_CAPS)))
		{
			throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
		}

		var result = new VideoFormat[cap_count];

		var cap_data = Marshal.AllocHGlobal(cap_size);

		for (var i = 0; i < cap_count; i++)
		{
			var entry = new VideoFormat();

			DirectShow.AM_MEDIA_TYPE mt = null;
			config.GetStreamCaps(i, ref mt, cap_data);
			entry.Caps = PtrToStructure<DirectShow.VIDEO_STREAM_CONFIG_CAPS>(cap_data);

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

			DirectShow.DeleteMediaType(ref mt);

			result[i] = entry;
		}

		Marshal.FreeHGlobal(cap_data);

		return result;
	}

	private static void SetVideoOutputFormat(DirectShow.IPin pin, int index, Size size, long timePerFrame)
	{
		var config = pin as DirectShow.IAMStreamConfig;
		if (config == null)
		{
			throw new InvalidOperationException("no IAMStreamConfig interface.");
		}

		int cap_count = 0, cap_size = 0;
		config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
		if (cap_size != Marshal.SizeOf(typeof(DirectShow.VIDEO_STREAM_CONFIG_CAPS)))
		{
			throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
		}

		var cap_data = Marshal.AllocHGlobal(cap_size);

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

		config.SetFormat(mt);

		if (cap_data != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(cap_data);
		}

		if (mt != null)
		{
			DirectShow.DeleteMediaType(ref mt);
		}
	}

	private static T PtrToStructure<T>(IntPtr ptr)
	{
		return (T)Marshal.PtrToStructure(ptr, typeof(T));
	}

	internal class PropertyItems
	{
		private readonly Dictionary<DirectShow.CameraControlProperty, Property> CameraControl;

		private readonly Dictionary<DirectShow.VideoProcAmpProperty, Property> VideoProcAmp;

		public PropertyItems(DirectShow.IBaseFilter vcap_source)
		{
			CameraControl = Enum.GetValues(typeof(DirectShow.CameraControlProperty))
				.Cast<DirectShow.CameraControlProperty>()
				.Select(item =>
				{
					Property prop = null;
					try
					{
						var cam_ctrl = vcap_source as DirectShow.IAMCameraControl;
						if (cam_ctrl == null)
						{
							throw new NotSupportedException("no IAMCameraControl Interface.");
						}

						int min = 0, max = 0, step = 0, def = 0, flags = 0;
						cam_ctrl.GetRange(item, ref min, ref max, ref step, ref def,
							ref flags);

						Action<DirectShow.CameraControlFlags, int> set = (flag, value) =>
							cam_ctrl.Set(item, value, (int)flag);
						var get = () =>
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
					}

					return new { Key = item, Value = prop };
				}).ToDictionary(x => x.Key, x => x.Value);

			VideoProcAmp = Enum.GetValues(typeof(DirectShow.VideoProcAmpProperty))
				.Cast<DirectShow.VideoProcAmpProperty>()
				.Select(item =>
				{
					Property prop = null;
					try
					{
						var vid_ctrl = vcap_source as DirectShow.IAMVideoProcAmp;
						if (vid_ctrl == null)
						{
							throw new NotSupportedException("no IAMVideoProcAmp Interface.");
						}

						int min = 0, max = 0, step = 0, def = 0, flags = 0;
						vid_ctrl.GetRange(item, ref min, ref max, ref step, ref def,
							ref flags);

						Action<DirectShow.CameraControlFlags, int> set = (flag, value) =>
							vid_ctrl.Set(item, value, (int)flag);
						var get = () =>
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
					}

					return new { Key = item, Value = prop };
				}).ToDictionary(x => x.Key, x => x.Value);
		}

		public Property this[DirectShow.CameraControlProperty item] => CameraControl[item];

		public Property this[DirectShow.VideoProcAmpProperty item] => VideoProcAmp[item];

		public class Property
		{
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

			public int Min { get; }
			public int Max { get; }
			public int Step { get; }
			public int Default { get; }
			public DirectShow.CameraControlFlags Flags { get; }
			public Action<DirectShow.CameraControlFlags, int> SetValue { get; }
			public Func<int> GetValue { get; }
			public bool Available { get; }
			public bool CanAuto { get; }

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
		private readonly object BufferLock = new();

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

		public int SampleCB(double SampleTime, DirectShow.IMediaSample pSample)
		{
			return 0;
			//throw new NotImplementedException();
		}

		public Bitmap
			GetBitmap(int width, int height, int stride)
		{
			if (Buffer == null)
			{
				return EmptyBitmap(width, height);
			}

			lock (BufferLock)
			{
				return BufferToBitmap(Buffer, width, height, stride);
			}
		}
	}

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct BgraColor
	{
		[FieldOffset(3)] public readonly byte A;

		[FieldOffset(2)] public readonly byte R;

		[FieldOffset(1)] public readonly byte G;

		[FieldOffset(0)] public readonly byte B;

		public BgraColor(byte r, byte g, byte b, byte a = byte.MaxValue)
		{
			A = a;
			R = r;
			G = g;
			B = b;
		}
	}

	public class VideoFormat
	{
		public string MajorType { get; set; }
		public string SubType { get; set; }
		public Size Size { get; set; }
		public long TimePerFrame { get; set; }
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
