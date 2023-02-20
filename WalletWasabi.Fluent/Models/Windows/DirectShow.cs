using Avalonia;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace WalletWasabi.Fluent.Models.Windows;

[SupportedOSPlatform("windows")]
public static class DirectShow
{
	public static object? CoCreateInstance(Guid clsid)
	{
		Type? type = Type.GetTypeFromCLSID(clsid);
		if (type is { })
		{
			return Activator.CreateInstance(type);
		}
		return null;
	}

	public static void ReleaseInstance<T>(ref T? com) where T : class
	{
		if (com != null)
		{
			Marshal.ReleaseComObject(com);
			com = null;
		}
	}

	public static IGraphBuilder? CreateGraph()
	{
		return CoCreateInstance(DsGuid.CLSID_FilterGraph) as IGraphBuilder;
	}

	public static void PlayGraph(IGraphBuilder graph, FILTER_STATE state)
	{
		if (graph is not IMediaControl mediaControl)
		{
			return;
		}

		switch (state)
		{
			case FILTER_STATE.Paused:
				mediaControl.Pause();
				break;

			case FILTER_STATE.Stopped:
				mediaControl.Stop();
				break;

			default:
				mediaControl.Run();
				break;
		}
	}

	public static List<string> GetFilters(Guid category)
	{
		var result = new List<string>();

		EnumMonikers(category, (moniker, prop) =>
		{
			object? value = null;
			_ = prop.Read("FriendlyName", ref value, 0);
			if (value is { })
			{
				result.Add((string)value);
			}

			return false;
		});

		return result;
	}

	public static IBaseFilter? CreateFilter(Guid clsid)
	{
		return CoCreateInstance(clsid) as IBaseFilter;
	}

	public static IBaseFilter CreateFilter(Guid category, int index)
	{
		IBaseFilter? result = null;

		int curr_index = 0;
		EnumMonikers(category, (moniker, prop) =>
		{
			if (index != curr_index++)
			{
				return false;
			}

			Guid guid = DsGuid.IID_IBaseFilter;
			moniker.BindToObject(null, null, ref guid, out object value);
			result = value as IBaseFilter;
			return true;
		});

		if (result == null)
		{
			throw new ArgumentException("can't create filter.");
		}
		return result;
	}

	private static void EnumMonikers(Guid category, Func<IMoniker, IPropertyBag, bool> func)
	{
		IEnumMoniker? enumerator = null;
		ICreateDevEnum? device = null;

		try
		{
			Type? type = Type.GetTypeFromCLSID(DsGuid.CLSID_SystemDeviceEnum);
			if (type is null)
			{
				return;
			}
			device = Activator.CreateInstance(type) as ICreateDevEnum;

			device?.CreateClassEnumerator(ref category, ref enumerator, 0);

			if (enumerator == null)
			{
				return;
			}
			var monikers = new IMoniker[1];
			var fetched = IntPtr.Zero;

			while (enumerator.Next(monikers.Length, monikers, fetched) == 0)
			{
				var moniker = monikers[0];

				Guid guid = DsGuid.IID_IPropertyBag;
				moniker.BindToStorage(null, null, ref guid, out object value);
				var prop = (IPropertyBag)value;

				try
				{
					var rc = func(moniker, prop);
					if (rc == true)
					{
						break;
					}
				}
				finally
				{
					Marshal.ReleaseComObject(prop);

					if (moniker != null)
					{
						Marshal.ReleaseComObject(moniker);
					}
				}
			}
		}
		finally
		{
			if (enumerator != null)
			{
				Marshal.ReleaseComObject(enumerator);
			}
			if (device != null)
			{
				Marshal.ReleaseComObject(device);
			}
		}
	}

	public static IPin FindPin(IBaseFilter filter, string name)
	{
		var result = EnumPins(filter, (info) => info.achName == name);

		if (result == null)
		{
			throw new ArgumentException("Can't find pin.");
		}
		return result;
	}

	public static IPin FindPin(IBaseFilter filter, int index, PIN_DIRECTION direction)
	{
		int curr_index = 0;
		var result = EnumPins(filter, (info) =>
		{
			if (info.dir != direction)
			{
				return false;
			}
			return index == curr_index++;
		});

		if (result == null)
		{
			throw new ArgumentException("Can't find pin.");
		}
		return result;
	}

