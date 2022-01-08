using Gma.QrCodeNet.Encoding.Positioning.Stencils;

namespace Gma.QrCodeNet.Encoding.Positioning;

internal static class PositioningPatternBuilder
{
	internal static void EmbedBasicPatterns(int version, TriStateMatrix matrix)
	{
		new PositionDetectionPattern(version).ApplyTo(matrix);
		new DarkDotAtLeftBottom(version).ApplyTo(matrix);
		new AlignmentPattern(version).ApplyTo(matrix);
		new TimingPattern(version).ApplyTo(matrix);
	}
}
