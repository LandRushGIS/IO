namespace LandRush.IO.DMF
{
	internal struct Version
	{
		public Version(uint major, uint minor)
		{
			this.Major = major;
			this.Minor = minor;
		}

		public override string ToString()
		{
			return Major.ToString() + "." + Minor.ToString();
		}

		public readonly uint Major;
		public readonly uint Minor;
	}
}