	private static IPin? EnumPins(IBaseFilter filter, Func<PIN_INFO, bool> func)
	{
		IEnumPins? pins = null;
		IPin? ipin = null;

		try
		{
			filter.EnumPins(ref pins);

			int fetched = 0;
			while (pins?.Next(1, ref ipin, ref fetched) == 0)
			{
				if (fetched == 0)
				{
					break;
				}

				var info = new PIN_INFO();
				try
				{
					ipin?.QueryPinInfo(info);
					var rc = func(info);
					if (rc)
					{
						return ipin;
					}
				}
				finally
				{
					if (info.pFilter != null)
					{
						Marshal.ReleaseComObject(info.pFilter);
					}
				}
			}
		}
		catch
		{
			if (ipin != null)
			{
				Marshal.ReleaseComObject(ipin);
			}
			throw;
		}
		finally
		{
			if (pins != null)
			{
				Marshal.ReleaseComObject(pins);
			}
		}

		return null;
	}

	public static void ConnectFilter(IGraphBuilder graph, IBaseFilter out_flt, int out_no, IBaseFilter in_flt,
		int in_no)
	{
		var out_pin = FindPin(out_flt, out_no, PIN_DIRECTION.PINDIR_OUTPUT);
		var inp_pin = FindPin(in_flt, in_no, PIN_DIRECTION.PINDIR_INPUT);
		graph.Connect(out_pin, inp_pin);
	}

	public static void DeleteMediaType(ref AM_MEDIA_TYPE? mt)
	{
		if (mt == null)
		{
			return;
		}

		if (mt.lSampleSize != 0)
		{
			Marshal.FreeCoTaskMem(mt.pbFormat);
		}
		if (mt.pUnk != IntPtr.Zero)
		{
			Marshal.FreeCoTaskMem(mt.pUnk);
		}
		mt = null;
	}

