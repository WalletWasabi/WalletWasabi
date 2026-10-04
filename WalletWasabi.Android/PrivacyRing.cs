using Android.Content;
using Android.Graphics;
using Android.Views;

namespace WalletWasabi.Android;

internal sealed class PrivacyRing(Context context) : View(context)
{
	public int Progress { get; set { field = Math.Clamp(value, 0, 100); Invalidate(); } }
	protected override void OnDraw(Canvas canvas)
	{
		base.OnDraw(canvas);
		var diameter = Math.Min(Width, Height) * 0.72f;
		var left = (Width - diameter) / 2;
		var top = (Height - diameter) / 2;
		using var bounds = new RectF(left, top, left + diameter, top + diameter);
		using var paint = new Paint(PaintFlags.AntiAlias) { Color = Color.Rgb(50, 63, 53), StrokeWidth = diameter * 0.09f };
		paint.SetStyle(Paint.Style.Stroke);
		canvas.DrawOval(bounds, paint);
		paint.Color = Color.Rgb(163, 230, 53);
		paint.StrokeCap = Paint.Cap.Round;
		canvas.DrawArc(bounds, -90, Progress * 3.6f, false, paint);
		paint.SetStyle(Paint.Style.Fill);
		paint.TextSize = diameter * 0.25f;
		paint.TextAlign = Paint.Align.Center;
		canvas.DrawText($"{Progress}%", Width / 2f, Height / 2f - (paint.Ascent() + paint.Descent()) / 2f, paint);
	}
}
