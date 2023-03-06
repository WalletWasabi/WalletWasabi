using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using static WalletWasabi.Fluent.Models.Windows.DirectShow;

namespace WalletWasabi.Fluent.Models.Windows;

[SupportedOSPlatform("windows")]
public class WindowsCapture
{
	private static Dictionary<long, byte[]> ArrayBuffer = new();

	public WindowsCapture(int cameraIndex, Size size) : this(cameraIndex, new VideoFormat { Size = size })
	{
	}

	public WindowsCapture(int index, VideoFormat format)
	{
		var camera_list = FindDevices();
		if (index >= camera_list.Length)
		{
			throw new ArgumentException("USB camera is not available.", nameof(index));
		}

		IGraphBuilder? graph = CreateGraph();
		if (graph is not { })
		{
			throw new ArgumentException(nameof(graph));
		}
		var vcap_source = CreateVideoCaptureSource(index, format);
		graph.AddFilter(vcap_source, "VideoCapture");

		IBaseFilter? grabber = CreateSampleGrabber();
		if (grabber is not { })
		{
			throw new ArgumentException(nameof(grabber));
		}
		graph.AddFilter(grabber, "SampleGrabber");
		var i_grabber = (ISampleGrabber)grabber;
		i_grabber.SetBufferSamples(true);

		var renderer = CoCreateInstance(DsGuid.CLSID_NullRenderer) as IBaseFilter;
		graph.AddFilter(renderer, "NullRenderer");

		ICaptureGraphBuilder2? builder =
			CoCreateInstance(DsGuid.CLSID_CaptureGraphBuilder2) as
				ICaptureGraphBuilder2;
		builder?.SetFiltergraph(graph);
		var pinCategory = DsGuid.PIN_CATEGORY_CAPTURE;
		var mediaType = DsGuid.MEDIATYPE_Video;
		builder?.RenderStream(ref pinCategory, ref mediaType, vcap_source, grabber, renderer);

		var mt = new AM_MEDIA_TYPE();
		i_grabber.GetConnectedMediaType(mt);
		VIDEOINFOHEADER header = (VIDEOINFOHEADER)Marshal.PtrToStructure(mt.pbFormat, typeof(VIDEOINFOHEADER))!;
		var width = header.bmiHeader.biWidth;
		var height = header.bmiHeader.biHeight;
		var stride = width * (header.bmiHeader.biBitCount / 8);
		DeleteMediaType(ref mt);

		Size = new Size(width, height);

		GetBitmap = GetBitmapFromSampleGrabberCallback(i_grabber, width, height, stride);

		Start = () => PlayGraph(graph, FILTER_STATE.Running);
		Stop = () => PlayGraph(graph, FILTER_STATE.Stopped);
		Release = () =>
		{
			Stop();

			ReleaseInstance(ref i_grabber);
			ReleaseInstance(ref builder);
			ReleaseInstance(ref graph);
		};

		Properties = new PropertyItems(vcap_source);
	}

	public Size Size { get; private set; }

	public Action Start { get; private set; }

	public Action Stop { get; private set; }

	public Action Release { get; private set; }

	public Func<Bitmap> GetBitmap { get; private set; }

	internal PropertyItems Properties { get; private set; }

	public static string[] FindDevices()
	{
		return GetFilters(DsGuid.CLSID_VideoInputDeviceCategory).ToArray();
	}

	public static VideoFormat[] GetVideoFormat(int cameraIndex)
	{
		var filter = CreateFilter(DsGuid.CLSID_VideoInputDeviceCategory, cameraIndex);
		var pin = FindPin(filter, 0, PIN_DIRECTION.PINDIR_OUTPUT);
		return GetVideoOutputFormat(pin);
	}

	private Func<Bitmap> GetBitmapFromSampleGrabberCallback(ISampleGrabber i_grabber, int width,
		int height, int stride)
	{
		var sampler = new SampleGrabberCallback();
		i_grabber.SetCallback(sampler, 1);
		return () => sampler.GetBitmap(width, height, stride);
	}

	private Bitmap? GetBitmapFromSampleGrabberBuffer(ISampleGrabber i_grabber, int width, int height, int stride)
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

	private Bitmap? GetBitmapFromSampleGrabberBufferMain(ISampleGrabber i_grabber, int width, int height, int stride)
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
		const double DPI = 96.0;

