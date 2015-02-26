using System;

namespace LandRush.IO.DMF
{
	public class Brush
	{
		public enum BrushStyle
		{
			Solid = 0,
			Null = 1,
			Horizontal = 2,
			Vertical = 3,
			FDiagonal = 4,
			BDiagonal = 5,
			Cross = 6,
			DiagCross = 7
		}

		public Brush(Color color, BrushStyle style)
		{
			this.Color = color;
			this.Style = style;
		}

		public readonly Color Color;
		public readonly BrushStyle Style;
	}
}
