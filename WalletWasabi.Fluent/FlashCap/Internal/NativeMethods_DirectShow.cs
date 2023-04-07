////////////////////////////////////////////////////////////////////////////
//
// FlashCap - Independent camera capture library.
// Copyright (c) Kouji Matsui (@kozy_kekyo, @kekyo@mastodon.cloud)
//
// Licensed under Apache-v2: https://opensource.org/licenses/Apache-2.0
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using FlashCap.Utilities;

namespace FlashCap.Internal;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods_DirectShow
{
    [SuppressUnmanagedCodeSecurity]
    [Guid("0000010c-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersist
    {
        [PreserveSig] int GetClassID(out Guid classID);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("00000109-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistStream : IPersist
    {
        [PreserveSig] new int GetClassID(out Guid classID);

        [PreserveSig] int IsDirty();
        [PreserveSig] int Load(System.Runtime.InteropServices.ComTypes.IStream stm);
        [PreserveSig] int Save(System.Runtime.InteropServices.ComTypes.IStream stm, bool clearDirty);
        [PreserveSig] int GetSizeMax(out long size);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("0000000f-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMoniker : IPersistStream
    {
        [PreserveSig] new int GetClassID(out Guid classID);
        [PreserveSig] new int IsDirty();
        [PreserveSig] new int Load(System.Runtime.InteropServices.ComTypes.IStream stm);
        [PreserveSig] new int Save(System.Runtime.InteropServices.ComTypes.IStream stm, bool clearDirty);
        [PreserveSig] new int GetSizeMax(out long size);

        [PreserveSig] int BindToObject(
            System.Runtime.InteropServices.ComTypes.IBindCtx? bindContext,
            IMoniker? makeToLeft,
            in Guid riidResult,
            [MarshalAs(UnmanagedType.Interface)] out object? result);
        [PreserveSig] int BindToStorage(
            System.Runtime.InteropServices.ComTypes.IBindCtx? bindContext,
            IMoniker? makeToLeft,
            in Guid riidResult,
            [MarshalAs(UnmanagedType.Interface)] out object? result);

        // truncated.
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("00000102-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumMoniker
    {
        [PreserveSig] int Next(
            int request,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IMoniker?[] monikers,
            out int fetched);
        [PreserveSig] int Skip(int count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumMoniker? enumMoniker);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICreateDevEnum
    {
        [PreserveSig] int CreateClassEnumerator(
            in Guid type,
            out IEnumMoniker? enumMoniker,
            uint flags);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a86897-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IReferenceClock
    {
        [PreserveSig] int GetTime(out long time);
        [PreserveSig] int AdviseTime(
            long baseTime,
            long streamTime,
            IntPtr hEvent,
            out IntPtr adviseCookie);
        [PreserveSig] int AdvisePeriodic(
            long startTime,
            long periodTime,
            IntPtr hSemaphore,
            out IntPtr adviseCookie);
        [PreserveSig] int Unadvise(IntPtr adviseCookie);
    }

    public enum FILTER_STATE
    {
        Stopped,
        Paused,
        Running,
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a86899-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMediaFilter : IPersist
    {
        [PreserveSig] new void GetClassID(out Guid classID);

        [PreserveSig] int Stop();
        [PreserveSig] int Pause();
        [PreserveSig] int Run(long tStart);
        [PreserveSig] int GetState(uint milliSecsTimeout, out FILTER_STATE state);
        [PreserveSig] int SetSyncSource(IReferenceClock clock);
        [PreserveSig] int GetSyncSource(out IReferenceClock? clock);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AM_MEDIA_TYPE
    {
        public Guid majortype;   // MEDIATYPE_*
        public Guid subtype;     // MEDIASUBTYPE_*
        public int fixedSizeSamples;     //Made blittable: [MarshalAs(UnmanagedType.Bool)] public bool fixedSizeSamples;
        public int temporalCompression;  //Made blittable: [MarshalAs(UnmanagedType.Bool)] public bool temporalCompression;
        public int sampleSize;
        public Guid formattype;  // FORMATTYPE_*
        public IntPtr pUnk;
        public int formatSize;
        public IntPtr pFormat;   // VIDEOINFOHEADER / VIDEOINFOHEADER2

        public void Release()
        {
            if (this.pUnk != IntPtr.Zero)
            {
                Marshal.Release(this.pUnk);
                this.pUnk = IntPtr.Zero;
            }
            if (this.pFormat != IntPtr.Zero)
            {
                NativeMethods.FreeMemory(this.pFormat);
                this.pFormat = IntPtr.Zero;
            }
        }

        public unsafe IntPtr AllocateAndGetBih()
        {
            if (this.formattype == FORMAT_VideoInfo)
            {
                var pVih = (NativeMethods.VIDEOINFOHEADER*)this.pFormat.ToPointer();
                var pBih = (NativeMethods.BITMAPINFOHEADER*)(pVih + 1);
                var pBihCopied = NativeMethods.AllocateMemory((IntPtr)pBih->biSize);
                NativeMethods.CopyMemory(pBihCopied, (IntPtr)pBih, (IntPtr)pBih->biSize);
                return pBihCopied;
            }
            else if (this.formattype == FORMAT_VideoInfo2)
            {
                var pVih = (NativeMethods.VIDEOINFOHEADER2*)this.pFormat.ToPointer();
                var pBih = (NativeMethods.BITMAPINFOHEADER*)(pVih + 1);
                var pBihCopied = NativeMethods.AllocateMemory((IntPtr)pBih->biSize);
                NativeMethods.CopyMemory(pBihCopied, (IntPtr)pBih, (IntPtr)pBih->biSize);
                return pBihCopied;
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }

    public enum PIN_DIRECTION
    {
        Input,
        Output,
    }

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct PIN_INFO
    {
        public IBaseFilter filter;
        public PIN_DIRECTION dir;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string name;
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("89c31040-846b-11ce-97d3-00aa0055595a")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumMediaTypes
    {
        [PreserveSig] int Next(
            int request,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] AM_MEDIA_TYPE[] mediaTypes,
            out int fetched);
        [PreserveSig] int Skip(int count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumMediaTypes? enumMediaTypes);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a86891-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPin
    {
        [PreserveSig] int Connect(
            IPin receivePin,
            in AM_MEDIA_TYPE mt);
        [PreserveSig] int ReceiveConnection(
            IPin pReceivePin,
            in AM_MEDIA_TYPE mt);
        [PreserveSig] int Disconnect();
        [PreserveSig] int ConnectedTo(out IPin? pin);
        [PreserveSig] int ConnectionMediaType(out AM_MEDIA_TYPE mt);
        [PreserveSig] int QueryPinInfo(out PIN_INFO info);
        [PreserveSig] int QueryDirection(out PIN_DIRECTION pinDir);
        [PreserveSig] int QueryId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int QueryAccept(in AM_MEDIA_TYPE mt);
        [PreserveSig] int EnumMediaTypes(out IEnumMediaTypes? enumMediaTypes);
        [PreserveSig] int QueryInternalConnections(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=1)] out IPin?[] pins,
            ref int pin);
        [PreserveSig] int EndOfStream();
        [PreserveSig] int BeginFlush();
        [PreserveSig] int EndFlush();
        [PreserveSig] int NewSegment(
            long tStart,
            long tStop,
            double rate);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a86892-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumPins
    {
        [PreserveSig] int Next(
            int request,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IPin?[] pins,
            out int fetched);
        [PreserveSig] int Skip(int count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumPins? enumPins);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a86893-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumFilters
    {
        [PreserveSig] int Next(
            int request,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IPin?[] filters,
            out int fetched);
        [PreserveSig] int Skip(int count);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumPins? enumPins);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a8689f-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFilterGraph
    {
        [PreserveSig] int AddFilter(
            IBaseFilter filter,
            [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int RemoveFilter(IBaseFilter filter);
        [PreserveSig] int EnumFilters(out IEnumFilters? ppEnum);
        [PreserveSig] int FindFilterByName(
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            out IBaseFilter? filter);
        [PreserveSig] int ConnectDirect(
            IPin pinOut, IPin pinIn, in AM_MEDIA_TYPE mt);
        [PreserveSig] int Reconnect(IPin pin);
        [PreserveSig] int Disconnect(IPin pin);
        [PreserveSig] int SetDefaultSyncSource();
    }

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct FILTER_INFO
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=128)] public string chName;
        public IFilterGraph graph;
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a86895-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBaseFilter : IMediaFilter
    {
        [PreserveSig] new int GetClassID(out Guid classID);
        [PreserveSig] new int Stop();
        [PreserveSig] new int Pause();
        [PreserveSig] new int Run(long tStart);
        [PreserveSig] new int GetState(uint milliSecsTimeout, out FILTER_STATE state);
        [PreserveSig] new int SetSyncSource(IReferenceClock clock);
        [PreserveSig] new int GetSyncSource(out IReferenceClock? clock);

        [PreserveSig] int EnumPins(out IEnumPins? enumPins);
        [PreserveSig] int FindPin(
            [MarshalAs(UnmanagedType.LPWStr)] string id,
            out IPin? pin);
        [PreserveSig] int QueryFilterInfo(out FILTER_INFO info);
        [PreserveSig] int JoinFilterGraph(
            IFilterGraph graph,
            [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int QueryVendorInfo(
            [MarshalAs(UnmanagedType.LPWStr)] out string vendorInfo);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("3127CA40-446E-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IErrorLog
    {
        [PreserveSig] int AddError(
            [MarshalAs(UnmanagedType.LPWStr)] string propName,
            in System.Runtime.InteropServices.ComTypes.EXCEPINFO excepInfo);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyBag
    {
        [PreserveSig] int Read(
            [MarshalAs(UnmanagedType.LPWStr)] string propName,
            out object value,
            IErrorLog? errorLog);
        [PreserveSig] int Write(
            [MarshalAs(UnmanagedType.LPWStr)] string propName,
            in object value);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VIDEO_STREAM_CONFIG_CAPS
    {
        public Guid guid;
        public uint VideoStandard;
        [Obsolete] public NativeMethods.SIZE InputSize;
        [Obsolete] public NativeMethods.SIZE MinCroppingSize;
        [Obsolete] public NativeMethods.SIZE MaxCroppingSize;
        [Obsolete] public int CropGranularityX;
        [Obsolete] public int CropGranularityY;
        [Obsolete] public int CropAlignX;
        [Obsolete] public int CropAlignY;
        [Obsolete] public NativeMethods.SIZE MinOutputSize;
        [Obsolete] public NativeMethods.SIZE MaxOutputSize;
        [Obsolete] public int OutputGranularityX;
        [Obsolete] public int OutputGranularityY;
        [Obsolete] public int StretchTapsX;
        [Obsolete] public int StretchTapsY;
        [Obsolete] public int ShrinkTapsX;
        [Obsolete] public int ShrinkTapsY;
        public long MinFrameInterval;
        public long MaxFrameInterval;
        [Obsolete] public int MinBitsPerSecond;
        [Obsolete] public int MaxBitsPerSecond;
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("C6E13340-30AC-11d0-A18C-00A0C9118956")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAMStreamConfig
    {
        [PreserveSig] int SetFormat(in AM_MEDIA_TYPE mt);
        [PreserveSig] int GetFormat(out AM_MEDIA_TYPE mt);
        [PreserveSig] int GetNumberOfCapabilities(out int count, out int size);
        [PreserveSig] int GetStreamCaps(
            int index, out IntPtr pMediaType, out VIDEO_STREAM_CONFIG_CAPS scc);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a868a9-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphBuilder : IFilterGraph
    {
        [PreserveSig] new int AddFilter(
            IBaseFilter filter,
            [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] new int RemoveFilter(IBaseFilter filter);
        [PreserveSig] new int EnumFilters(out IEnumFilters? ppEnum);
        [PreserveSig] new int FindFilterByName(
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            out IBaseFilter? filter);
        [PreserveSig] new int ConnectDirect(
            IPin pinOut, IPin pinIn, in AM_MEDIA_TYPE mt);
        [PreserveSig] new int Reconnect(IPin pin);
        [PreserveSig] new int Disconnect(IPin pin);
        [PreserveSig] new int SetDefaultSyncSource();

        [PreserveSig] int Connect(
            IPin pinOut, IPin pinIn);
        [PreserveSig] int Render(
            IPin pinOut);
        [PreserveSig] int RenderFile(
            [MarshalAs(UnmanagedType.LPWStr)] string strFile,
            [MarshalAs(UnmanagedType.LPWStr)] string strPlayList);
        [PreserveSig] int AddSourceFilter(
            [MarshalAs(UnmanagedType.LPWStr)] string strFileName,
            [MarshalAs(UnmanagedType.LPWStr)] string strFilterName,
            out IBaseFilter? filter);
        [PreserveSig] int SetLogFile(
            IntPtr hFile);
        [PreserveSig] int Abort();
        [PreserveSig] int ShouldOperationContinue();
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a8689a-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMediaSample
    {
        [PreserveSig] int GetPointer(ref IntPtr ppBuffer);
        [PreserveSig] int GetSize();
        [PreserveSig] int GetTime(
            out long timeStart, out long timeEnd);
        [PreserveSig] int SetTime(
            in long timeStart, in long timeEnd);
        [PreserveSig] int IsSyncPoint();
        [PreserveSig] int SetSyncPoint([MarshalAs(UnmanagedType.Bool)] bool isSyncPoint);
        [PreserveSig] int IsPreroll();
        [PreserveSig] int SetPreroll([MarshalAs(UnmanagedType.Bool)] bool isPreroll);
        [PreserveSig] int GetActualDataLength();
        [PreserveSig] int SetActualDataLength(int length);
        [PreserveSig] int GetMediaType(out IntPtr pMediaType);  // AM_MEDIA_TYPE**
        [PreserveSig] int SetMediaType(in AM_MEDIA_TYPE mediaType);
        [PreserveSig] int IsDiscontinuity();
        [PreserveSig] int SetDiscontinuity([MarshalAs(UnmanagedType.Bool)] bool discontinuity);
        [PreserveSig] int GetMediaTime(out long pTimeStart, out long pTimeEnd);
        [PreserveSig] int SetMediaTime(in long timeStart, in long timeEnd);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("0579154a-2b53-4994-b0d0-e773148eff85")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISampleGrabberCB
    {
        [PreserveSig] int SampleCB(
            double sampleTime,
            IMediaSample sample);
        [PreserveSig] int BufferCB(
            double sampleTime,
            IntPtr pBuffer,
            int bufferLen);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("6b652fff-11fe-4fce-92ad-0266b5d7c78f")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISampleGrabber :
        IBaseFilter  // ISampleGrabber isn't derived from IBaseFilter, but CLSID_SampleGrabber is implemented it.
    {
        [PreserveSig] int SetOneShot(
            [MarshalAs(UnmanagedType.Bool)] bool oneShot);
        [PreserveSig] int SetMediaType(
            in AM_MEDIA_TYPE type);
        [PreserveSig] int GetConnectedMediaType(
            out AM_MEDIA_TYPE type);
        [PreserveSig] int SetBufferSamples(
            [MarshalAs(UnmanagedType.Bool)] bool bufferThem);
        [PreserveSig] int GetCurrentBuffer(
            ref int bufferSize,
            IntPtr pBuffer);
        [PreserveSig, Obsolete] int GetCurrentSample(
            out IMediaSample? sample);
        [PreserveSig] int SetCallback(
            ISampleGrabberCB callback,
            int whichMethodToCallback);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("93E5A4E0-2D50-11d2-ABFA-00A0C9C6E38D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICaptureGraphBuilder2
    {
        [PreserveSig] int SetFiltergraph(
            IGraphBuilder gb);

        [PreserveSig] int GetFiltergraph(
            out IGraphBuilder? gb);

        [PreserveSig] int SetOutputFileName(
            in Guid type,
            [MarshalAs(UnmanagedType.LPWStr)] string strFile,
            out IBaseFilter? filter,
            [MarshalAs(UnmanagedType.Interface)] out object? sink);   // IFileSinkFilter

        [PreserveSig] int FindInterface(
            in Guid category,
            in Guid type,
            IBaseFilter pf,
            in Guid riidResult,
            [MarshalAs(UnmanagedType.Interface)] out object? intf);

        [PreserveSig] int RenderStream(
            in Guid category,
            in Guid type,
            [MarshalAs(UnmanagedType.Interface)] object source,
            IBaseFilter? compressor,
            IBaseFilter? renderer);

        [PreserveSig] int ControlStream(
            in Guid category,
            in Guid type,
            IBaseFilter? filter,
            in long start,
            in long stop,
            short startCookie,
            short stopCookie);

        [PreserveSig] int AllocCapFile(
            [MarshalAs(UnmanagedType.LPWStr)] string str,
            long size);
    
        [PreserveSig] int CopyCaptureFile(
            [MarshalAs(UnmanagedType.LPWStr)] string strOld,
            [MarshalAs(UnmanagedType.LPWStr)] string strNew,
            int fAllowEscAbort,
            [MarshalAs(UnmanagedType.Interface)] object? callback);   // IAMCopyCaptureFileProgress

        [PreserveSig] int FindPin(
            [MarshalAs(UnmanagedType.IUnknown)] object source,
            PIN_DIRECTION pindir,
            in Guid category,
            in Guid type,
            [MarshalAs(UnmanagedType.Bool)] bool unconnected,
            int num,
            out IPin? pin);
    }

    [SuppressUnmanagedCodeSecurity]
    [Guid("56a868b1-0ad4-11ce-b03a-0020af0ba770")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMediaControl
    {
        // These entries are IDispatch members.
        // Couldn't use InterfaceIsIDispatch or InterfaceIsDual, because there're obsoleted.
        [PreserveSig] int vptr_IDispatch_GetTypeInfoCount();
        [PreserveSig] int vptr_IDispatch_GetTypeInfo();
        [PreserveSig] int vptr_IDispatch_GetIDsOfNames();
        [PreserveSig] int vptr_IDispatch_Invoke();

        [PreserveSig] int Run();
        [PreserveSig] int Pause();
        [PreserveSig] int Stop();
        [PreserveSig] int GetState(
            int msTimeout, out FILTER_STATE state);
        [PreserveSig] int RenderFile(
            [MarshalAs(UnmanagedType.BStr)] string strFilename);
        [PreserveSig] int AddSourceFilter(
            [MarshalAs(UnmanagedType.BStr)] string strFilename,
            [MarshalAs(UnmanagedType.IUnknown)] out object? unk);
        [PreserveSig] int get_FilterCollection(
            [MarshalAs(UnmanagedType.IUnknown)] out object? unk);
        [PreserveSig] int get_RegFilterCollection(
            [MarshalAs(UnmanagedType.IUnknown)] out object? unk);
        [PreserveSig] int StopWhenReady();
    }

    ////////////////////////////////////////////////////////////////////////

    public static readonly Guid CLSID_SystemDeviceEnum =
        new Guid("62BE5D10-60EB-11d0-BD3B-00A0C911CE86");
    public static readonly Guid CLSID_VideoInputDeviceCategory =
        new Guid("860BB310-5D01-11d0-BD3B-00A0C911CE86");
    public static readonly Guid CLSID_GraphBuilder =
        new Guid("E436EBB3-524F-11CE-9F53-0020AF0BA770");
    public static readonly Guid CLSID_SampleGrabber =
        new Guid("C1F400A0-3F08-11D3-9F0B-006008039E37");
    public static readonly Guid CLSID_NullRenderer =
        new Guid("C1F400A4-3F08-11D3-9F0B-006008039E37");
    public static readonly Guid CLSID_CaptureGraphBuilder2 =
        new Guid("BF87B6E1-8C27-11d0-B3F0-00AA003761C5");

    public static readonly Guid IID_ICreateDevEnum =
        new Guid("29840822-5B84-11D0-BD3B-00A0C911CE86");
    public static readonly Guid IID_IBaseFilter =
        new Guid("56a86895-0ad4-11ce-b03a-0020af0ba770");
    public static readonly Guid IID_IPropertyBag =
        new Guid("55272A00-42CB-11CE-8135-00AA004BB851");
    public static readonly Guid IID_IGraphBuilder =
        new Guid("56a868a9-0ad4-11ce-b03a-0020af0ba770");
    public static readonly Guid IID_ISampleGrabber =
        new Guid("6B652FFF-11FE-4fce-92AD-0266B5D7C78F");
    public static readonly Guid IID_ICaptureGraphBuilder2 =
        new Guid("93E5A4E0-2D50-11d2-ABFA-00A0C9C6E38D");

    public static readonly Guid FORMAT_VideoInfo =
        new Guid("05589F80-C356-11CE-BF01-00AA0055595A");
    public static readonly Guid FORMAT_VideoInfo2 =
        new Guid("F72A76A0-EB0A-11d0-ACE4-0000C0CC16BA");

    public static readonly Guid MEDIATYPE_Video =
        new Guid("73646976-0000-0010-8000-00AA00389B71");

    public static readonly Guid PIN_CATEGORY_CAPTURE =
        new Guid("fb6c4281-0353-11d1-905f-0000c0cc16ba");

    ////////////////////////////////////////////////////////////////////////

    [Flags]
    public enum CLSCTX : uint
    {
        CLSCTX_INPROC_SERVER = 0x1,
        CLSCTX_INPROC_HANDLER = 0x2,
        CLSCTX_LOCAL_SERVER = 0x4,
    }

    [DllImport("ole32")]
    public static extern int CoCreateInstance(
        in Guid classId,
        [MarshalAs(UnmanagedType.IUnknown)] object? outer,
        CLSCTX classContext,
        in Guid iid,
        [MarshalAs(UnmanagedType.IUnknown)] out object? created);

    public static void SafeReleaseBlock<TIF>(this TIF intf, Action<TIF> action)
        where TIF : notnull
    {
        try
        {
            action(intf);
        }
        finally
        {
            Marshal.ReleaseComObject(intf);
        }
    }

    public static TR SafeReleaseBlock<TR, TIF>(this TIF intf, Func<TIF, TR> action)
        where TIF : notnull
    {
        try
        {
            return action(intf);
        }
        finally
        {
            Marshal.ReleaseComObject(intf);
        }
    }

    public static IEnumerable<IMoniker> EnumerateDeviceMoniker(Guid deviceCategory)
    {
        if (CoCreateInstance(
            in CLSID_SystemDeviceEnum,
            null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            in IID_ICreateDevEnum,
            out var cde) == 0 &&
            cde != null)
        {
            if (cde is ICreateDevEnum deviceEnumCreator)
            {
                if (deviceEnumCreator.CreateClassEnumerator(
                    in deviceCategory,
                    out var enumMoniker, 0) == 0 &&
                    enumMoniker is { })
                {
                    var monikers = new IMoniker?[1];
                    while (enumMoniker.Next(monikers.Length, monikers, out var fetched) == 0 &&
                        fetched == monikers.Length &&
                        monikers[0] is { })
                    {
                        yield return monikers[0]!;
                        Marshal.ReleaseComObject(monikers[0]!);
                    }
                    Marshal.ReleaseComObject(enumMoniker);
                }
            }
            Marshal.ReleaseComObject(cde);
        }
    }

    public static IPropertyBag? GetPropertyBag(
        this IMoniker moniker) =>
        moniker.BindToStorage(
            null, null, in IID_IPropertyBag, out var pb) == 0 &&
            pb is IPropertyBag propertyBag ?
                propertyBag : null;

    public static T GetValue<T>(
        this IPropertyBag pb, string name, T defaultValue = default!) =>
        pb.Read(name, out var value, null) == 0 ?
            (T)value : defaultValue;

    public static IEnumerable<IPin> EnumeratePins(
        this IBaseFilter baseFilter)
    {
        if (baseFilter.EnumPins(out var enumPins) == 0 &&
            enumPins is { })
        {
            var pins = new IPin?[1];
            while (enumPins.Next(pins.Length, pins, out var fetched) == 0 &&
                fetched == pins.Length &&
                pins[0] is { })
            {
                yield return pins[0]!;
            }
            Marshal.ReleaseComObject(enumPins);
        }
    }

    public static PIN_INFO? GetPinInfo(
        this IPin pin) =>
        pin.QueryPinInfo(out var pinInfo) == 0 ?
            pinInfo : null;

    public sealed class VideoMediaFormat : IDisposable
    {
        public readonly AM_MEDIA_TYPE PartialMediaType;
        public readonly NativeMethods.VIDEOINFOHEADER VideoInformation;
        public readonly VIDEO_STREAM_CONFIG_CAPS Capabilities;
        
        private IntPtr pBih_;

        public VideoMediaFormat(
            AM_MEDIA_TYPE partialMediaType,
            in NativeMethods.VIDEOINFOHEADER information,
            IntPtr pBih,
            in VIDEO_STREAM_CONFIG_CAPS capabilities)
        {
            Debug.Assert(partialMediaType.pFormat == IntPtr.Zero);
            Debug.Assert(partialMediaType.pUnk == IntPtr.Zero);

            this.PartialMediaType = partialMediaType;
            this.VideoInformation = information;
            this.pBih_ = pBih;
            this.Capabilities = capabilities;
        }

        ~VideoMediaFormat() =>
            this.Dispose();

        public void Dispose()
        {
            if (this.pBih_ != IntPtr.Zero)
            {
                NativeMethods.FreeMemory(this.pBih_);
                this.pBih_ = IntPtr.Zero;
            }
        }

        public IntPtr pBih =>
            this.pBih_;

        public unsafe AM_MEDIA_TYPE AllocateFormalMediaType()
        {
            // Copy.
            var mediaType = this.PartialMediaType;

            if (mediaType.formattype == FORMAT_VideoInfo)
            {
                var pBihFrom = (NativeMethods.BITMAPINFOHEADER*)this.pBih.ToPointer();
                var bihFromSize = pBihFrom->CalculateRawSize();

                mediaType.formatSize = sizeof(NativeMethods.VIDEOINFOHEADER) + bihFromSize;
                mediaType.pFormat = NativeMethods.AllocateMemory((IntPtr)mediaType.formatSize);

                var pVihTo = (NativeMethods.VIDEOINFOHEADER*)mediaType.pFormat;
                *pVihTo = this.VideoInformation;

                var pBihTo = (NativeMethods.BITMAPINFOHEADER*)(pVihTo + 1);
                NativeMethods.CopyMemory((IntPtr)pBihTo, (IntPtr)pBihFrom, (IntPtr)bihFromSize);
            }
            else if (mediaType.formattype == FORMAT_VideoInfo2)
            {
                var pBihFrom = (NativeMethods.BITMAPINFOHEADER*)this.pBih.ToPointer();
                var bihFromSize = pBihFrom->CalculateRawSize();

                mediaType.formatSize = sizeof(NativeMethods.VIDEOINFOHEADER2) + bihFromSize;
                mediaType.pFormat = NativeMethods.AllocateMemory((IntPtr)mediaType.formatSize);

                var pVihTo = (NativeMethods.VIDEOINFOHEADER*)mediaType.pFormat;
                *pVihTo = this.VideoInformation;

                var pBihTo = (NativeMethods.BITMAPINFOHEADER*)(((byte*)pVihTo) + sizeof(NativeMethods.VIDEOINFOHEADER2));
                NativeMethods.CopyMemory((IntPtr)pBihTo, (IntPtr)pBihFrom, (IntPtr)bihFromSize);
            }
            else
            {
                throw new ArgumentException();
            }

            return mediaType;
        }
        
        public VideoCharacteristics? CreateVideoCharacteristics() =>
            NativeMethods.CreateVideoCharacteristics(
                this.pBih,
                new Fraction(
                    // Precision is only under 3 digits (0.001)
                    (int)(10_000_000_000.0 / this.VideoInformation.AvgTimePerFrame),
                    1_000).Reduce(),
                true, // TODO: support non discrete entries.
                this.PartialMediaType.subtype.ToString());
    }

    ////////////////////////////////////////////////////////////////////////

    private static unsafe readonly int videoStreamConfigCapsSize =
        sizeof(VIDEO_STREAM_CONFIG_CAPS);

    public static bool SetFormat(this IPin pin, VideoMediaFormat format)
    {
        if (pin is IAMStreamConfig streamConfig)
        {
            var mediaType = format.AllocateFormalMediaType();
            try
            {
                if (streamConfig.SetFormat(in mediaType) == 0)
                {
                    return true;
                }
            }
            finally
            {
                mediaType.Release();
            }
        }
        return false;
    }

    public static IEnumerable<VideoMediaFormat> EnumerateFormats(
        this IPin pin)
    {
        static unsafe AM_MEDIA_TYPE CloneAndRelease(IntPtr pMediaType)
        {
            var pmt = (AM_MEDIA_TYPE*)pMediaType.ToPointer();
            var mt = *pmt;   // Copy.
            NativeMethods.FreeMemory(pMediaType);
            return mt;
        }

        static unsafe bool Extract(
            in AM_MEDIA_TYPE mediaType,
            out NativeMethods.VIDEOINFOHEADER vih,
            out IntPtr pBihResult)
        {
            if (mediaType.majortype == MEDIATYPE_Video)
            {
                if (mediaType.formattype == FORMAT_VideoInfo &&
                    mediaType.formatSize >=
                    (sizeof(NativeMethods.VIDEOINFOHEADER) +
                     sizeof(NativeMethods.BITMAPINFOHEADER)))
                {
                    var pVih = (NativeMethods.VIDEOINFOHEADER*)mediaType.pFormat.ToPointer();
                    vih = *pVih;

                    var pBih = (NativeMethods.BITMAPINFOHEADER*)(pVih + 1);
                    pBihResult = NativeMethods.AllocateMemory((IntPtr)pBih->biSize);
                    NativeMethods.CopyMemory(pBihResult, (IntPtr)pBih, (IntPtr)pBih->biSize);

                    return true;
                }
                if (mediaType.formattype == FORMAT_VideoInfo2 &&
                    mediaType.formatSize >=
                    (sizeof(NativeMethods.VIDEOINFOHEADER2) +
                     sizeof(NativeMethods.BITMAPINFOHEADER)))
                {
                    var pVih = (NativeMethods.VIDEOINFOHEADER*)mediaType.pFormat.ToPointer();
                    vih = *pVih;

                    var pVih2 = (NativeMethods.VIDEOINFOHEADER2*)mediaType.pFormat.ToPointer();
                    var pBih = (NativeMethods.BITMAPINFOHEADER*)(pVih2 + 1);
                    pBihResult = NativeMethods.AllocateMemory((IntPtr)pBih->biSize);
                    NativeMethods.CopyMemory(pBihResult, (IntPtr)pBih, (IntPtr)pBih->biSize);

                    return true;
                }
            }

            vih = default;
            pBihResult = default;
            return false;
        }

        if (pin is IAMStreamConfig streamConfig)
        {
            if (streamConfig.GetNumberOfCapabilities(out var count, out var size) == 0 &&
                size == videoStreamConfigCapsSize)
            {
                for (var index = 0; index < count; index++)
                {
                    if (streamConfig.GetStreamCaps(index, out var pMediaType, out var caps) == 0 &&
                        pMediaType != IntPtr.Zero)
                    {
                        var mediaType = CloneAndRelease(pMediaType);
                        try
                        {
                            if (Extract(in mediaType, out var vih, out var pBih))
                            {
                                // Copy.
                                var partialMediaType = mediaType;
                                partialMediaType.pFormat = IntPtr.Zero;
                                yield return new VideoMediaFormat(partialMediaType, vih, pBih, caps);
                            }
                        }
                        finally
                        {
                            mediaType.Release();
                        }
                    }
                }
            }
        }
    }

    public static IGraphBuilder CreateGraphBuilder()
    {
        if (CoCreateInstance(
            in CLSID_GraphBuilder,
            null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            in IID_IGraphBuilder,
            out var gb) == 0 &&
            gb is IGraphBuilder graphBuilder)
        {
            return graphBuilder;
        }
        else
        {
            throw new InvalidOperationException("FlashCap: Couldn't create graph builder.");
        }
    }

    public static ISampleGrabber CreateSampleGrabber()
    {
        // OMG, the sample grabber is deplicated.
        // It isn't removed now, but feel remove in Windows future release...
        // https://docs.microsoft.com/en-us/windows/win32/directshow/isamplegrabber
        if (CoCreateInstance(
            in CLSID_SampleGrabber,
            null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            in IID_ISampleGrabber,
            out var sg) == 0 &&
            sg is ISampleGrabber sampleGrabber)
        {
            return sampleGrabber;
        }
        else
        {
            throw new InvalidOperationException("FlashCap: Couldn't create sample grabber.");
        }
    }

    public static IBaseFilter CreateNullRenderer()
    {
        // OMG, the null renderer is deplicated.
        // It isn't removed now, but feel remove in Windows future release...
        // https://docs.microsoft.com/en-us/windows/win32/directshow/null-renderer-filter
        if (CoCreateInstance(
            in CLSID_NullRenderer,
            null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            in IID_IBaseFilter,
            out var nr) == 0 &&
            nr is IBaseFilter nullRenderer)
        {
            return nullRenderer;
        }
        else
        {
            throw new InvalidOperationException("FlashCap: Couldn't create null renderer.");
        }
    }

    public static ICaptureGraphBuilder2 CreateCaptureGraphBuilder()
    {
        if (CoCreateInstance(
            in CLSID_CaptureGraphBuilder2,
            null,
            CLSCTX.CLSCTX_INPROC_SERVER,
            in IID_ICaptureGraphBuilder2,
            out var cgb) == 0 &&
            cgb is ICaptureGraphBuilder2 captureGraphBuilder)
        {
            return captureGraphBuilder;
        }
        else
        {
            throw new InvalidOperationException("FlashCap: Couldn't create capture graph builder.");
        }
    }
}
