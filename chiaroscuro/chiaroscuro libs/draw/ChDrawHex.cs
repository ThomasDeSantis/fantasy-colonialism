using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapData;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ChGenerateHex
{
    public static class ChDrawHex
    {
        //Draws the bounds of a hex and saves it to the specified output path.
        public static string drawHexBounds(Hex hex, string hexBoundOutputPath)
        {
            (int,int) normalizedBottomRightBounds = hex.getNormalizedBounds();
            using (SixLabors.ImageSharp.Image<Rgba32> image = new SixLabors.ImageSharp.Image<Rgba32>(normalizedBottomRightBounds.Item1, normalizedBottomRightBounds.Item2))
            {
                for(int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        if(hex.containsPointNormalized((x, y)))
                        {
                            if (y % 2 == 0)
                            {
                                image[x, y] = Color.Blue;
                            }
                            else
                            {
                                image[x, y] = Color.Green;
                            }
                        }
                            
                    }
                }
                image.Save(hexBoundOutputPath);
                return hexBoundOutputPath;
            }
        }
    }
}
