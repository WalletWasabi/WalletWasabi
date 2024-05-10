using Avalonia;

namespace WalletWasabi.Fluent.Controls.Rendering;

internal record struct DrawPayload(
	HandlerCommand HandlerCommand,
	IDrawHandler? Handler = null,
	Rect Bounds = default);