	[ComVisible(true), ComImport(), Guid("56a8689f-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IFilterGraph
	{
		int AddFilter([In] IBaseFilter? pFilter, [In, MarshalAs(UnmanagedType.LPWStr)] string pName);

		int RemoveFilter([In] IBaseFilter pFilter);

		int EnumFilters([In, Out] ref IEnumFilters ppEnum);

		int FindFilterByName([In, MarshalAs(UnmanagedType.LPWStr)] string pName,
			[In, Out] ref IBaseFilter ppFilter);

		int ConnectDirect([In] IPin ppinOut, [In] IPin ppinIn, [In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int Reconnect([In] IPin ppin);

		int Disconnect([In] IPin ppin);

		int SetDefaultSyncSource();
	}

	[ComVisible(true), ComImport(), Guid("56a868a9-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IGraphBuilder : IFilterGraph
	{
		int Connect([In] IPin ppinOut, [In] IPin ppinIn);

		int Render([In] IPin ppinOut);

		int RenderFile([In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFile,
			[In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrPlayList);

		int AddSourceFilter([In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFileName,
			[In, MarshalAs(UnmanagedType.LPWStr)] string lpcwstrFilterName, [In, Out] ref IBaseFilter ppFilter);

		int SetLogFile(IntPtr hFile);

		int Abort();

		int ShouldOperationContinue();
	}

	[ComVisible(true), ComImport(), Guid("56a868b1-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsDual)]
	public interface IMediaControl
	{
		int Run();

		int Pause();

		int Stop();

		int GetState(int msTimeout, out int pfs);

		int RenderFile(string strFilename);

		int AddSourceFilter([In] string strFilename, [In, Out, MarshalAs(UnmanagedType.IDispatch)]
				ref object ppUnk);

		int Get_FilterCollection([In, Out, MarshalAs(UnmanagedType.IDispatch)]
				ref object ppUnk);

		int Get_RegFilterCollection([In, Out, MarshalAs(UnmanagedType.IDispatch)]
				ref object ppUnk);

		int StopWhenReady();
	}

	[ComVisible(true), ComImport(), Guid("93E5A4E0-2D50-11d2-ABFA-00A0C9C6E38D"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ICaptureGraphBuilder2
	{
		int SetFiltergraph([In] IGraphBuilder pfg);

		int GetFiltergraph([In, Out] ref IGraphBuilder ppfg);

		int SetOutputFileName([In] ref Guid pType, [In, MarshalAs(UnmanagedType.LPWStr)] string lpstrFile,
			[In, Out] ref IBaseFilter ppbf, [In, Out] ref IFileSinkFilter ppSink);

		int FindInterface([In] ref Guid pCategory, [In] ref Guid pType, [In] IBaseFilter pbf, [In] IntPtr riid,
			[In, Out, MarshalAs(UnmanagedType.IUnknown)]
				ref object ppint);

		int RenderStream([In] ref Guid pCategory, [In] ref Guid pType, [In, MarshalAs(UnmanagedType.IUnknown)]
				object pSource, [In] IBaseFilter pfCompressor, [In] IBaseFilter? pfRenderer);

		int ControlStream([In] ref Guid pCategory, [In] ref Guid pType, [In] IBaseFilter pFilter,
			[In] IntPtr pstart, [In] IntPtr pstop, [In] short wStartCookie, [In] short wStopCookie);

		int AllocCapFile([In, MarshalAs(UnmanagedType.LPWStr)] string lpstrFile, [In] long dwlSize);

		int CopyCaptureFile([In, MarshalAs(UnmanagedType.LPWStr)] string lpwstrOld,
			[In, MarshalAs(UnmanagedType.LPWStr)] string lpwstrNew, [In] int fAllowEscAbort,
			[In] IAMCopyCaptureFileProgress pFilter);

		int FindPin([In] object pSource, [In] int pindir, [In] ref Guid pCategory, [In] ref Guid pType,
			[In, MarshalAs(UnmanagedType.Bool)] bool fUnconnected, [In] int num, [Out] out IntPtr ppPin);
	}

	[ComVisible(true), ComImport(), Guid("a2104830-7c70-11cf-8bce-00aa00a3f1a6"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IFileSinkFilter
	{
		int SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
			[In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int GetCurFile([In, Out, MarshalAs(UnmanagedType.LPWStr)]
				ref string pszFileName, [Out, MarshalAs(UnmanagedType.LPStruct)]
				out AM_MEDIA_TYPE pmt);
	}

	[ComVisible(true), ComImport(), Guid("670d1d20-a068-11d0-b3f0-00aa003761c5"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAMCopyCaptureFileProgress
	{
		int Progress(int iProgress);
	}

	[ComVisible(true), ComImport(), Guid("C6E13370-30AC-11d0-A18C-00A0C9118956"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAMCameraControl
	{
		int GetRange([In] CameraControlProperty property, [In, Out] ref int pMin, [In, Out] ref int pMax,
			[In, Out] ref int pSteppingDelta, [In, Out] ref int pDefault, [In, Out] ref int pCapsFlag);

		int Set([In] CameraControlProperty property, [In] int lValue, [In] int flags);

		int Get([In] CameraControlProperty property, [In, Out] ref int lValue, [In, Out] ref int flags);
	}

	[ComVisible(true), ComImport(), Guid("C6E13360-30AC-11d0-A18C-00A0C9118956"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAMVideoProcAmp
	{
		int GetRange([In] VideoProcAmpProperty property, [In, Out] ref int pMin, [In, Out] ref int pMax,
			[In, Out] ref int pSteppingDelta, [In, Out] ref int pDefault, [In, Out] ref int pCapsFlag);

		int Set([In] VideoProcAmpProperty property, [In] int lValue, [In] int flags);

		int Get([In] VideoProcAmpProperty property, [In, Out] ref int lValue, [In, Out] ref int flags);
	}

	[ComVisible(true), ComImport(), Guid("6A2E0670-28E4-11D0-A18C-00A0C9118956"),
	 System.Security.SuppressUnmanagedCodeSecurity, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAMVideoControl
	{
		int GetCaps([In] IPin pPin, [Out] out int pCapsFlags);

		int SetMode([In] IPin pPin, [In] int mode);

		int GetMode([In] IPin pPin, [Out] out int mode);

		int GetCurrentActualFrameRate([In] IPin pPin, [Out] out long actualFrameRate);

		int GetMaxAvailableFrameRate([In] IPin pPin, [In] int iIndex, [In] Size dimensions,
			[Out] out long maxAvailableFrameRate);

		int GetFrameRateList([In] IPin pPin, [In] int iIndex, [In] Size dimensions, [Out] out int listSize,
			[Out] out IntPtr frameRates);
	}

	[ComVisible(true), ComImport(), Guid("56a86895-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IBaseFilter
	{
		int GetClassID([Out] out Guid pClassID);

		int Stop();

		int Pause();

		int Run(long tStart);

		int GetState(int dwMilliSecsTimeout, [In, Out] ref int filtState);

		int SetSyncSource([In] IReferenceClock pClock);

		int GetSyncSource([In, Out] ref IReferenceClock pClock);

		int EnumPins([In, Out] ref IEnumPins? ppEnum);

		int FindPin([In, MarshalAs(UnmanagedType.LPWStr)] string id, [In, Out] ref IPin ppPin);

		int QueryFilterInfo([Out] FILTER_INFO pInfo);

		int JoinFilterGraph([In] IFilterGraph pGraph, [In, MarshalAs(UnmanagedType.LPWStr)] string pName);

		int QueryVendorInfo([In, Out, MarshalAs(UnmanagedType.LPWStr)]
				ref string pVendorInfo);
	}

	[ComVisible(true), ComImport(), Guid("56a86893-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IEnumFilters
	{
		int Next([In] int cFilters, [In, Out] ref IBaseFilter ppFilter, [In, Out] ref int pcFetched);

		int Skip([In] int cFilters);

		void Reset();

		void Clone([In, Out] ref IEnumFilters ppEnum);
	}

	[ComVisible(true), ComImport(), Guid("C6E13340-30AC-11d0-A18C-00A0C9118956"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IAMStreamConfig
	{
		int SetFormat([In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE? pmt);

		int GetFormat([In, Out, MarshalAs(UnmanagedType.LPStruct)]
				ref AM_MEDIA_TYPE ppmt);

		int GetNumberOfCapabilities(ref int piCount, ref int piSize);

		int GetStreamCaps(int iIndex, [In, Out, MarshalAs(UnmanagedType.LPStruct)]
				ref AM_MEDIA_TYPE? ppmt, IntPtr pSCC);
	}

	[ComVisible(true), ComImport(), Guid("56a8689a-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IMediaSample
	{
		int GetPointer(ref IntPtr ppBuffer);

		int GetSize();

		int GetTime(ref long pTimeStart, ref long pTimeEnd);

		int SetTime([In, MarshalAs(UnmanagedType.LPStruct)]
				ulong pTimeStart, [In, MarshalAs(UnmanagedType.LPStruct)]
				ulong pTimeEnd);

		int IsSyncPoint();

		int SetSyncPoint([In, MarshalAs(UnmanagedType.Bool)] bool bIsSyncPoint);

		int IsPreroll();

		int SetPreroll([In, MarshalAs(UnmanagedType.Bool)] bool bIsPreroll);

		int GetActualDataLength();

		int SetActualDataLength(int len);

		int GetMediaType([In, Out, MarshalAs(UnmanagedType.LPStruct)]
				ref AM_MEDIA_TYPE ppMediaType);

		int SetMediaType([In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pMediaType);

		int IsDiscontinuity();

		int SetDiscontinuity([In, MarshalAs(UnmanagedType.Bool)] bool bDiscontinuity);

		int GetMediaTime(ref long pTimeStart, ref long pTimeEnd);

		int SetMediaTime([In, MarshalAs(UnmanagedType.LPStruct)]
				ulong pTimeStart, [In, MarshalAs(UnmanagedType.LPStruct)]
				ulong pTimeEnd);
	}

	[ComVisible(true), ComImport(), Guid("89c31040-846b-11ce-97d3-00aa0055595a"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IEnumMediaTypes
	{
		int Next([In] int cMediaTypes, [In, Out, MarshalAs(UnmanagedType.LPStruct)]
				ref AM_MEDIA_TYPE ppMediaTypes, [In, Out] ref int pcFetched);

		int Skip([In] int cMediaTypes);

		int Reset();

		int Clone([In, Out] ref IEnumMediaTypes ppEnum);
	}

	[ComVisible(true), ComImport(), Guid("56a86891-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IPin
	{
		int Connect([In] IPin pReceivePin, [In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int ReceiveConnection([In] IPin pReceivePin, [In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int Disconnect();

		int ConnectedTo([In, Out] ref IPin ppPin);

		int ConnectionMediaType([Out, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int QueryPinInfo([Out] PIN_INFO pInfo);

		int QueryDirection(ref PIN_DIRECTION pPinDir);

		int QueryId([In, Out, MarshalAs(UnmanagedType.LPWStr)]
				ref string id);

		int QueryAccept([In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int EnumMediaTypes([In, Out] ref IEnumMediaTypes ppEnum);

		int QueryInternalConnections(IntPtr apPin, [In, Out] ref int nPin);

		int EndOfStream();

		int BeginFlush();

		int EndFlush();

		int NewSegment(long tStart, long tStop, double dRate);
	}

	[ComVisible(true), ComImport(), Guid("56a86892-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IEnumPins
	{
		int Next([In] int cPins, [In, Out] ref IPin? ppPins, [In, Out] ref int pcFetched);

		int Skip([In] int cPins);

		void Reset();

		void Clone([In, Out] ref IEnumPins ppEnum);
	}

	[ComVisible(true), ComImport(), Guid("56a86897-0ad4-11ce-b03a-0020af0ba770"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IReferenceClock
	{
		int GetTime(ref long pTime);

		int AdviseTime(long baseTime, long streamTime, IntPtr hEvent, ref int pdwAdviseCookie);

		int AdvisePeriodic(long startTime, long periodTime, IntPtr hSemaphore, ref int pdwAdviseCookie);

		int Unadvise(int dwAdviseCookie);
	}

	[ComVisible(true), ComImport(), Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ICreateDevEnum
	{
		int CreateClassEnumerator([In] ref Guid pType,
			[In, Out] ref IEnumMoniker? ppEnumMoniker, [In] int dwFlags);
	}

	[ComVisible(true), ComImport(), Guid("55272A00-42CB-11CE-8135-00AA004BB851"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface IPropertyBag
	{
		int Read([MarshalAs(UnmanagedType.LPWStr)] string propName, ref object? var, int errorLog);

		int Write(string propName, ref object var);
	}

	[ComVisible(true), ComImport(), Guid("6B652FFF-11FE-4fce-92AD-0266B5D7C78F"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ISampleGrabber
	{
		int SetOneShot([In, MarshalAs(UnmanagedType.Bool)] bool oneShot);

		int SetMediaType([In, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int GetConnectedMediaType([Out, MarshalAs(UnmanagedType.LPStruct)]
				AM_MEDIA_TYPE pmt);

		int SetBufferSamples([In, MarshalAs(UnmanagedType.Bool)] bool bufferThem);

		int GetCurrentBuffer(ref int pBufferSize, IntPtr pBuffer);

		int GetCurrentSample(IntPtr ppSample);

		int SetCallback(ISampleGrabberCB pCallback, int whichMethodToCallback);
	}

	[ComVisible(true), ComImport(), Guid("0579154A-2B53-4994-B0D0-E773148EFF85"),
	 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	public interface ISampleGrabberCB
	{
		[PreserveSig()]
		int SampleCB(double sampleTime, IMediaSample pSample);

		[PreserveSig()]
		int BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen);
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential), ComVisible(false)]
	public class AM_MEDIA_TYPE
	{
		public Guid MajorType;
		public Guid SubType;
		[MarshalAs(UnmanagedType.Bool)] public bool bFixedSizeSamples;
		[MarshalAs(UnmanagedType.Bool)] public bool bTemporalCompression;
		public uint lSampleSize;
		public Guid FormatType;
		public IntPtr pUnk;
		public uint cbFormat;
		public IntPtr pbFormat;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode), ComVisible(false)]
	public class FILTER_INFO
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string? achName;

		[MarshalAs(UnmanagedType.IUnknown)] public object? pGraph;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode), ComVisible(false)]
	public class PIN_INFO
	{
		public IBaseFilter? pFilter;
		public PIN_DIRECTION dir;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string? achName;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 8), ComVisible(false)]
	public struct VIDEO_STREAM_CONFIG_CAPS
	{
		public Guid Guid;
		public uint VideoStandard;
		public SIZE InputSize;
		public SIZE MinCroppingSize;
		public SIZE MaxCroppingSize;
		public int CropGranularityX;
		public int CropGranularityY;
		public int CropAlignX;
		public int CropAlignY;
		public SIZE MinOutputSize;
		public SIZE MaxOutputSize;
		public int OutputGranularityX;
		public int OutputGranularityY;
		public int StretchTapsX;
		public int StretchTapsY;
		public int ShrinkTapsX;
		public int ShrinkTapsY;
		public long MinFrameInterval;
		public long MaxFrameInterval;
		public int MinBitsPerSecond;
		public int MaxBitsPerSecond;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential), ComVisible(false)]
	public struct VIDEOINFOHEADER
	{
		public RECT SrcRect;
		public RECT TrgRect;
		public int BitRate;
		public int BitErrorRate;
		public long AvgTimePerFrame;
		public BITMAPINFOHEADER bmiHeader;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential), ComVisible(false)]
	public struct VIDEOINFOHEADER2
	{
		public RECT SrcRect;
		public RECT TrgRect;
		public int BitRate;
		public int BitErrorRate;
		public long AvgTimePerFrame;
		public int InterlaceFlags;
		public int CopyProtectFlags;
		public int PictAspectRatioX;
		public int PictAspectRatioY;
		public int ControlFlags; // or Reserved1
		public int Reserved2;
		public BITMAPINFOHEADER bmiHeader;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 2), ComVisible(false)]
	public struct BITMAPINFOHEADER
	{
		public int biSize;
		public int biWidth;
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential), ComVisible(false)]
	public struct WAVEFORMATEX
	{
		public ushort wFormatTag;
		public ushort nChannels;
		public uint nSamplesPerSec;
		public uint nAvgBytesPerSec;
		public short nBlockAlign;
		public short wBitsPerSample;
		public short cbSize;
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 8), ComVisible(false)]
	public struct SIZE
	{
		public int cx;
		public int cy;

		public override string ToString()
		{
			return $"{{{cx}, {cy}}}";
		} // for debugging.
	}

	[Serializable]
	[StructLayout(LayoutKind.Sequential), ComVisible(false)]
	public struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;

		public override string ToString()
		{
			return $"{{{Left}, {Top}, {Right}, {Bottom}}}";
		} // for debugging.
	}

	[ComVisible(false)]
	public enum PIN_DIRECTION
	{
		PINDIR_INPUT = 0,
		PINDIR_OUTPUT = 1,
	}

	[ComVisible(false)]
	public enum FILTER_STATE : int
	{
		Stopped = 0,
		Paused = 1,
		Running = 2,
	}

	[ComVisible(false)]
	public enum CameraControlProperty
	{
		Pan = 0,
		Tilt = 1,
		Roll = 2,
		Zoom = 3,
		Exposure = 4,
		Iris = 5,
		Focus = 6,
	}

	[ComVisible(false), Flags()]
	internal enum CameraControlFlags
	{
		Auto = 0x0001,
		Manual = 0x0002,
	}

	[ComVisible(false)]
	public enum VideoProcAmpProperty
	{
		Brightness = 0,
		Contrast = 1,
		Hue = 2,
		Saturation = 3,
		Sharpness = 4,
		Gamma = 5,
		ColorEnable = 6,
		WhiteBalance = 7,
		BacklightCompensation = 8,
		Gain = 9
	}

	public static class DsGuid
	{
		public static readonly Guid MEDIATYPE_Video = new("{73646976-0000-0010-8000-00AA00389B71}");

		public static readonly Guid MEDIATYPE_Audio = new("{73647561-0000-0010-8000-00AA00389B71}");

		public static readonly Guid MEDIASUBTYPE_None = new("{E436EB8E-524F-11CE-9F53-0020AF0BA770}");

		public static readonly Guid MEDIASUBTYPE_YUYV = new("{56595559-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_IYUV = new("{56555949-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_YVU9 = new("{39555659-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_YUY2 = new("{32595559-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_YVYU = new("{55595659-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_UYVY = new("{59565955-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_MJPG = new("{47504A4D-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_RGB565 = new("{E436EB7B-524F-11CE-9F53-0020AF0BA770}");
		public static readonly Guid MEDIASUBTYPE_RGB555 = new("{E436EB7C-524F-11CE-9F53-0020AF0BA770}");
		public static readonly Guid MEDIASUBTYPE_RGB24 = new("{E436EB7D-524F-11CE-9F53-0020AF0BA770}");
		public static readonly Guid MEDIASUBTYPE_RGB32 = new("{E436EB7E-524F-11CE-9F53-0020AF0BA770}");
		public static readonly Guid MEDIASUBTYPE_ARGB32 = new("{773C9AC0-3274-11D0-B724-00AA006C1A01}");
		public static readonly Guid MEDIASUBTYPE_PCM = new("{00000001-0000-0010-8000-00AA00389B71}");
		public static readonly Guid MEDIASUBTYPE_WAVE = new("{E436EB8B-524F-11CE-9F53-0020AF0BA770}");

		public static readonly Guid FORMAT_None = new("{0F6417D6-C318-11D0-A43F-00A0C9223196}");

		public static readonly Guid FORMAT_VideoInfo = new("{05589F80-C356-11CE-BF01-00AA0055595A}");
		public static readonly Guid FORMAT_VideoInfo2 = new("{F72A76A0-EB0A-11d0-ACE4-0000C0CC16BA}");
		public static readonly Guid FORMAT_WaveFormatEx = new("{05589F81-C356-11CE-BF01-00AA0055595A}");

		public static readonly Guid CLSID_AudioInputDeviceCategory = new("{33D9A762-90C8-11d0-BD43-00A0C911CE86}");

		public static readonly Guid CLSID_AudioRendererCategory = new("{E0F158E1-CB04-11d0-BD4E-00A0C911CE86}");

		public static readonly Guid CLSID_VideoInputDeviceCategory = new("{860BB310-5D01-11d0-BD3B-00A0C911CE86}");

		public static readonly Guid CLSID_VideoCompressorCategory = new("{33D9A760-90C8-11d0-BD43-00A0C911CE86}");

		public static readonly Guid CLSID_NullRenderer = new("{C1F400A4-3F08-11D3-9F0B-006008039E37}");
		public static readonly Guid CLSID_SampleGrabber = new("{C1F400A0-3F08-11D3-9F0B-006008039E37}");

		public static readonly Guid CLSID_FilterGraph = new("{E436EBB3-524F-11CE-9F53-0020AF0BA770}");
		public static readonly Guid CLSID_SystemDeviceEnum = new("{62BE5D10-60EB-11d0-BD3B-00A0C911CE86}");
		public static readonly Guid CLSID_CaptureGraphBuilder2 = new("{BF87B6E1-8C27-11d0-B3F0-00AA003761C5}");

		public static readonly Guid IID_IPropertyBag = new("{55272A00-42CB-11CE-8135-00AA004BB851}");
		public static readonly Guid IID_IBaseFilter = new("{56a86895-0ad4-11ce-b03a-0020af0ba770}");
		public static readonly Guid IID_IAMStreamConfig = new("{C6E13340-30AC-11d0-A18C-00A0C9118956}");

		public static readonly Guid PIN_CATEGORY_CAPTURE = new("{fb6c4281-0353-11d1-905f-0000c0cc16ba}");
		public static readonly Guid PIN_CATEGORY_PREVIEW = new("{fb6c4282-0353-11d1-905f-0000c0cc16ba}");
		public static readonly Guid PIN_CATEGORY_STILL = new("{fb6c428a-0353-11d1-905f-0000c0cc16ba}");

		private static Dictionary<Guid, string>? NicknameCache = null;

		public static string GetNickname(Guid guid)
		{
			NicknameCache ??= typeof(DsGuid)
					.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
					.Where(x => x.FieldType == typeof(Guid))
					.ToDictionary(x => (Guid)x.GetValue(null)!, x => x.Name);

			if (NicknameCache.TryGetValue(guid, out string? value))
			{
				var name = value;
				var elem = name.Split('_');

				if (elem.Length >= 2)
				{
					var text = string.Join("_", elem.Skip(1).ToArray());
					return $"[{text}]";
				}
				else
				{
					return name;
				}
			}

			return guid.ToString();
		}
	}
}
