// This is auto generated code by FlashCap.V4L2Generator [0.14.6]. Do not edit.
// Linux version 5.13.0-39-generic (buildd@lcy02-amd64-080) (gcc (Ubuntu 9.4.0-1ubuntu1~20.04.1) 9.4.0, GNU ld (GNU Binutils for Ubuntu) 2.34) #44~20.04.1-Ubuntu SMP Thu Mar 24 16:43:35 UTC 2022
// Fri, 15 Apr 2022 03:57:16 GMT

using System;
using System.Runtime.InteropServices;

namespace FlashCap.Internal.V4L2
{
    internal abstract partial class NativeMethods_V4L2_Interop
    {
        // Common
        public abstract string Label { get; }
        public abstract string Architecture { get; }
        public abstract int sizeof_size_t { get; }
        public abstract int sizeof_off_t { get; }

        // Definitions
        public virtual uint V4L2_CAP_VIDEO_CAPTURE => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_ABGR32 => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_ARGB32 => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_JPEG => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_MJPEG => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_RGB24 => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_RGB332 => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_RGB565 => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_RGB565X => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_UYVY => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_XRGB32 => throw new NotImplementedException();
        public virtual uint V4L2_PIX_FMT_YUYV => throw new NotImplementedException();
        public virtual uint VIDIOC_DQBUF => throw new NotImplementedException();
        public virtual uint VIDIOC_ENUM_FMT => throw new NotImplementedException();
        public virtual uint VIDIOC_ENUM_FRAMEINTERVALS => throw new NotImplementedException();
        public virtual uint VIDIOC_ENUM_FRAMESIZES => throw new NotImplementedException();
        public virtual uint VIDIOC_QBUF => throw new NotImplementedException();
        public virtual uint VIDIOC_QUERYBUF => throw new NotImplementedException();
        public virtual uint VIDIOC_QUERYCAP => throw new NotImplementedException();
        public virtual uint VIDIOC_REQBUFS => throw new NotImplementedException();
        public virtual uint VIDIOC_S_FMT => throw new NotImplementedException();
        public virtual uint VIDIOC_STREAMOFF => throw new NotImplementedException();
        public virtual uint VIDIOC_STREAMON => throw new NotImplementedException();

        // Enums
        public enum v4l2_buf_type
        {
            VIDEO_CAPTURE = 1,
            VIDEO_OUTPUT = 2,
            VIDEO_OVERLAY = 3,
            VBI_CAPTURE = 4,
            VBI_OUTPUT = 5,
            SLICED_VBI_CAPTURE = 6,
            SLICED_VBI_OUTPUT = 7,
            VIDEO_OUTPUT_OVERLAY = 8,
            VIDEO_CAPTURE_MPLANE = 9,
            VIDEO_OUTPUT_MPLANE = 10,
            SDR_CAPTURE = 11,
            SDR_OUTPUT = 12,
            META_CAPTURE = 13,
            META_OUTPUT = 14,
            PRIVATE = 128,
        }

        public enum v4l2_field
        {
            ANY = 0,
            NONE = 1,
            TOP = 2,
            BOTTOM = 3,
            INTERLACED = 4,
            SEQ_TB = 5,
            SEQ_BT = 6,
            ALTERNATE = 7,
            INTERLACED_TB = 8,
            INTERLACED_BT = 9,
        }

        public enum v4l2_frmivaltypes
        {
            DISCRETE = 1,
            CONTINUOUS = 2,
            STEPWISE = 3,
        }

        public enum v4l2_frmsizetypes
        {
            DISCRETE = 1,
            CONTINUOUS = 2,
            STEPWISE = 3,
        }

        public enum v4l2_memory
        {
            MMAP = 1,
            USERPTR = 2,
            OVERLAY = 3,
            DMABUF = 4,
        }


        // Structures
        public interface timespec
        {
            IntPtr tv_sec
            {
                get;
                set;
            }

            IntPtr tv_nsec
            {
                get;
                set;
            }

        }
        public virtual timespec Create_timespec() => throw new NotImplementedException();

        public interface timeval
        {
            IntPtr tv_sec
            {
                get;
                set;
            }

            IntPtr tv_usec
            {
                get;
                set;
            }

        }
        public virtual timeval Create_timeval() => throw new NotImplementedException();

        public interface v4l2_buffer
        {
            uint index
            {
                get;
                set;
            }

            uint type
            {
                get;
                set;
            }

            uint bytesused
            {
                get;
                set;
            }

            uint flags
            {
                get;
                set;
            }

            uint field
            {
                get;
                set;
            }

            timeval timestamp
            {
                get;
                set;
            }

            v4l2_timecode timecode
            {
                get;
                set;
            }

            uint sequence
            {
                get;
                set;
            }

            uint memory
            {
                get;
                set;
            }

            uint m_offset
            {
                get;
                set;
            }

            UIntPtr m_userptr
            {
                get;
                set;
            }

            IntPtr m_planes
            {
                get;
                set;
            }

