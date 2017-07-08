using Mandelbrot.Generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mandelbrot
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var height = 2048;
			short maxIterations = 1000;
			var scale = 2F;

			MandelbrotGenerator gen = new MandelbrotGenerator(height, maxIterations, scale);
			gen.Generate();
			gen.SaveImage("image.jpg");

			Console.Read();
		}
	}
}
