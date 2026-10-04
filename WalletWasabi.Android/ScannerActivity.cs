using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using ZXing;
using ZXing.Common;
using Camera = Android.Hardware.Camera;

namespace WalletWasabi.Android;

// The legacy camera API provides an NV21 preview on the full Android 24+ support range.
#pragma warning disable CS0618
[Activity(Name = "io.wasabiwallet.android.ScannerActivity", Theme = "@style/WasabiTheme", Exported = false)]
public sealed class ScannerActivity : Activity, TextureView.ISurfaceTextureListener, Camera.IPreviewCallback
{
	private Camera? _camera;
	private TextureView _preview = null!;
	private int _decoding;
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		Window!.AddFlags(WindowManagerFlags.Secure);
		var root = new FrameLayout(this);
		root.SetFitsSystemWindows(true);
		_preview = new TextureView(this) { SurfaceTextureListener = this };
		root.AddView(_preview, new FrameLayout.LayoutParams(-1, -1));
		var caption = new TextView(this) { Text = "Scan a Bitcoin payment QR code", TextSize = 20, Gravity = GravityFlags.Center };
		caption.SetTextColor(Color.White);
		caption.SetBackgroundColor(Color.Argb(170, 17, 21, 18));
		root.AddView(caption, new FrameLayout.LayoutParams(-1, 140, GravityFlags.Bottom));
		SetContentView(root);
	}
	public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
	{
		if (_camera is not null) { return; }
		try
		{
			_camera = Camera.Open();
			var parameters = _camera!.GetParameters()!;
			var size = parameters.SupportedPreviewSizes!.Where(s => s.Width <= 1280).OrderByDescending(s => s.Width).FirstOrDefault() ?? parameters.PreviewSize!;
			parameters.SetPreviewSize(size.Width, size.Height);
			if (parameters.SupportedFocusModes!.Contains(Camera.Parameters.FocusModeContinuousPicture!)) { parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture; }
			_camera.SetParameters(parameters);
			_camera.SetDisplayOrientation(90);
			_camera.SetPreviewTexture(surface);
			_camera.SetPreviewCallback(this);
			_camera.StartPreview();
		}
		catch (Exception)
		{
			Toast.MakeText(this, "Could not open the camera", ToastLength.Long)!.Show();
			Finish();
		}
	}
	public void OnPreviewFrame(byte[]? data, Camera? camera)
	{
		if (data is null || camera is null || Interlocked.Exchange(ref _decoding, 1) != 0) { return; }
		var size = camera.GetParameters()?.PreviewSize;
		if (size is null || data.Length < size.Width * size.Height) { Interlocked.Exchange(ref _decoding, 0); return; }
		var pixels = data[..(size.Width * size.Height)];
		_ = Task.Run(() =>
		{
			try
			{
				var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
				var result = reader.Decode(pixels, size.Width, size.Height, RGBLuminanceSource.BitmapFormat.Gray8);
				if (result?.Text is { Length: <= 4096 } text)
				{
					RunOnUiThread(() => { if (!IsFinishing && !IsDestroyed) { SetResult(global::Android.App.Result.Ok, new Intent().PutExtra("payment", text)); Finish(); } });
				return;
				}
			}
			catch (Exception) { /* A malformed preview frame must not end scanning. */ }
			finally { Interlocked.Exchange(ref _decoding, 0); }
		});
	}
	public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) { ReleaseCamera(); return true; }
	public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) { }
	public void OnSurfaceTextureUpdated(SurfaceTexture surface) { }
	protected override void OnPause() { ReleaseCamera(); base.OnPause(); }
	protected override void OnResume()
	{
		base.OnResume();
		if (_preview.IsAvailable && _preview.SurfaceTexture is { } surface) { OnSurfaceTextureAvailable(surface, _preview.Width, _preview.Height); }
	}
	private void ReleaseCamera()
	{
		_camera?.SetPreviewCallback(null);
		_camera?.StopPreview();
		_camera?.Release();
		_camera?.Dispose();
		_camera = null;
	}
}
#pragma warning restore CS0618
