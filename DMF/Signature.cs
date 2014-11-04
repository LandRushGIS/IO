namespace LandRush.IO.DMF
{
	internal struct Signature
	{
		public Signature(Version version, bool isCompressed)
		{
			this.Version = version;
			this.IsCompressed = isCompressed;
		}

		public readonly Version Version;
		public readonly bool IsCompressed;
	}
}