		var result = new WriteableBitmap(new PixelSize(width, height), new Vector(DPI, DPI),
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
			var backBufferBytes = height * width * 4;
			unsafe
			{
				fixed (void* src = &bgraArray[0])
				{
					Buffer.MemoryCopy(src, lockedBitmap.Address.ToPointer(), (uint)backBufferBytes,
						(uint)backBufferBytes);
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

	private IBaseFilter? CreateSampleGrabber()
	{
		var filter = CreateFilter(DsGuid.CLSID_SampleGrabber);
		ISampleGrabber? ismp = filter as ISampleGrabber;

		var mt = new AM_MEDIA_TYPE();
		mt.MajorType = DsGuid.MEDIATYPE_Video;
		mt.SubType = DsGuid.MEDIASUBTYPE_RGB24;
		ismp?.SetMediaType(mt);
		return filter;
	}

	private IBaseFilter CreateVideoCaptureSource(int index, VideoFormat format)
	{
		var filter = CreateFilter(DsGuid.CLSID_VideoInputDeviceCategory, index);
		var pin = FindPin(filter, 0, PIN_DIRECTION.PINDIR_OUTPUT);
		SetVideoOutputFormat(pin, format);
		return filter;
	}

	private static void SetVideoOutputFormat(IPin pin, VideoFormat format)
	{
		var formats = GetVideoOutputFormat(pin);

		for (var i = 0; i < formats.Length; i++)
		{
			var item = formats[i];

			if (item.MajorType != DsGuid.GetNickname(DsGuid.MEDIATYPE_Video))
			{
				continue;
			}

			if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType)
			{
				continue;
			}

			if (item.Caps.Guid != DsGuid.FORMAT_VideoInfo)
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

			if (item.MajorType != DsGuid.GetNickname(DsGuid.MEDIATYPE_Video))
			{
				continue;
			}

			if (string.IsNullOrEmpty(format.SubType) == false && format.SubType != item.SubType)
			{
				continue;
			}

			if (item.Caps.Guid != DsGuid.FORMAT_VideoInfo)
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

	private static VideoFormat[] GetVideoOutputFormat(IPin pin)
	{
		if (pin is not IAMStreamConfig config)
		{
			throw new InvalidOperationException("no IAMStreamConfig interface.");
		}

		int cap_count = 0, cap_size = 0;
		config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
		if (cap_size != Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS)))
		{
			throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
		}

		var result = new VideoFormat[cap_count];

		var cap_data = Marshal.AllocHGlobal(cap_size);

		for (var i = 0; i < cap_count; i++)
		{
			var entry = new VideoFormat();

			AM_MEDIA_TYPE? mt = null;
			config.GetStreamCaps(i, ref mt, cap_data);
			entry.Caps = PtrToStructure<VIDEO_STREAM_CONFIG_CAPS>(cap_data);

			if (mt is not { })
			{
				continue;
			}
			entry.MajorType = DsGuid.GetNickname(mt.MajorType);
			entry.SubType = DsGuid.GetNickname(mt.SubType);

			if (mt.FormatType == DsGuid.FORMAT_VideoInfo)
			{
				var vinfo = PtrToStructure<VIDEOINFOHEADER>(mt.pbFormat);
				entry.Size = new Size(vinfo.bmiHeader.biWidth, vinfo.bmiHeader.biHeight);
				entry.TimePerFrame = vinfo.AvgTimePerFrame;
			}
			else if (mt.FormatType == DsGuid.FORMAT_VideoInfo2)
			{
				var vinfo = PtrToStructure<VIDEOINFOHEADER2>(mt.pbFormat);
				entry.Size = new Size(vinfo.bmiHeader.biWidth, vinfo.bmiHeader.biHeight);
				entry.TimePerFrame = vinfo.AvgTimePerFrame;
			}

			DeleteMediaType(ref mt);

			result[i] = entry;
		}

		Marshal.FreeHGlobal(cap_data);

		return result;
	}

	private static void SetVideoOutputFormat(IPin pin, int index, Size size, long timePerFrame)
	{
		if (pin is not IAMStreamConfig config)
		{
			throw new InvalidOperationException("no IAMStreamConfig interface.");
		}

		int cap_count = 0, cap_size = 0;
		config.GetNumberOfCapabilities(ref cap_count, ref cap_size);
		if (cap_size != Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS)))
		{
			throw new InvalidOperationException("no VIDEO_STREAM_CONFIG_CAPS.");
		}

		var cap_data = Marshal.AllocHGlobal(cap_size);

		AM_MEDIA_TYPE? mt = null;
		config.GetStreamCaps(index, ref mt, cap_data);
		_ = PtrToStructure<VIDEO_STREAM_CONFIG_CAPS>(cap_data);

		if (mt?.FormatType == DsGuid.FORMAT_VideoInfo)
		{
			var vinfo = PtrToStructure<VIDEOINFOHEADER>(mt.pbFormat);
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
		else if (mt?.FormatType == DsGuid.FORMAT_VideoInfo2)
		{
			var vinfo = PtrToStructure<VIDEOINFOHEADER2>(mt.pbFormat);
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
			DeleteMediaType(ref mt);
		}
	}

	private static T? PtrToStructure<T>(IntPtr ptr) => (T?)Marshal.PtrToStructure(ptr, typeof(T));

	internal class PropertyItems
	{
		private readonly Dictionary<CameraControlProperty, Property> _cameraControl;

		private readonly Dictionary<VideoProcAmpProperty, Property> _videoProcAmp;

		public PropertyItems(IBaseFilter vcap_source)
		{
			_cameraControl = Enum.GetValues(typeof(CameraControlProperty))
				.Cast<CameraControlProperty>()
				.Select(item =>
				{
					Property? prop = null;
					try
					{
						if (vcap_source is not IAMCameraControl cam_ctrl)
						{
							throw new NotSupportedException("no IAMCameraControl Interface.");
						}

						int min = 0, max = 0, step = 0, def = 0, flags = 0;
						cam_ctrl.GetRange(item, ref min, ref max, ref step, ref def,
							ref flags);

						void Set(CameraControlFlags flag, int value) => cam_ctrl.Set(item, value, (int)flag);

						var get = () =>
						{
							var value = 0;
							cam_ctrl.Get(item, ref value, ref flags);
							return value;
						};
						prop = new Property(min, max, step, def, flags, Set, get);
					}
					catch (Exception)
					{
						prop = new Property();
					}

					return new { Key = item, Value = prop };
				}).ToDictionary(x => x.Key, x => x.Value);

			_videoProcAmp = Enum.GetValues(typeof(VideoProcAmpProperty))
				.Cast<VideoProcAmpProperty>()
				.Select(item =>
				{
					Property? prop = null;
					try
					{
						if (vcap_source is not IAMVideoProcAmp vid_ctrl)
						{
							throw new NotSupportedException("no IAMVideoProcAmp Interface.");
						}

						int min = 0, max = 0, step = 0, def = 0, flags = 0;
						vid_ctrl.GetRange(item, ref min, ref max, ref step, ref def,
							ref flags);

						void Set(CameraControlFlags flag, int value) => vid_ctrl.Set(item, value, (int)flag);

						var get = () =>
						{
							var value = 0;
							vid_ctrl.Get(item, ref value, ref flags);
							return value;
						};
						prop = new Property(min, max, step, def, flags, Set, get);
					}
					catch (Exception)
					{
						prop = new Property();
					}

					return new { Key = item, Value = prop };
				}).ToDictionary(x => x.Key, x => x.Value);
		}

		public Property this[CameraControlProperty item] => _cameraControl[item];

		public Property this[VideoProcAmpProperty item] => _videoProcAmp[item];

		public class Property
		{
			public Property()
			{
				SetValue = (flag, value) => { };
				Available = false;
			}

			public Property(int min, int max, int step, int @default, int flags,
				Action<CameraControlFlags, int> set, Func<int> get)
			{
				Min = min;
				Max = max;
				Step = step;
				Default = @default;
				Flags = (CameraControlFlags)flags;
				CanAuto = (Flags & CameraControlFlags.Auto) == CameraControlFlags.Auto;
				SetValue = set;
				GetValue = get;
				Available = true;
			}

			public int Min { get; }
			public int Max { get; }
			public int Step { get; }
			public int Default { get; }
			public CameraControlFlags Flags { get; }
			public Action<CameraControlFlags, int> SetValue { get; }
			public Func<int>? GetValue { get; }
			public bool Available { get; }
			public bool CanAuto { get; }

			public override string ToString()
			{
				return
					$"Available={Available}, Min={Min}, Max={Max}, Step={Step}, Default={Default}, Flags={Flags}";
			}
		}
	}

	private class SampleGrabberCallback : ISampleGrabberCB
	{
		private byte[]? _buffer;
		private readonly object _bufferLock = new();

		public int BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
		{
			if (_buffer == null || _buffer.Length != bufferLen)
			{
				_buffer = new byte[bufferLen];
			}

			lock (_bufferLock)
			{
				Marshal.Copy(pBuffer, _buffer, 0, bufferLen);
			}

			return 0;
		}

		public int SampleCB(double sampleTime, IMediaSample pSample)
		{
			return 0;
			//throw new NotImplementedException();
		}

		public Bitmap
			GetBitmap(int width, int height, int stride)
		{
			if (_buffer == null)
			{
				return EmptyBitmap(width, height);
			}

			lock (_bufferLock)
			{
				return BufferToBitmap(_buffer, width, height, stride);
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
		public string? MajorType { get; set; }
		public string? SubType { get; set; }
		public Size Size { get; set; }
		public long TimePerFrame { get; set; }
		public VIDEO_STREAM_CONFIG_CAPS Caps { get; set; }

		public override string ToString()
		{
			return $"{MajorType}, {SubType}, {Size}, {TimePerFrame}, {CapsString()}";
		}

		private string CapsString()
		{
			var sb = new StringBuilder();
			sb.AppendFormat("{0}, ", DsGuid.GetNickname(Caps.Guid));
			foreach (var info in Caps.GetType().GetFields())
			{
				sb.AppendFormat("{0}={1}, ", info.Name, info.GetValue(Caps));
			}

			return sb.ToString();
		}
	}
}
