// This is auto generated code by FlashCap.V4L2Generator [0.14.6]. Do not edit.
// Linux version 5.13.0-39-generic (buildd@lcy02-amd64-080) (gcc (Ubuntu 9.4.0-1ubuntu1~20.04.1) 9.4.0, GNU ld (GNU Binutils for Ubuntu) 2.34) #44~20.04.1-Ubuntu SMP Thu Mar 24 16:43:35 UTC 2022
// Fri, 15 Apr 2022 03:59:31 GMT

using System;
using System.Runtime.InteropServices;

namespace FlashCap.Internal.V4L2
{
    internal sealed class NativeMethods_V4L2_Interop_x86_64 : NativeMethods_V4L2_Interop
    {
        // Common
        public override string Label => "Linux version 5.13.0-39-generic (buildd@lcy02-amd64-080) (gcc (Ubuntu 9.4.0-1ubuntu1~20.04.1) 9.4.0, GNU ld (GNU Binutils for Ubuntu) 2.34) #44~20.04.1-Ubuntu SMP Thu Mar 24 16:43:35 UTC 2022";
        public override string Architecture => "x86_64";
        public override int sizeof_size_t => 8;
        public override int sizeof_off_t => 8;

        // Definitions
        public override uint V4L2_CAP_VIDEO_CAPTURE => 1U;
        public override uint V4L2_PIX_FMT_ABGR32 => 875713089U;
        public override uint V4L2_PIX_FMT_ARGB32 => 875708738U;
        public override uint V4L2_PIX_FMT_JPEG => 1195724874U;
        public override uint V4L2_PIX_FMT_MJPEG => 1196444237U;
        public override uint V4L2_PIX_FMT_RGB24 => 859981650U;
        public override uint V4L2_PIX_FMT_RGB332 => 826427218U;
        public override uint V4L2_PIX_FMT_RGB565 => 1346520914U;
        public override uint V4L2_PIX_FMT_RGB565X => 1380075346U;
        public override uint V4L2_PIX_FMT_UYVY => 1498831189U;
        public override uint V4L2_PIX_FMT_XRGB32 => 875714626U;
        public override uint V4L2_PIX_FMT_YUYV => 1448695129U;
        public override uint VIDIOC_DQBUF => 3227014673U;
        public override uint VIDIOC_ENUM_FMT => 3225441794U;
        public override uint VIDIOC_ENUM_FRAMEINTERVALS => 3224655435U;
        public override uint VIDIOC_ENUM_FRAMESIZES => 3224131146U;
        public override uint VIDIOC_QBUF => 3227014671U;
        public override uint VIDIOC_QUERYBUF => 3227014665U;
        public override uint VIDIOC_QUERYCAP => 2154321408U;
        public override uint VIDIOC_REQBUFS => 3222558216U;
        public override uint VIDIOC_S_FMT => 3234878981U;
        public override uint VIDIOC_STREAMOFF => 1074026003U;
        public override uint VIDIOC_STREAMON => 1074026002U;

        // Structures
        [StructLayout(LayoutKind.Explicit, Size=16)]
        private new unsafe struct timespec : NativeMethods_V4L2_Interop.timespec
        {
            [FieldOffset(0)] private IntPtr tv_sec_;   // long
            public IntPtr tv_sec
            {
                get => this.tv_sec_;
                set => this.tv_sec_ = (IntPtr)value;
            }

            [FieldOffset(8)] private IntPtr tv_nsec_;   // long
            public IntPtr tv_nsec
            {
                get => this.tv_nsec_;
                set => this.tv_nsec_ = (IntPtr)value;
            }

        }
        public override NativeMethods_V4L2_Interop.timespec Create_timespec() => new timespec();

        [StructLayout(LayoutKind.Explicit, Size=16)]
        private new unsafe struct timeval : NativeMethods_V4L2_Interop.timeval
        {
            [FieldOffset(0)] private IntPtr tv_sec_;   // long
            public IntPtr tv_sec
            {
                get => this.tv_sec_;
                set => this.tv_sec_ = (IntPtr)value;
            }

            [FieldOffset(8)] private IntPtr tv_usec_;   // long
            public IntPtr tv_usec
            {
                get => this.tv_usec_;
                set => this.tv_usec_ = (IntPtr)value;
            }

        }
        public override NativeMethods_V4L2_Interop.timeval Create_timeval() => new timeval();