            int m_fd
            {
                get;
                set;
            }

            uint length
            {
                get;
                set;
            }

            uint reserved2
            {
                get;
                set;
            }

            int request_fd
            {
                get;
                set;
            }

            uint reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_buffer Create_v4l2_buffer() => throw new NotImplementedException();

        public interface v4l2_capability
        {
            byte[] driver
            {
                get;
                set;
            }

            byte[] card
            {
                get;
                set;
            }

            byte[] bus_info
            {
                get;
                set;
            }

            uint version
            {
                get;
                set;
            }

            uint capabilities
            {
                get;
                set;
            }

            uint device_caps
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_capability Create_v4l2_capability() => throw new NotImplementedException();

        public interface v4l2_clip
        {
            v4l2_rect c
            {
                get;
                set;
            }

            IntPtr next
            {
                get;
                set;
            }

        }
        public virtual v4l2_clip Create_v4l2_clip() => throw new NotImplementedException();

        public interface v4l2_fmtdesc
        {
            uint index
            {
                get;
                set;
            }

            uint type
            {
                get;
                set;
            }

            uint flags
            {
                get;
                set;
            }

            byte[] description
            {
                get;
                set;
            }

            uint pixelformat
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_fmtdesc Create_v4l2_fmtdesc() => throw new NotImplementedException();

        public interface v4l2_format
        {
            uint type
            {
                get;
                set;
            }

            v4l2_pix_format fmt_pix
            {
                get;
                set;
            }

            v4l2_pix_format_mplane fmt_pix_mp
            {
                get;
                set;
            }

            v4l2_window fmt_win
            {
                get;
                set;
            }

            v4l2_vbi_format fmt_vbi
            {
                get;
                set;
            }

            v4l2_sliced_vbi_format fmt_sliced
            {
                get;
                set;
            }

            v4l2_sdr_format fmt_sdr
            {
                get;
                set;
            }

            v4l2_meta_format fmt_meta
            {
                get;
                set;
            }

            byte[] fmt_raw_data
            {
                get;
                set;
            }

        }
        public virtual v4l2_format Create_v4l2_format() => throw new NotImplementedException();

        public interface v4l2_fract
        {
            uint numerator
            {
                get;
                set;
            }

            uint denominator
            {
                get;
                set;
            }

        }
        public virtual v4l2_fract Create_v4l2_fract() => throw new NotImplementedException();

        public interface v4l2_frmival_stepwise
        {
            v4l2_fract min
            {
                get;
                set;
            }

            v4l2_fract max
            {
                get;
                set;
            }

            v4l2_fract step
            {
                get;
                set;
            }

        }
        public virtual v4l2_frmival_stepwise Create_v4l2_frmival_stepwise() => throw new NotImplementedException();

        public interface v4l2_frmivalenum
        {
            uint index
            {
                get;
                set;
            }

            uint pixel_format
            {
                get;
                set;
            }

            uint width
            {
                get;
                set;
            }

            uint height
            {
                get;
                set;
            }

            uint type
            {
                get;
                set;
            }

            v4l2_fract discrete
            {
                get;
                set;
            }

            v4l2_frmival_stepwise stepwise
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_frmivalenum Create_v4l2_frmivalenum() => throw new NotImplementedException();

        public interface v4l2_frmsize_discrete
        {
            uint width
            {
                get;
                set;
            }

            uint height
            {
                get;
                set;
            }

        }
        public virtual v4l2_frmsize_discrete Create_v4l2_frmsize_discrete() => throw new NotImplementedException();

        public interface v4l2_frmsize_stepwise
        {
            uint min_width
            {
                get;
                set;
            }

            uint max_width
            {
                get;
                set;
            }

            uint step_width
            {
                get;
                set;
            }

            uint min_height
            {
                get;
                set;
            }

            uint max_height
            {
                get;
                set;
            }

            uint step_height
            {
                get;
                set;
            }

        }
        public virtual v4l2_frmsize_stepwise Create_v4l2_frmsize_stepwise() => throw new NotImplementedException();

        public interface v4l2_frmsizeenum
        {
            uint index
            {
                get;
                set;
            }

            uint pixel_format
            {
                get;
                set;
            }

            uint type
            {
                get;
                set;
            }

            v4l2_frmsize_discrete discrete
            {
                get;
                set;
            }

            v4l2_frmsize_stepwise stepwise
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_frmsizeenum Create_v4l2_frmsizeenum() => throw new NotImplementedException();

        public interface v4l2_meta_format
        {
            uint dataformat
            {
                get;
                set;
            }

            uint buffersize
            {
                get;
                set;
            }

        }
        public virtual v4l2_meta_format Create_v4l2_meta_format() => throw new NotImplementedException();

        public interface v4l2_pix_format
        {
            uint width
            {
                get;
                set;
            }

            uint height
            {
                get;
                set;
            }

            uint pixelformat
            {
                get;
                set;
            }

            uint field
            {
                get;
                set;
            }

