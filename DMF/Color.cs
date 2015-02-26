using System;

namespace LandRush.IO.DMF
{
	public struct Color
	{
		public Color(int value)
		{
			this.value = value;
		}

		public Color(byte r, byte g, byte b)
		{
			this.value = (0 | (Int32)b << 16 | (Int32)g << 8 | r);
		}

		public byte R
		{
			get { return (byte)(this.value & 0xFF); }
		}

		public byte G
		{
			get { return (byte)((this.value >> 8) & 0xFF); }
		}

		public byte B
		{
			get { return (byte)((this.value >> 16) & 0xFF); }
		}

		public override bool Equals(object obj)
		{
			return (obj is Color) && (((Color)obj).value == this.value);
		}

		public override int GetHashCode()
		{
			return value.GetHashCode();
		}

		public static bool operator ==(Color color1, Color color2)
		{
			return (color1.value == color2.value);
		}

		public static bool operator !=(Color color1, Color color2)
		{
			return !(color1.value == color2.value);
		}

		private Int32 value;
	}
}