        [StructLayout(LayoutKind.Explicit, Size=88)]
        private new unsafe struct v4l2_buffer : NativeMethods_V4L2_Interop.v4l2_buffer
        {
            [FieldOffset(0)] private uint index_;
            public uint index
            {
                get => this.index_;
                set => this.index_ = (uint)value;
            }

            [FieldOffset(4)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(8)] private uint bytesused_;
            public uint bytesused
            {
                get => this.bytesused_;
                set => this.bytesused_ = (uint)value;
            }

            [FieldOffset(12)] private uint flags_;
            public uint flags
            {
                get => this.flags_;
                set => this.flags_ = (uint)value;
            }

            [FieldOffset(16)] private uint field_;
            public uint field
            {
                get => this.field_;
                set => this.field_ = (uint)value;
            }

            [FieldOffset(24)] private timeval timestamp_;
            public NativeMethods_V4L2_Interop.timeval timestamp
            {
                get => this.timestamp_;
                set => this.timestamp_ = (timeval)value;
            }

            [FieldOffset(40)] private v4l2_timecode timecode_;
            public NativeMethods_V4L2_Interop.v4l2_timecode timecode
            {
                get => this.timecode_;
                set => this.timecode_ = (v4l2_timecode)value;
            }

            [FieldOffset(56)] private uint sequence_;
            public uint sequence
            {
                get => this.sequence_;
                set => this.sequence_ = (uint)value;
            }

            [FieldOffset(60)] private uint memory_;
            public uint memory
            {
                get => this.memory_;
                set => this.memory_ = (uint)value;
            }

            [FieldOffset(64)] private uint m_offset_;
            public uint m_offset
            {
                get => this.m_offset_;
                set => this.m_offset_ = (uint)value;
            }

            [FieldOffset(64)] private UIntPtr m_userptr_;   // unsigned long
            public UIntPtr m_userptr
            {
                get => this.m_userptr_;
                set => this.m_userptr_ = (UIntPtr)value;
            }

            [FieldOffset(64)] private v4l2_plane* m_planes_;
            public IntPtr m_planes
            {
                get => (IntPtr)this.m_planes_;
                set => this.m_planes_ = (v4l2_plane*)value.ToPointer();
            }

            [FieldOffset(64)] private int m_fd_;
            public int m_fd
            {
                get => this.m_fd_;
                set => this.m_fd_ = (int)value;
            }

            [FieldOffset(72)] private uint length_;
            public uint length
            {
                get => this.length_;
                set => this.length_ = (uint)value;
            }

            [FieldOffset(76)] private uint reserved2_;
            public uint reserved2
            {
                get => this.reserved2_;
                set => this.reserved2_ = (uint)value;
            }

            [FieldOffset(80)] private int request_fd_;
            public int request_fd
            {
                get => this.request_fd_;
                set => this.request_fd_ = (int)value;
            }

            [FieldOffset(80)] private uint reserved_;
            public uint reserved
            {
                get => this.reserved_;
                set => this.reserved_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_buffer Create_v4l2_buffer() => new v4l2_buffer();

        [StructLayout(LayoutKind.Explicit, Size=104)]
        private new unsafe struct v4l2_capability : NativeMethods_V4L2_Interop.v4l2_capability
        {
            [FieldOffset(0)] private fixed byte driver_[16];
            public byte[] driver
            {
                get { fixed (byte* p = this.driver_) { return get(p, 16); } }
                set { fixed (byte* p = this.driver_) { set(p, value, 16); } }
            }

            [FieldOffset(16)] private fixed byte card_[32];
            public byte[] card
            {
                get { fixed (byte* p = this.card_) { return get(p, 32); } }
                set { fixed (byte* p = this.card_) { set(p, value, 32); } }
            }

            [FieldOffset(48)] private fixed byte bus_info_[32];
            public byte[] bus_info
            {
                get { fixed (byte* p = this.bus_info_) { return get(p, 32); } }
                set { fixed (byte* p = this.bus_info_) { set(p, value, 32); } }
            }

            [FieldOffset(80)] private uint version_;
            public uint version
            {
                get => this.version_;
                set => this.version_ = (uint)value;
            }

            [FieldOffset(84)] private uint capabilities_;
            public uint capabilities
            {
                get => this.capabilities_;
                set => this.capabilities_ = (uint)value;
            }

            [FieldOffset(88)] private uint device_caps_;
            public uint device_caps
            {
                get => this.device_caps_;
                set => this.device_caps_ = (uint)value;
            }

            [FieldOffset(92)] private fixed uint reserved_[3];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 3); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 3); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_capability Create_v4l2_capability() => new v4l2_capability();

        [StructLayout(LayoutKind.Explicit, Size=24)]
        private new unsafe struct v4l2_clip : NativeMethods_V4L2_Interop.v4l2_clip
        {
            [FieldOffset(0)] private v4l2_rect c_;
            public NativeMethods_V4L2_Interop.v4l2_rect c
            {
                get => this.c_;
                set => this.c_ = (v4l2_rect)value;
            }

            [FieldOffset(16)] private v4l2_clip* next_;
            public IntPtr next
            {
                get => (IntPtr)this.next_;
                set => this.next_ = (v4l2_clip*)value.ToPointer();
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_clip Create_v4l2_clip() => new v4l2_clip();

        [StructLayout(LayoutKind.Explicit, Size=64)]
        private new unsafe struct v4l2_fmtdesc : NativeMethods_V4L2_Interop.v4l2_fmtdesc
        {
            [FieldOffset(0)] private uint index_;
            public uint index
            {
                get => this.index_;
                set => this.index_ = (uint)value;
            }

            [FieldOffset(4)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(8)] private uint flags_;
            public uint flags
            {
                get => this.flags_;
                set => this.flags_ = (uint)value;
            }

            [FieldOffset(12)] private fixed byte description_[32];
            public byte[] description
            {
                get { fixed (byte* p = this.description_) { return get(p, 32); } }
                set { fixed (byte* p = this.description_) { set(p, value, 32); } }
            }

            [FieldOffset(44)] private uint pixelformat_;
            public uint pixelformat
            {
                get => this.pixelformat_;
                set => this.pixelformat_ = (uint)value;
            }

            [FieldOffset(48)] private fixed uint reserved_[4];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 4); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 4); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_fmtdesc Create_v4l2_fmtdesc() => new v4l2_fmtdesc();

        [StructLayout(LayoutKind.Explicit, Size=208)]
        private new unsafe struct v4l2_format : NativeMethods_V4L2_Interop.v4l2_format
        {
            [FieldOffset(0)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(8)] private v4l2_pix_format fmt_pix_;
            public NativeMethods_V4L2_Interop.v4l2_pix_format fmt_pix
            {
                get => this.fmt_pix_;
                set => this.fmt_pix_ = (v4l2_pix_format)value;
            }

            [FieldOffset(8)] private v4l2_pix_format_mplane fmt_pix_mp_;
            public NativeMethods_V4L2_Interop.v4l2_pix_format_mplane fmt_pix_mp
            {
                get => this.fmt_pix_mp_;
                set => this.fmt_pix_mp_ = (v4l2_pix_format_mplane)value;
            }

            [FieldOffset(8)] private v4l2_window fmt_win_;
            public NativeMethods_V4L2_Interop.v4l2_window fmt_win
            {
                get => this.fmt_win_;
                set => this.fmt_win_ = (v4l2_window)value;
            }

            [FieldOffset(8)] private v4l2_vbi_format fmt_vbi_;
            public NativeMethods_V4L2_Interop.v4l2_vbi_format fmt_vbi
            {
                get => this.fmt_vbi_;
                set => this.fmt_vbi_ = (v4l2_vbi_format)value;
            }

            [FieldOffset(8)] private v4l2_sliced_vbi_format fmt_sliced_;
            public NativeMethods_V4L2_Interop.v4l2_sliced_vbi_format fmt_sliced
            {
                get => this.fmt_sliced_;
                set => this.fmt_sliced_ = (v4l2_sliced_vbi_format)value;
            }

            [FieldOffset(8)] private v4l2_sdr_format fmt_sdr_;
            public NativeMethods_V4L2_Interop.v4l2_sdr_format fmt_sdr
            {
                get => this.fmt_sdr_;
                set => this.fmt_sdr_ = (v4l2_sdr_format)value;
            }

            [FieldOffset(8)] private v4l2_meta_format fmt_meta_;
            public NativeMethods_V4L2_Interop.v4l2_meta_format fmt_meta
            {
                get => this.fmt_meta_;
                set => this.fmt_meta_ = (v4l2_meta_format)value;
            }

            [FieldOffset(8)] private fixed byte fmt_raw_data_[200];
            public byte[] fmt_raw_data
            {
                get { fixed (byte* p = this.fmt_raw_data_) { return get(p, 200); } }
                set { fixed (byte* p = this.fmt_raw_data_) { set(p, value, 200); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_format Create_v4l2_format() => new v4l2_format();

        [StructLayout(LayoutKind.Explicit, Size=8)]
        private new unsafe struct v4l2_fract : NativeMethods_V4L2_Interop.v4l2_fract
        {
            [FieldOffset(0)] private uint numerator_;
            public uint numerator
            {
                get => this.numerator_;
                set => this.numerator_ = (uint)value;
            }

            [FieldOffset(4)] private uint denominator_;
            public uint denominator
            {
                get => this.denominator_;
                set => this.denominator_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_fract Create_v4l2_fract() => new v4l2_fract();

        [StructLayout(LayoutKind.Explicit, Size=24)]
        private new unsafe struct v4l2_frmival_stepwise : NativeMethods_V4L2_Interop.v4l2_frmival_stepwise
        {
            [FieldOffset(0)] private v4l2_fract min_;
            public NativeMethods_V4L2_Interop.v4l2_fract min
            {
                get => this.min_;
                set => this.min_ = (v4l2_fract)value;
            }

            [FieldOffset(8)] private v4l2_fract max_;
            public NativeMethods_V4L2_Interop.v4l2_fract max
            {
                get => this.max_;
                set => this.max_ = (v4l2_fract)value;
            }

            [FieldOffset(16)] private v4l2_fract step_;
            public NativeMethods_V4L2_Interop.v4l2_fract step
            {
                get => this.step_;
                set => this.step_ = (v4l2_fract)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_frmival_stepwise Create_v4l2_frmival_stepwise() => new v4l2_frmival_stepwise();

        [StructLayout(LayoutKind.Explicit, Size=52)]
        private new unsafe struct v4l2_frmivalenum : NativeMethods_V4L2_Interop.v4l2_frmivalenum
        {
            [FieldOffset(0)] private uint index_;
            public uint index
            {
                get => this.index_;
                set => this.index_ = (uint)value;
            }

            [FieldOffset(4)] private uint pixel_format_;
            public uint pixel_format
            {
                get => this.pixel_format_;
                set => this.pixel_format_ = (uint)value;
            }

            [FieldOffset(8)] private uint width_;
            public uint width
            {
                get => this.width_;
                set => this.width_ = (uint)value;
            }

            [FieldOffset(12)] private uint height_;
            public uint height
            {
                get => this.height_;
                set => this.height_ = (uint)value;
            }

            [FieldOffset(16)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(20)] private v4l2_fract discrete_;
            public NativeMethods_V4L2_Interop.v4l2_fract discrete
            {
                get => this.discrete_;
                set => this.discrete_ = (v4l2_fract)value;
            }

            [FieldOffset(20)] private v4l2_frmival_stepwise stepwise_;
            public NativeMethods_V4L2_Interop.v4l2_frmival_stepwise stepwise
            {
                get => this.stepwise_;
                set => this.stepwise_ = (v4l2_frmival_stepwise)value;
            }

            [FieldOffset(44)] private fixed uint reserved_[2];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 2); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 2); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_frmivalenum Create_v4l2_frmivalenum() => new v4l2_frmivalenum();

        [StructLayout(LayoutKind.Explicit, Size=8)]
        private new unsafe struct v4l2_frmsize_discrete : NativeMethods_V4L2_Interop.v4l2_frmsize_discrete
        {
            [FieldOffset(0)] private uint width_;
            public uint width
            {
                get => this.width_;
                set => this.width_ = (uint)value;
            }

            [FieldOffset(4)] private uint height_;
            public uint height
            {
                get => this.height_;
                set => this.height_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_frmsize_discrete Create_v4l2_frmsize_discrete() => new v4l2_frmsize_discrete();

        [StructLayout(LayoutKind.Explicit, Size=24)]
        private new unsafe struct v4l2_frmsize_stepwise : NativeMethods_V4L2_Interop.v4l2_frmsize_stepwise
        {
            [FieldOffset(0)] private uint min_width_;
            public uint min_width
            {
                get => this.min_width_;
                set => this.min_width_ = (uint)value;
            }

            [FieldOffset(4)] private uint max_width_;
            public uint max_width
            {
                get => this.max_width_;
                set => this.max_width_ = (uint)value;
            }

            [FieldOffset(8)] private uint step_width_;
            public uint step_width
            {
                get => this.step_width_;
                set => this.step_width_ = (uint)value;
            }

            [FieldOffset(12)] private uint min_height_;
            public uint min_height
            {
                get => this.min_height_;
                set => this.min_height_ = (uint)value;
            }

            [FieldOffset(16)] private uint max_height_;
            public uint max_height
            {
                get => this.max_height_;
                set => this.max_height_ = (uint)value;
            }

            [FieldOffset(20)] private uint step_height_;
            public uint step_height
            {
                get => this.step_height_;
                set => this.step_height_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_frmsize_stepwise Create_v4l2_frmsize_stepwise() => new v4l2_frmsize_stepwise();

        [StructLayout(LayoutKind.Explicit, Size=44)]
        private new unsafe struct v4l2_frmsizeenum : NativeMethods_V4L2_Interop.v4l2_frmsizeenum
        {
            [FieldOffset(0)] private uint index_;
            public uint index
            {
                get => this.index_;
                set => this.index_ = (uint)value;
            }

            [FieldOffset(4)] private uint pixel_format_;
            public uint pixel_format
            {
                get => this.pixel_format_;
                set => this.pixel_format_ = (uint)value;
            }

            [FieldOffset(8)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(12)] private v4l2_frmsize_discrete discrete_;
            public NativeMethods_V4L2_Interop.v4l2_frmsize_discrete discrete
            {
                get => this.discrete_;
                set => this.discrete_ = (v4l2_frmsize_discrete)value;
            }

            [FieldOffset(12)] private v4l2_frmsize_stepwise stepwise_;
            public NativeMethods_V4L2_Interop.v4l2_frmsize_stepwise stepwise
            {
                get => this.stepwise_;
                set => this.stepwise_ = (v4l2_frmsize_stepwise)value;
            }

            [FieldOffset(36)] private fixed uint reserved_[2];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 2); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 2); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_frmsizeenum Create_v4l2_frmsizeenum() => new v4l2_frmsizeenum();

        [StructLayout(LayoutKind.Explicit, Size=8)]
        private new unsafe struct v4l2_meta_format : NativeMethods_V4L2_Interop.v4l2_meta_format
        {
            [FieldOffset(0)] private uint dataformat_;
            public uint dataformat
            {
                get => this.dataformat_;
                set => this.dataformat_ = (uint)value;
            }

            [FieldOffset(4)] private uint buffersize_;
            public uint buffersize
            {
                get => this.buffersize_;
                set => this.buffersize_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_meta_format Create_v4l2_meta_format() => new v4l2_meta_format();

        [StructLayout(LayoutKind.Explicit, Size=48)]
        private new unsafe struct v4l2_pix_format : NativeMethods_V4L2_Interop.v4l2_pix_format
        {
            [FieldOffset(0)] private uint width_;
            public uint width
            {
                get => this.width_;
                set => this.width_ = (uint)value;
            }

            [FieldOffset(4)] private uint height_;
            public uint height
            {
                get => this.height_;
                set => this.height_ = (uint)value;
            }

            [FieldOffset(8)] private uint pixelformat_;
            public uint pixelformat
            {
                get => this.pixelformat_;
                set => this.pixelformat_ = (uint)value;
            }

            [FieldOffset(12)] private uint field_;
            public uint field
            {
                get => this.field_;
                set => this.field_ = (uint)value;
            }

            [FieldOffset(16)] private uint bytesperline_;
            public uint bytesperline
            {
                get => this.bytesperline_;
                set => this.bytesperline_ = (uint)value;
            }

            [FieldOffset(20)] private uint sizeimage_;
            public uint sizeimage
            {
                get => this.sizeimage_;
                set => this.sizeimage_ = (uint)value;
            }

            [FieldOffset(24)] private uint colorspace_;
            public uint colorspace
            {
                get => this.colorspace_;
                set => this.colorspace_ = (uint)value;
            }

            [FieldOffset(28)] private uint priv_;
            public uint priv
            {
                get => this.priv_;
                set => this.priv_ = (uint)value;
            }

            [FieldOffset(32)] private uint flags_;
            public uint flags
            {
                get => this.flags_;
                set => this.flags_ = (uint)value;
            }

            [FieldOffset(36)] private uint ycbcr_enc_;
            public uint ycbcr_enc
            {
                get => this.ycbcr_enc_;
                set => this.ycbcr_enc_ = (uint)value;
            }

            [FieldOffset(36)] private uint hsv_enc_;
            public uint hsv_enc
            {
                get => this.hsv_enc_;
                set => this.hsv_enc_ = (uint)value;
            }

            [FieldOffset(40)] private uint quantization_;
            public uint quantization
            {
                get => this.quantization_;
                set => this.quantization_ = (uint)value;
            }

            [FieldOffset(44)] private uint xfer_func_;
            public uint xfer_func
            {
                get => this.xfer_func_;
                set => this.xfer_func_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_pix_format Create_v4l2_pix_format() => new v4l2_pix_format();

        [StructLayout(LayoutKind.Explicit, Size=192)]
        private new unsafe struct v4l2_pix_format_mplane : NativeMethods_V4L2_Interop.v4l2_pix_format_mplane
        {
            [FieldOffset(0)] private uint width_;
            public uint width
            {
                get => this.width_;
                set => this.width_ = (uint)value;
            }

            [FieldOffset(4)] private uint height_;
            public uint height
            {
                get => this.height_;
                set => this.height_ = (uint)value;
            }

            [FieldOffset(8)] private uint pixelformat_;
            public uint pixelformat
            {
                get => this.pixelformat_;
                set => this.pixelformat_ = (uint)value;
            }

            [FieldOffset(12)] private uint field_;
            public uint field
            {
                get => this.field_;
                set => this.field_ = (uint)value;
            }

            [FieldOffset(16)] private uint colorspace_;
            public uint colorspace
            {
                get => this.colorspace_;
                set => this.colorspace_ = (uint)value;
            }

            [FieldOffset(20)] private fixed byte plane_fmt_[20 * 8];   // sizeof(v4l2_plane_pix_format): 20
            public NativeMethods_V4L2_Interop.v4l2_plane_pix_format[] plane_fmt
            {
                get { fixed (byte* p = this.plane_fmt_) { return get<v4l2_plane_pix_format, NativeMethods_V4L2_Interop.v4l2_plane_pix_format>(p, 20, 8); } }
                set { fixed (byte* p = this.plane_fmt_) { set<v4l2_plane_pix_format, NativeMethods_V4L2_Interop.v4l2_plane_pix_format>(p, value, 20, 8); } }
            }

            [FieldOffset(180)] private byte num_planes_;
            public byte num_planes
            {
                get => this.num_planes_;
                set => this.num_planes_ = (byte)value;
            }

            [FieldOffset(181)] private byte flags_;
            public byte flags
            {
                get => this.flags_;
                set => this.flags_ = (byte)value;
            }

            [FieldOffset(182)] private byte ycbcr_enc_;
            public byte ycbcr_enc
            {
                get => this.ycbcr_enc_;
                set => this.ycbcr_enc_ = (byte)value;
            }

            [FieldOffset(182)] private byte hsv_enc_;
            public byte hsv_enc
            {
                get => this.hsv_enc_;
                set => this.hsv_enc_ = (byte)value;
            }

            [FieldOffset(183)] private byte quantization_;
            public byte quantization
            {
                get => this.quantization_;
                set => this.quantization_ = (byte)value;
            }

            [FieldOffset(184)] private byte xfer_func_;
            public byte xfer_func
            {
                get => this.xfer_func_;
                set => this.xfer_func_ = (byte)value;
            }

            [FieldOffset(185)] private fixed byte reserved_[7];
            public byte[] reserved
            {
                get { fixed (byte* p = this.reserved_) { return get(p, 7); } }
                set { fixed (byte* p = this.reserved_) { set(p, value, 7); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_pix_format_mplane Create_v4l2_pix_format_mplane() => new v4l2_pix_format_mplane();

        [StructLayout(LayoutKind.Explicit, Size=64)]
        private new unsafe struct v4l2_plane : NativeMethods_V4L2_Interop.v4l2_plane
        {
            [FieldOffset(0)] private uint bytesused_;
            public uint bytesused
            {
                get => this.bytesused_;
                set => this.bytesused_ = (uint)value;
            }

            [FieldOffset(4)] private uint length_;
            public uint length
            {
                get => this.length_;
                set => this.length_ = (uint)value;
            }

            [FieldOffset(8)] private uint m_mem_offset_;
            public uint m_mem_offset
            {
                get => this.m_mem_offset_;
                set => this.m_mem_offset_ = (uint)value;
            }

            [FieldOffset(8)] private UIntPtr m_userptr_;   // unsigned long
            public UIntPtr m_userptr
            {
                get => this.m_userptr_;
                set => this.m_userptr_ = (UIntPtr)value;
            }

            [FieldOffset(8)] private int m_fd_;
            public int m_fd
            {
                get => this.m_fd_;
                set => this.m_fd_ = (int)value;
            }

            [FieldOffset(16)] private uint data_offset_;
            public uint data_offset
            {
                get => this.data_offset_;
                set => this.data_offset_ = (uint)value;
            }

            [FieldOffset(20)] private fixed uint reserved_[11];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 11); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 11); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_plane Create_v4l2_plane() => new v4l2_plane();

        [StructLayout(LayoutKind.Explicit, Size=20)]
        private new unsafe struct v4l2_plane_pix_format : NativeMethods_V4L2_Interop.v4l2_plane_pix_format
        {
            [FieldOffset(0)] private uint sizeimage_;
            public uint sizeimage
            {
                get => this.sizeimage_;
                set => this.sizeimage_ = (uint)value;
            }

            [FieldOffset(4)] private uint bytesperline_;
            public uint bytesperline
            {
                get => this.bytesperline_;
                set => this.bytesperline_ = (uint)value;
            }

            [FieldOffset(8)] private fixed ushort reserved_[6];
            public ushort[] reserved
            {
                get { fixed (ushort* p = this.reserved_) { return get(p, 6); } }
                set { fixed (ushort* p = this.reserved_) { set(p, value, 6); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_plane_pix_format Create_v4l2_plane_pix_format() => new v4l2_plane_pix_format();

        [StructLayout(LayoutKind.Explicit, Size=16)]
        private new unsafe struct v4l2_rect : NativeMethods_V4L2_Interop.v4l2_rect
        {
            [FieldOffset(0)] private int left_;
            public int left
            {
                get => this.left_;
                set => this.left_ = (int)value;
            }

            [FieldOffset(4)] private int top_;
            public int top
            {
                get => this.top_;
                set => this.top_ = (int)value;
            }

            [FieldOffset(8)] private uint width_;
            public uint width
            {
                get => this.width_;
                set => this.width_ = (uint)value;
            }

            [FieldOffset(12)] private uint height_;
            public uint height
            {
                get => this.height_;
                set => this.height_ = (uint)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_rect Create_v4l2_rect() => new v4l2_rect();

        [StructLayout(LayoutKind.Explicit, Size=20)]
        private new unsafe struct v4l2_requestbuffers : NativeMethods_V4L2_Interop.v4l2_requestbuffers
        {
            [FieldOffset(0)] private uint count_;
            public uint count
            {
                get => this.count_;
                set => this.count_ = (uint)value;
            }

            [FieldOffset(4)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(8)] private uint memory_;
            public uint memory
            {
                get => this.memory_;
                set => this.memory_ = (uint)value;
            }

            [FieldOffset(12)] private uint capabilities_;
            public uint capabilities
            {
                get => this.capabilities_;
                set => this.capabilities_ = (uint)value;
            }

            [FieldOffset(16)] private fixed uint reserved_[1];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 1); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 1); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_requestbuffers Create_v4l2_requestbuffers() => new v4l2_requestbuffers();

        [StructLayout(LayoutKind.Explicit, Size=32)]
        private new unsafe struct v4l2_sdr_format : NativeMethods_V4L2_Interop.v4l2_sdr_format
        {
            [FieldOffset(0)] private uint pixelformat_;
            public uint pixelformat
            {
                get => this.pixelformat_;
                set => this.pixelformat_ = (uint)value;
            }

            [FieldOffset(4)] private uint buffersize_;
            public uint buffersize
            {
                get => this.buffersize_;
                set => this.buffersize_ = (uint)value;
            }

            [FieldOffset(8)] private fixed byte reserved_[24];
            public byte[] reserved
            {
                get { fixed (byte* p = this.reserved_) { return get(p, 24); } }
                set { fixed (byte* p = this.reserved_) { set(p, value, 24); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_sdr_format Create_v4l2_sdr_format() => new v4l2_sdr_format();

        [StructLayout(LayoutKind.Explicit, Size=112)]
        private new unsafe struct v4l2_sliced_vbi_format : NativeMethods_V4L2_Interop.v4l2_sliced_vbi_format
        {
            [FieldOffset(0)] private ushort service_set_;
            public ushort service_set
            {
                get => this.service_set_;
                set => this.service_set_ = (ushort)value;
            }

            [FieldOffset(2)] private fixed ushort service_lines_[2 * 24];
            public ushort[][] service_lines
            {
                get { fixed (ushort* p = this.service_lines_) { return get(p, 2,24); } }
                set { fixed (ushort* p = this.service_lines_) { set(p, value, 2,24); } }
            }

            [FieldOffset(100)] private uint io_size_;
            public uint io_size
            {
                get => this.io_size_;
                set => this.io_size_ = (uint)value;
            }

            [FieldOffset(104)] private fixed uint reserved_[2];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 2); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 2); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_sliced_vbi_format Create_v4l2_sliced_vbi_format() => new v4l2_sliced_vbi_format();

        [StructLayout(LayoutKind.Explicit, Size=16)]
        private new unsafe struct v4l2_timecode : NativeMethods_V4L2_Interop.v4l2_timecode
        {
            [FieldOffset(0)] private uint type_;
            public uint type
            {
                get => this.type_;
                set => this.type_ = (uint)value;
            }

            [FieldOffset(4)] private uint flags_;
            public uint flags
            {
                get => this.flags_;
                set => this.flags_ = (uint)value;
            }

            [FieldOffset(8)] private byte frames_;
            public byte frames
            {
                get => this.frames_;
                set => this.frames_ = (byte)value;
            }

            [FieldOffset(9)] private byte seconds_;
            public byte seconds
            {
                get => this.seconds_;
                set => this.seconds_ = (byte)value;
            }

            [FieldOffset(10)] private byte minutes_;
            public byte minutes
            {
                get => this.minutes_;
                set => this.minutes_ = (byte)value;
            }

            [FieldOffset(11)] private byte hours_;
            public byte hours
            {
                get => this.hours_;
                set => this.hours_ = (byte)value;
            }

            [FieldOffset(12)] private fixed byte userbits_[4];
            public byte[] userbits
            {
                get { fixed (byte* p = this.userbits_) { return get(p, 4); } }
                set { fixed (byte* p = this.userbits_) { set(p, value, 4); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_timecode Create_v4l2_timecode() => new v4l2_timecode();

        [StructLayout(LayoutKind.Explicit, Size=44)]
        private new unsafe struct v4l2_vbi_format : NativeMethods_V4L2_Interop.v4l2_vbi_format
        {
            [FieldOffset(0)] private uint sampling_rate_;
            public uint sampling_rate
            {
                get => this.sampling_rate_;
                set => this.sampling_rate_ = (uint)value;
            }

            [FieldOffset(4)] private uint offset_;
            public uint offset
            {
                get => this.offset_;
                set => this.offset_ = (uint)value;
            }

            [FieldOffset(8)] private uint samples_per_line_;
            public uint samples_per_line
            {
                get => this.samples_per_line_;
                set => this.samples_per_line_ = (uint)value;
            }

            [FieldOffset(12)] private uint sample_format_;
            public uint sample_format
            {
                get => this.sample_format_;
                set => this.sample_format_ = (uint)value;
            }

            [FieldOffset(16)] private fixed int start_[2];
            public int[] start
            {
                get { fixed (int* p = this.start_) { return get(p, 2); } }
                set { fixed (int* p = this.start_) { set(p, value, 2); } }
            }

            [FieldOffset(24)] private fixed uint count_[2];
            public uint[] count
            {
                get { fixed (uint* p = this.count_) { return get(p, 2); } }
                set { fixed (uint* p = this.count_) { set(p, value, 2); } }
            }

            [FieldOffset(32)] private uint flags_;
            public uint flags
            {
                get => this.flags_;
                set => this.flags_ = (uint)value;
            }

            [FieldOffset(36)] private fixed uint reserved_[2];
            public uint[] reserved
            {
                get { fixed (uint* p = this.reserved_) { return get(p, 2); } }
                set { fixed (uint* p = this.reserved_) { set(p, value, 2); } }
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_vbi_format Create_v4l2_vbi_format() => new v4l2_vbi_format();

        [StructLayout(LayoutKind.Explicit, Size=56)]
        private new unsafe struct v4l2_window : NativeMethods_V4L2_Interop.v4l2_window
        {
            [FieldOffset(0)] private v4l2_rect w_;
            public NativeMethods_V4L2_Interop.v4l2_rect w
            {
                get => this.w_;
                set => this.w_ = (v4l2_rect)value;
            }

            [FieldOffset(16)] private uint field_;
            public uint field
            {
                get => this.field_;
                set => this.field_ = (uint)value;
            }

            [FieldOffset(20)] private uint chromakey_;
            public uint chromakey
            {
                get => this.chromakey_;
                set => this.chromakey_ = (uint)value;
            }

            [FieldOffset(24)] private v4l2_clip* clips_;
            public IntPtr clips
            {
                get => (IntPtr)this.clips_;
                set => this.clips_ = (v4l2_clip*)value.ToPointer();
            }

            [FieldOffset(32)] private uint clipcount_;
            public uint clipcount
            {
                get => this.clipcount_;
                set => this.clipcount_ = (uint)value;
            }

            [FieldOffset(40)] private void* bitmap_;
            public IntPtr bitmap
            {
                get => (IntPtr)this.bitmap_;
                set => this.bitmap_ = (void*)value.ToPointer();
            }

            [FieldOffset(48)] private byte global_alpha_;
            public byte global_alpha
            {
                get => this.global_alpha_;
                set => this.global_alpha_ = (byte)value;
            }

        }
        public override NativeMethods_V4L2_Interop.v4l2_window Create_v4l2_window() => new v4l2_window();


    }
}