            uint bytesperline
            {
                get;
                set;
            }

            uint sizeimage
            {
                get;
                set;
            }

            uint colorspace
            {
                get;
                set;
            }

            uint priv
            {
                get;
                set;
            }

            uint flags
            {
                get;
                set;
            }

            uint ycbcr_enc
            {
                get;
                set;
            }

            uint hsv_enc
            {
                get;
                set;
            }

            uint quantization
            {
                get;
                set;
            }

            uint xfer_func
            {
                get;
                set;
            }

        }
        public virtual v4l2_pix_format Create_v4l2_pix_format() => throw new NotImplementedException();

        public interface v4l2_pix_format_mplane
        {
            uint width
            {
                get;
                set;
            }

            uint height
            {
                get;
                set;
            }

            uint pixelformat
            {
                get;
                set;
            }

            uint field
            {
                get;
                set;
            }

            uint colorspace
            {
                get;
                set;
            }

            v4l2_plane_pix_format[] plane_fmt
            {
                get;
                set;
            }

            byte num_planes
            {
                get;
                set;
            }

            byte flags
            {
                get;
                set;
            }

            byte ycbcr_enc
            {
                get;
                set;
            }

            byte hsv_enc
            {
                get;
                set;
            }

            byte quantization
            {
                get;
                set;
            }

            byte xfer_func
            {
                get;
                set;
            }

            byte[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_pix_format_mplane Create_v4l2_pix_format_mplane() => throw new NotImplementedException();

        public interface v4l2_plane
        {
            uint bytesused
            {
                get;
                set;
            }

            uint length
            {
                get;
                set;
            }

            uint m_mem_offset
            {
                get;
                set;
            }

            UIntPtr m_userptr
            {
                get;
                set;
            }

            int m_fd
            {
                get;
                set;
            }

            uint data_offset
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_plane Create_v4l2_plane() => throw new NotImplementedException();

        public interface v4l2_plane_pix_format
        {
            uint sizeimage
            {
                get;
                set;
            }

            uint bytesperline
            {
                get;
                set;
            }

            ushort[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_plane_pix_format Create_v4l2_plane_pix_format() => throw new NotImplementedException();

        public interface v4l2_rect
        {
            int left
            {
                get;
                set;
            }

            int top
            {
                get;
                set;
            }

            uint width
            {
                get;
                set;
            }

            uint height
            {
                get;
                set;
            }

        }
        public virtual v4l2_rect Create_v4l2_rect() => throw new NotImplementedException();

        public interface v4l2_requestbuffers
        {
            uint count
            {
                get;
                set;
            }

            uint type
            {
                get;
                set;
            }

            uint memory
            {
                get;
                set;
            }

            uint capabilities
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_requestbuffers Create_v4l2_requestbuffers() => throw new NotImplementedException();

        public interface v4l2_sdr_format
        {
            uint pixelformat
            {
                get;
                set;
            }

            uint buffersize
            {
                get;
                set;
            }

            byte[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_sdr_format Create_v4l2_sdr_format() => throw new NotImplementedException();

        public interface v4l2_sliced_vbi_format
        {
            ushort service_set
            {
                get;
                set;
            }

            ushort[][] service_lines
            {
                get;
                set;
            }

            uint io_size
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_sliced_vbi_format Create_v4l2_sliced_vbi_format() => throw new NotImplementedException();

        public interface v4l2_timecode
        {
            uint type
            {
                get;
                set;
            }

            uint flags
            {
                get;
                set;
            }

            byte frames
            {
                get;
                set;
            }

            byte seconds
            {
                get;
                set;
            }

            byte minutes
            {
                get;
                set;
            }

            byte hours
            {
                get;
                set;
            }

            byte[] userbits
            {
                get;
                set;
            }

        }
        public virtual v4l2_timecode Create_v4l2_timecode() => throw new NotImplementedException();

        public interface v4l2_vbi_format
        {
            uint sampling_rate
            {
                get;
                set;
            }

            uint offset
            {
                get;
                set;
            }

            uint samples_per_line
            {
                get;
                set;
            }

            uint sample_format
            {
                get;
                set;
            }

            int[] start
            {
                get;
                set;
            }

            uint[] count
            {
                get;
                set;
            }

            uint flags
            {
                get;
                set;
            }

            uint[] reserved
            {
                get;
                set;
            }

        }
        public virtual v4l2_vbi_format Create_v4l2_vbi_format() => throw new NotImplementedException();

        public interface v4l2_window
        {
            v4l2_rect w
            {
                get;
                set;
            }

            uint field
            {
                get;
                set;
            }

            uint chromakey
            {
                get;
                set;
            }

            IntPtr clips
            {
                get;
                set;
            }

            uint clipcount
            {
                get;
                set;
            }

            IntPtr bitmap
            {
                get;
                set;
            }

            byte global_alpha
            {
                get;
                set;
            }

        }
        public virtual v4l2_window Create_v4l2_window() => throw new NotImplementedException();


    }
}

