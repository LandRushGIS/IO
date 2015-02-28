using System;

namespace LandRush.IO.DMF
{
	public class Pen
	{
		public enum PenStyle
		{
			Solid = 0,
			Dash = 1,
			Dot = 2,
			DashDot = 3,
			DashDotDot = 4,
			Null = 5,
			InsideFrame = 6,
			// UserStyle,
			// Alternate
		}

		public Pen(Color color, int width, PenStyle style)
		{
			this.Color = color;
			this.Width = width;
			this.Style = style;
		}

		public readonly Color Color;
		public readonly int Width;
		public readonly PenStyle Style;
	}
}
