using Avalonia;
using WalletWasabi.Fluent.Controls.Spectrum;

namespace WalletWasabi.Fluent.Controls.Rendering;

internal record struct DrawPayload(
	HandlerCommand HandlerCommand,
	IDrawHandler? Handler = null,
	Rect Bounds = default);
