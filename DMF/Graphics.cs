namespace LandRush.IO.DMF
{
	public struct Pen
	{
		public Pen(int color = 0, int width = 0, byte style = 0)
		{
			this.Color = color;
			this.Width = width;
			this.Style = style;
		}

		public readonly int Color;
		public readonly int Width;
		public readonly byte Style;
	}

	public struct Brush
	{
		public Brush(int color = 0, byte style = 0)
		{
			this.Color = color;
			this.Style = style;
		}

		public readonly int Color;
		public readonly byte Style;
	}

	public struct Font
	{
		public Font(int color = 0, int size = 0, byte style = 0)
		{
			this.Color = color;
			this.Size = size;
			this.Style = style;
		}

		public readonly int Color;
		public readonly int Size;
		public readonly byte Style;
	}
}