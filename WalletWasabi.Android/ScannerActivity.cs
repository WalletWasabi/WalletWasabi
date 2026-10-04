using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Stopwatch = System.Diagnostics.Stopwatch;
using WalletWasabi.Mobile;
using ZXing;
using ZXing.Common;

namespace WalletWasabi.Android;

[Activity(Name = "io.wasabiwallet.android.ScannerActivity", Theme = "@style/WasabiTheme", Exported = false)]
public sealed class ScannerActivity : Activity, TextureView.ISurfaceTextureListener
{
	private TextureView _preview = null!;
	private HandlerThread? _thread;
	private Handler? _handler;
	private CameraDevice? _camera;
	private CameraCaptureSession? _session;
	private ImageReader? _reader;
	private Surface? _surface;
	private DeviceStates? _deviceStates;
	private SessionStates? _sessionStates;
	private Frames? _frames;
	private CaptureRequest.Builder? _request;
	private volatile bool _resumed;
	private int _decoding;
	private int _sensorRotation;
	private int _width;
	private int _height;
	private long _lastFrame;
	private readonly object _cameraGate = new();
	private int _generation;

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		Window!.AddFlags(WindowManagerFlags.Secure);
		var root = new FrameLayout(this);
		root.SetFitsSystemWindows(true);
		root.SetBackgroundColor(Color.Rgb(17, 21, 18));
		_preview = new TextureView(this) { SurfaceTextureListener = this, ContentDescription = "Bitcoin QR camera preview" };
		root.AddView(_preview, new FrameLayout.LayoutParams(-1, -1));
		var caption = new TextView(this) { Text = "Scan payment request", TextSize = 20, Gravity = GravityFlags.Center };
		caption.SetTextColor(Color.White);
		caption.SetBackgroundColor(Color.Argb(190, 17, 21, 18));
		root.AddView(caption, new FrameLayout.LayoutParams(-1, (int)(72 * Resources!.DisplayMetrics!.Density), GravityFlags.Bottom));
		SetContentView(root);
	}

	protected override void OnResume()
	{
		base.OnResume();
		_resumed = true;
		_thread = new HandlerThread("WasabiCamera");
		_thread.Start();
		_handler = new Handler(_thread.Looper!);
		if (_preview.IsAvailable) { OpenCamera(); }
	}

	protected override void OnPause()
	{
		_resumed = false;
		ReleaseCamera();
		base.OnPause();
	}

	public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) => OpenCamera();
	public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) { ReleaseCamera(); return true; }
	public void OnSurfaceTextureUpdated(SurfaceTexture surface) { }
	public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) => TransformPreview();

	private void OpenCamera()
	{
		lock (_cameraGate)
		{
			if (!_resumed || _camera is not null || _deviceStates is not null || _handler is null || _preview.SurfaceTexture is null) { return; }
			try
			{
				var manager = (CameraManager)GetSystemService(CameraService)!;
				var identifier = manager.GetCameraIdList()!.FirstOrDefault(id =>
				{
					using var info = manager.GetCameraCharacteristics(id);
					using var facing = info.Get(CameraCharacteristics.LensFacing) as Java.Lang.Integer;
					return facing?.IntValue() == (int)LensFacing.Back;
				}) ?? manager.GetCameraIdList()!.First();
				using var characteristics = manager.GetCameraCharacteristics(identifier);
				using var orientation = characteristics.Get(CameraCharacteristics.SensorOrientation) as Java.Lang.Integer;
				_sensorRotation = orientation?.IntValue() ?? 0;
				using var map = characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap) as StreamConfigurationMap ?? throw new InvalidOperationException("Camera has no preview configuration.");
				using var previewClass = Java.Lang.Class.FromType(typeof(SurfaceTexture));
				var previewSizes = map.GetOutputSizes(previewClass) ?? [];
				var size = map.GetOutputSizes((int)ImageFormatType.Yuv420888)!.Where(s => s.Width <= 1280 && s.Height <= 1280
					&& previewSizes.Any(p => p.Width == s.Width && p.Height == s.Height)).OrderByDescending(s => s.Width * s.Height).FirstOrDefault()
				  ?? throw new InvalidOperationException("Camera has no bounded QR preview size.");
				_width = size.Width;
				_height = size.Height;
				_preview.SurfaceTexture.SetDefaultBufferSize(_width, _height);
				_surface = new Surface(_preview.SurfaceTexture);
				_reader = ImageReader.NewInstance(_width, _height, ImageFormatType.Yuv420888, 2)!;
				_frames = new Frames(this, _generation);
				_reader.SetOnImageAvailableListener(_frames, _handler);
				_deviceStates = new DeviceStates(this, characteristics, _generation);
				manager.OpenCamera(identifier, _deviceStates, _handler);
				TransformPreview();
			}
			catch (Exception) { CameraFailed(_generation); }
		}
	}

	private void StartPreview(CameraDevice camera, bool continuousFocus, int generation)
	{
		lock (_cameraGate)
		{
			if (!_resumed || generation != _generation || _reader is null || _surface is null) { camera.Close(); return; }
			try
			{
				_camera = camera;
				_request = camera.CreateCaptureRequest(CameraTemplate.Preview)!;
				_request.AddTarget(_surface);
				_request.AddTarget(_reader.Surface!);
				if (continuousFocus)
				{
					using var focus = Java.Lang.Integer.ValueOf((int)ControlAFMode.ContinuousPicture)!;
					_request.Set(CaptureRequest.ControlAfMode!, focus);
				}
				_sessionStates = new SessionStates(this, generation);
#pragma warning disable CS0618, CA1422
				camera.CreateCaptureSession(new[] { _surface, _reader.Surface! }, _sessionStates, _handler);
#pragma warning restore CS0618, CA1422
			}
			catch (Exception) { CameraFailed(generation); }
		}
	}

	private void TransformPreview()
	{
		if (_width == 0 || _preview.Width == 0) { return; }
#pragma warning disable CS0618, CA1422
		var displayDegrees = (int)(OperatingSystem.IsAndroidVersionAtLeast(30) ? Display!.Rotation : WindowManager!.DefaultDisplay!.Rotation) * 90;
#pragma warning restore CS0618, CA1422
		var rotation = CameraFrame.RelativeRotation(_sensorRotation, displayDegrees);
		var rotatedWidth = rotation is 90 or 270 ? _height : _width;
		var rotatedHeight = rotation is 90 or 270 ? _width : _height;
		using var matrix = new Matrix();
		var scale = Math.Max((float)_preview.Width / rotatedWidth, (float)_preview.Height / rotatedHeight);
		matrix.PostScale((float)_width / _preview.Width, (float)_height / _preview.Height, _preview.Width / 2f, _preview.Height / 2f);
		matrix.PostRotate(rotation, _preview.Width / 2f, _preview.Height / 2f);
		matrix.PostScale(scale, scale, _preview.Width / 2f, _preview.Height / 2f);
		_preview.SetTransform(matrix);
	}

	private void OnFrame(ImageReader reader, int generation)
	{
		byte[] pixels;
		int width, height;
		lock (_cameraGate)
		{
			if (!_resumed || generation != _generation || reader != _reader) { return; }
			// Acquire and copy while teardown is excluded. Queued callbacks may arrive
			// after Close(), so neither a stale reader nor a stale decoded frame wins.
			try
			{
				using var image = reader.AcquireLatestImage();
				if (image is null || Stopwatch.GetElapsedTime(_lastFrame) < TimeSpan.FromMilliseconds(200)
					|| Interlocked.CompareExchange(ref _decoding, 1, 0) != 0) { return; }
				_lastFrame = Stopwatch.GetTimestamp();
				var planes = image.GetPlanes()!;
				try
				{
					var plane = planes[0];
					using var buffer = plane.Buffer!;
					var raw = new byte[buffer.Remaining()];
					buffer.Get(raw);
					width = image.Width; height = image.Height;
					pixels = CameraFrame.Luminance(raw, width, height, plane.RowStride, plane.PixelStride);
				}
				finally { foreach (var plane in planes) { plane.Dispose(); } }
			}
			catch (Exception) { Interlocked.Exchange(ref _decoding, 0); return; }
		}
		_ = Task.Run(() =>
		{
			try
			{
				var decoder = new BarcodeReaderGeneric { AutoRotate = true, Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
				var result = decoder.Decode(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
				if (result?.Text is { Length: <= 4096 } text)
				{
					RunOnUiThread(() => { if (_resumed && generation == _generation && !IsFinishing && !IsDestroyed) { SetResult(global::Android.App.Result.Ok, new Intent().PutExtra("payment", text)); Finish(); } });
				}
			}
			catch (Exception) { }
			finally { Interlocked.Exchange(ref _decoding, 0); }
		});
	}

	private void CameraFailed(int generation) => RunOnUiThread(() => { if (_resumed && generation == _generation && !IsFinishing) { Toast.MakeText(this, "Camera unavailable", ToastLength.Long)!.Show(); Finish(); } });
	private void ReleaseCamera()
	{
		HandlerThread? thread;
		Handler? handler;
		lock (_cameraGate)
		{
			_generation++;
			_session?.Close(); _session?.Dispose(); _session = null;
			_camera?.Close(); _camera?.Dispose(); _camera = null;
			_reader?.SetOnImageAvailableListener(null, null); _reader?.Close(); _reader?.Dispose(); _reader = null;
			_surface?.Dispose(); _surface = null;
			_request?.Dispose(); _request = null;
			// Camera2 owns outstanding callbacks until closure completes. Let their
			// Java peers retire normally instead of disposing a callback still queued.
			_deviceStates = null; _sessionStates = null; _frames = null;
			thread = _thread; _thread = null;
			handler = _handler; _handler = null;
		}
		thread?.QuitSafely(); thread?.Join(1000); thread?.Dispose();
		handler?.Dispose();
	}

	private sealed class DeviceStates : CameraDevice.StateCallback
	{
		private readonly ScannerActivity _owner;
		private readonly bool _focus;
		private readonly int _generation;
		public DeviceStates(ScannerActivity owner, CameraCharacteristics info, int generation)
		{
			_owner = owner;
			_generation = generation;
			using var modes = info.Get(CameraCharacteristics.ControlAfAvailableModes)?.JavaCast<JavaArray<int>>();
			_focus = modes?.ToArray()?.Contains((int)ControlAFMode.ContinuousPicture) is true;
		}
		public override void OnOpened(CameraDevice camera) => _owner.StartPreview(camera, _focus, _generation);
		public override void OnDisconnected(CameraDevice camera) { camera.Close(); _owner.CameraFailed(_generation); }
		public override void OnError(CameraDevice camera, CameraError error) { camera.Close(); _owner.CameraFailed(_generation); }
	}
	private sealed class SessionStates(ScannerActivity owner, int generation) : CameraCaptureSession.StateCallback
	{
		public override void OnConfigured(CameraCaptureSession session)
		{
			lock (owner._cameraGate)
			{
				if (!owner._resumed || generation != owner._generation || owner._request is null) { session.Close(); return; }
				try
				{
					owner._session = session;
					using var request = owner._request.Build();
					session.SetRepeatingRequest(request!, null, owner._handler);
				}
				catch (Exception) { owner.CameraFailed(generation); }
			}
		}
		public override void OnConfigureFailed(CameraCaptureSession session) { session.Close(); owner.CameraFailed(generation); }
	}
	private sealed class Frames(ScannerActivity owner, int generation) : Java.Lang.Object, ImageReader.IOnImageAvailableListener
	{
		public void OnImageAvailable(ImageReader? reader) { if (reader is not null) { owner.OnFrame(reader, generation); } }
	}
}
