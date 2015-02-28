using System;

namespace LandRush.IO.DMF
{
	public struct Font
	{
		public Font(
			bool bold,
			bool italic,
			bool underLine,
			bool strikeOut,
			Color color,
			float size,
			byte charset,
			string name)
		{
			this.Bold = bold;
			this.Italic = italic;
			this.Underline = underLine;
			this.StrikeOut = strikeOut;
			this.Color = color;
			this.Size = size;
			this.CharSet = charset;
			this.Name = name;
		}

		public readonly bool Bold;
		public readonly bool Italic;
		public readonly bool Underline;
		public readonly bool StrikeOut;
		public readonly Color Color;
		public readonly float Size;
		public readonly byte CharSet;
		public readonly string Name;
	}
}