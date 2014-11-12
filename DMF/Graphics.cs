namespace LandRush.IO.DMF
{
	public struct Pen
	{
		public Pen(int color, int width, byte style)
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
		public Brush(int color, byte style)
		{
			this.Color = color;
			this.Style = style;
		}

		public readonly int Color;
		public readonly byte Style;
	}

	public struct Font
	{
		public Font(
			int color,
			int size,
			byte style,
			string name)
		{
			this.Color = color;
			this.Size = size;
			this.Style = style;
			this.Name = name;
		}

		public readonly int Color;
		public readonly int Size;
		public readonly byte Style;
		public readonly string Name;
	}
}