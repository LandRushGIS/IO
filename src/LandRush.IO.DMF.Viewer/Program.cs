using System;
using System.IO;

namespace LandRush.IO.DMF.Viewer
{
	class Program
	{
		static void Main(string[] args)
		{
			string fileName = Console.ReadLine();
			DMF.Reader.Read(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
		}
	}
}
