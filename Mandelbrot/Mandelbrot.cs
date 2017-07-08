using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

// Original CodeReview Question:
// https://codereview.stackexchange.com/questions/104171/multithreaded-mandelbrot-generator

// Interesting link about coloring:
// http://www.fractalforums.com/programming/newbie-how-to-map-colors-in-the-mandelbrot-set/

// Trusty Wikipedia:
// https://en.wikipedia.org/wiki/Mandelbrot_set

namespace Mandelbrot.Generator
{
	public class MandelbrotGenerator
	{
		// Readonly properties to be set in constructor
		public int Width { get; }
		public int Height { get; }
		public short MaxIterations { get; }
		public float ScaleFactor { get; }

		private short[] _iterationsPerPixel = null;
		private Point _center;
		private SizeF _scaleSize;
		private float _scaleSquared;

		public MandelbrotGenerator(int height, short maxIterations, float scaleFactor = 2.0F)
		{
			// Use some very basic level limit checking using some arbitrary (but practical) limits.
			const int heightLow = 512;
			const int heightHigh = 4096 * 2;
			const short iterationLow = 100;
			const short iterationHigh = 32000;
			const float scaleLow = 1.0F;
			const float scaleHigh = 8.0F;

			CheckLimits(nameof(height), height, heightLow, heightHigh);
			CheckLimits(nameof(maxIterations), maxIterations, iterationLow, iterationHigh);
			CheckLimits(nameof(scaleFactor), scaleFactor, scaleLow, scaleHigh);

			ScaleFactor = scaleFactor;
			Width = (int)(scaleFactor * height);
			Height = height;
			MaxIterations = maxIterations;

			_center = new Point(Width / 2, Height / 2);

			// And we'll scale the size so the brot sits within region [-ScaleFactor,ScaleFactor], 
			_scaleSquared = ScaleFactor * ScaleFactor;
			_scaleSize = new SizeF(_center.X / ScaleFactor, _center.Y);
		}

		private void CheckLimits(string name, double value, double inclusiveLow, double inclusiveHigh)
		{
			if (value < inclusiveLow || value > inclusiveHigh)
			{
				throw new ArgumentOutOfRangeException(name, $"Argument must be between {inclusiveLow} and {inclusiveHigh} inclusively.");
			}
		}

		public void Generate()
		{
			_iterationsPerPixel = new short[Width * Height];

			var sections = GetHoriztonalSections();

			Parallel.ForEach(sections, section =>
			{
				var data = GenerateSection(section);

				for (var y = section.Start.Y; y < section.End.Y; y++)
				{
					var brotOffset = y * Width;
					var dataOffset = (y - section.Start.Y) * Width;

					for (var x = 0; x < Width; x++)
					{
						_iterationsPerPixel[brotOffset + x] = data[dataOffset + x];
					}
				}
			});
		}

		public void SaveImage(string filename) => SaveImage(filename, ImageFormat.Png);

		public void SaveImage(string filename, ImageFormat imageFormat)
		{
			if (_iterationsPerPixel == null || _iterationsPerPixel.Length == 0)
			{
				throw new Exception("You must create the Mandelbrot data set before you can save the image to file.");
			}

			// Create our image.
			using (Bitmap image = new Bitmap(Width, Height))
			{
				for (var y = 0; y < Height; y++)
				{
					var brotOffset = y * Width;

					for (var x = 0; x < Width; x++)
					{
						image.SetPixel(x, y, LookupColor(_iterationsPerPixel[brotOffset + x]));
					}
				}

				image.Save(filename, imageFormat);
			}
		}

		// Coloring is probably has the greatest potential for further improvements.
		// This is just one attempt that suffices for now.
		private Color LookupColor(short iterations)
		{
			if (iterations >= MaxIterations)
			{
				return Color.Black;
			}
			if (iterations < 64)
			{
				return Color.FromArgb(255, iterations * 2, 0, 0);
			}
			if (iterations < 128)
			{
				return Color.FromArgb(255, (((iterations - 64) * 128) / 126) + 128, 0, 0);
			}
			if (iterations < 256)
			{
				return Color.FromArgb(255, (((iterations - 128) * 62) / 127) + 193, 0, 0);
			}
			if (iterations < 512)
			{
				return Color.FromArgb(255, 255, (((iterations - 256) * 62) / 255) + 1, 0);
			}
			if (iterations < 1024)
			{
				return Color.FromArgb(255, 255, (((iterations - 512) * 63) / 511) + 64, 0);
			}
			if (iterations < 2048)
			{
				return Color.FromArgb(255, 255, (((iterations - 1024) * 63) / 1023) + 128, 0);
			}
			if (iterations < 4096)
			{
				return Color.FromArgb(255, 255, (((iterations - 2048) * 63) / 2047) + 192, 0);
			}
			return Color.FromArgb(255, 255, 255, 0);
		}

		private struct Section
		{
			public Point Start { get; }
			public Point End { get; }

			// The way I create sections, End.Y will always be greater than Start.Y
			// but the math nerd in me insists on using Math.Abs() anyway.
			public int Height => Math.Abs(End.Y - Start.Y);
			public int Width => Math.Abs(End.X - Start.X);

			public Section(Point start, Point end)
			{
				Start = start;
				End = end;
			}
		}

		private Section[] GetHoriztonalSections()
		{
			var sections = new Section[2 * Environment.ProcessorCount];

			var heightPerSection = Height / sections.Length;

			if (Height % sections.Length > 0) { heightPerSection++; }

			for (var i = 0; i < sections.Length - 1; i++)
			{
				var startY = heightPerSection * i;
				sections[i] = new Section(new Point(0, startY), new Point(Width, startY + heightPerSection));
			}

			// SPECIAL TREATMENT FOR LAST SECTION:
			// The width is the same per section, namely the image's Width,
			// but the very last section's height could be different since 
			// it's upper rightmost point really should be clamped to the image's boundaries.
			{
				var lastIndex = sections.Length - 1;
				var startY = heightPerSection * lastIndex;
				sections[lastIndex] = new Section(new Point(0, startY), new Point(Width, Height));
			}

			return sections;
		}

		private short[] GenerateSection(Section section)
		{
			// The sectionWidth is the same value as Width but for some odd reason
			// using Width is noticeably faster on my 8-core PC.  This is true even
			// if I create a local copy such as:
			//      var sectionWidth = section.Width;
			var data = new short[section.Height * Width];

			for (var y = section.Start.Y; y < section.End.Y; y++)
			{
				var indexOffset = (y - section.Start.Y) * Width;
				var anchorY = (y - _center.Y) / _scaleSize.Height;

				for (var x = section.Start.X; x < section.End.X; x++)
				{
					// The formula for a mandelbrot is z = z^2 + c, basically. We must relate that in code.
					var anchorX = (x - _center.X) / _scaleSize.Width;

					short iteration;
					float xTemp = 0;
					float yTemp = 0;
					float xSquared = 0;
					float ySquared = 0;

					for (iteration = 0; iteration < MaxIterations; iteration++)
					{
						if (xSquared + ySquared >= _scaleSquared) { break; }
						// Important for yTemp to be calculated BEFORE xTemp
						// since yTemp depends on older value of xTemp.
						yTemp = 2 * xTemp * yTemp + anchorY;
						xTemp = xSquared - ySquared + anchorX;
						xSquared = xTemp * xTemp;
						ySquared = yTemp * yTemp;
					}

					data[indexOffset + x] = iteration;
				}
			}

			return data;
		}
	}
}