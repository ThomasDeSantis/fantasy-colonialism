using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FantasyColonialismMapgen
{
    class FractalCoastline
    {
        SixLabors.ImageSharp.Image<Rgba32> image;

        int width;
        int height;

        MultifractalNoise noise;

        float turbulenceAmplitude;

        Rgba32 pointColor;
        Rgba32 oceanColor;

        string parentDirectory;

        int minLandmassSize;
        public FractalCoastline(string parentDirectoryI, string bmpPath, IConfiguration config)
        {
            image = SixLabors.ImageSharp.Image.Load<Rgba32>(parentDirectoryI + bmpPath);
            width = image.Width;
            height = image.Height;
            noise = new MultifractalNoise();

            pointColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapPoint"));
            oceanColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapOceanPoint"));

            turbulenceAmplitude = config.GetValue<float>("CoastlineGenerationSettings:Turbulence");
            minLandmassSize = config.GetValue<int>("CoastlineGenerationSettings:MinLandMassSize");

            parentDirectory = parentDirectoryI;
        }

        //Use multifractal noise with turbulence to shift points and give coastlines more texture
        public void generateFractalCoastlineWithTurbulence()
        {
            Image<Rgba32> output = new Image<Rgba32>(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Rgba32 baseColor = image[x, y];
                    byte baseBrightness = baseColor.B; //The image should be black/white so it doesnt matter which channel you look at

                    float noiseX = noise.GetNoise((float)x, (float)y);
                    float noiseY = noise.GetNoise((float)x, (float)y + 10000f); // Offset Y to avoid correlation in the noise

                    //Console.WriteLine(noiseX + " " + noiseY);

                    //Generate the offset for the turbulence
                    float dx = noiseX * turbulenceAmplitude;
                    float dy = noiseY * turbulenceAmplitude;


                    // Scale the offsets by turbulence amplitude.
                    int warpedX = x + (int)dx;
                    int warpedY = y + (int)dy;

                    //Console.WriteLine($"Original: ({x}, {y}), Warped: ({warpedX}, {warpedY})");

                    if (warpedX < 0 || warpedX >= width || warpedY < 0 || warpedY >= height)
                    {
                        output[x, y] = Color.Black;
                    }
                    else
                    {
                        output[x,y] = image[warpedX, warpedY];
                    }
                }
                
            }

            //Flood fill the map starting at 0,0 with ocean color, not crossing past the continental boundaries
            var stack = new Stack<(int x, int y)>();
            var visited = new bool[width, height];
            stack.Push((0, 0)); // Start from the top-left corner
            while (stack.Count > 0)
            {
                var (px, py) = stack.Pop();
                if (px >= 0 && px < width && py >= 0 && py < height && !visited[px, py] && !(output[px, py] == pointColor) && !(output[px, py] == oceanColor))
                {
                    output[px, py] = oceanColor;
                    visited[px, py] = true;
                    var neighbors = new (int x, int y)[]
                    {
                            (px, py - 1), // North
                            (px + 1, py), // East
                            (px, py + 1), // South
                            (px - 1, py)  // West
                    };
                    foreach (var (nx, ny) in neighbors)
                    {
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny] && !(output[nx, ny] == pointColor) && !(output[nx, ny] == oceanColor))
                        {
                            stack.Push((nx, ny));
                        }
                    }
                }
            }
            
            //Run through each landmass and count the number of pixels in each one.
            //If it is a single point surrounded by ocean, set it to ocean
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (output[x,y] == pointColor)
                    {
                        if (x > 0 && x < width - 1 && y > 0 && y < height - 1)
                        {
                            //Check the neighbors
                            int neighborCount = 0;
                            if (output[x - 1, y] == pointColor) neighborCount++;
                            if (output[x + 1, y] == pointColor) neighborCount++;
                            if (output[x, y - 1] == pointColor) neighborCount++;
                            if (output[x, y + 1] == pointColor) neighborCount++;
                            //If there are no neighbors, set it to ocean
                            if (neighborCount == 0)
                            {
                                output[x, y] = oceanColor;
                            }
                        }
                        else
                        {
                            output[x, y] = oceanColor; //Edge points are always ocean
                        }
                    }
                }
            }

            
            Rgba32 black = Color.Black;
            //Do one final run through and turn and black points to the point color
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (output[x, y] == black)
                    {
                        output[x, y] = pointColor;
                    }
                }
            }
            //Save the output image
            output.Save(parentDirectory + "coastline-turbulence.png");
        }

    }
}
