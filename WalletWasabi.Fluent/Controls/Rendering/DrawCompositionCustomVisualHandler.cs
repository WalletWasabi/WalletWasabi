using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;

namespace WalletWasabi.Fluent.Controls.Rendering;

internal class DrawCompositionCustomVisualHandler : CompositionCustomVisualHandler
{
	private bool _running;
	private IDrawHandler? _drawHandler;
	private Rect _bounds;
	private readonly object _sync = new();

	public override void OnMessage(object message)
	{
		if (message is not DrawPayload msg)
		{
			return;
		}

		switch (msg)
		{
			case
			{
				HandlerCommand: HandlerCommand.Start,
				Handler: { } handler,
				Bounds: var bounds,
			}:
			{
				_running = true;
				_drawHandler = handler;
				_bounds = bounds;
				RegisterForNextAnimationFrameUpdate();
				break;
			}
			case
			{
				HandlerCommand: HandlerCommand.Update,
			}:
			{
				RegisterForNextAnimationFrameUpdate();
				break;
			}
			case
			{
				HandlerCommand: HandlerCommand.Stop
			}:
			{
				_running = false;
				break;
			}
			case
			{
				HandlerCommand: HandlerCommand.Dispose
			}:
			{
				DisposeImpl();
				break;
			}
		}
	}

	private int _count;

	public override void OnAnimationFrameUpdate()
	{
		if (!_running)
		{
			return;
		}

		// TODO: This is fixed at 15 FPS for spectrum control.
		if (_count % 4 == 0)
		{
			Invalidate();
		}

		_count++;
		RegisterForNextAnimationFrameUpdate();
	}

	private void DisposeImpl()
	{
		lock (_sync)
		{
			_drawHandler?.Dispose();
		}
	}

	public override void OnRender(ImmediateDrawingContext context)
	{
		if (!_running)
		{
			return;
		}

		lock (_sync)
		{
			using var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
			if (lease is null)
			{
				return;
			}

			_drawHandler?.Draw(lease, _bounds);
		}
	}
}
