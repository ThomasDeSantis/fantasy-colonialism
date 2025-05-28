using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;
using Svg;
using SixLabors.ImageSharp.Drawing.Processing;
using Org.BouncyCastle.Crypto.Macs;
using System.Security.Cryptography.X509Certificates;

namespace FantasyColonialismMapgen
{
    class FractalCoastline
    {
        private Dictionary<Rgba32, int> colorToCoastlineRoughness;
        private Rgba32 nodeColor;
        private Rgba32 pointColor;
        private Rgba32 oceanColor;
        private Rgba32 backgroundColor;
        private SolidBrush brush;
        private DrawingOptions drawingOptions;
        private Random r;
        string parentDirectory;


        private List<FractalCoastlineSegment> segments = new List<FractalCoastlineSegment>();

        int width;
        int height;
        public FractalCoastline(string parentDirectoryI, IConfiguration config)
        {
            Console.WriteLine($"Begin processing config coast line roughnesses: {DateTime.UtcNow.ToString()}");
            colorToCoastlineRoughness = new Dictionary<Rgba32, int>();
            loadRoughnessCoastlinecolorMaps(config);
            Console.WriteLine($"End processing config coast line roughnesses: {DateTime.UtcNow.ToString()}");
            nodeColor = Rgba32.ParseHex(config.GetValue<string>("RoughnessCoastlineSettings:NodeColor"));
            pointColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapPoint"));
            oceanColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapOceanPoint"));
            width = config.GetValue<int>("ImageSettings:WorldWidth");
            height = config.GetValue<int>("ImageSettings:WorldHeight");
            brush = new SolidBrush(pointColor);
            r = new Random();

            //Disable antialiasing to maintain a full solid color brush
            drawingOptions = new DrawingOptions
            {
                GraphicsOptions = new GraphicsOptions
                {
                    Antialias = false
                }
            };

            backgroundColor = Rgba32.ParseHex("#FFFFFF");
            Console.WriteLine($"{colorToCoastlineRoughness.Keys.ToString()} roughness colors loaded from config.");
            foreach (var color in colorToCoastlineRoughness.Keys)
            {
                Console.WriteLine($"Color: {color}, Roughness: {colorToCoastlineRoughness[color]}");
            }
            Console.WriteLine($"Begin rendering coast node map without extra noise: {DateTime.UtcNow.ToString()}");
            renderBaseCoastNodeMap(parentDirectoryI);
            Console.WriteLine($"End rendering coast node map without extra noise: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin validating coast node map: {DateTime.UtcNow.ToString()}");
            bool isValid = validateCoastlineNodeMap(parentDirectoryI);
            Console.WriteLine($"End validating coast node map: {DateTime.UtcNow.ToString()}");

            if (isValid)
            {
                // Process the image into a list of FractalCoastlineSegment objects
                Console.WriteLine($"Begin processing image into FractalCoastlineSegment list: {DateTime.UtcNow.ToString()}");
                processImageIntoFractalCoastlineSegmentList(parentDirectoryI);
                Console.WriteLine($"End processing image into FractalCoastlineSegment list: {DateTime.UtcNow.ToString()}");

                // Render the segments to an image
                Console.WriteLine($"Begin rendering coastline segments to image: {DateTime.UtcNow.ToString()}");
                renderCoastlineSegments(parentDirectoryI, 0);
                Console.WriteLine($"End rendering coastline segments to image: {DateTime.UtcNow.ToString()}");
                for (int i = 1; i < 10; i++)
                {
                    Console.WriteLine($"Begin subdivision {i}: {DateTime.UtcNow.ToString()}");
                    subdivideSegments();
                    Console.WriteLine($"End subdivision {i}: {DateTime.UtcNow.ToString()}");
                    Console.WriteLine($"Begin rendering coastline segments to image #{i}: {DateTime.UtcNow.ToString()}");
                    renderCoastlineSegments(parentDirectoryI, i);
                    Console.WriteLine($"End rendering coastline segments to image: {DateTime.UtcNow.ToString()}");


                }
            }
        }

        //Iterate through the base image and render the image on a white background with only the relevant colors to fractal coastline generation
        private void renderBaseCoastNodeMap(string parentyDirectoryI)
        {
            string baseImagePath = System.IO.Path.Combine(parentyDirectoryI, "coastline-map-base.png");
            string outputImagePath = System.IO.Path.Combine(parentyDirectoryI, "coastline-map-filtered.png");

            if (!System.IO.File.Exists(baseImagePath))
            {
                throw new FileNotFoundException($"Base image not found at {baseImagePath}");
            }


            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(baseImagePath))
            {
                var width = image.Width - 1;
                var height = image.Height - 1;
                Console.WriteLine($"Image width:{width} Height: {height}");
                using (SixLabors.ImageSharp.Image<Rgba32> outputImage = new SixLabors.ImageSharp.Image<Rgba32>(width + 1, height + 1))
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            var pixel = image[x, y];
                            if (colorToCoastlineRoughness.ContainsKey(pixel) || pixel == nodeColor)
                            {
                                outputImage[x, y] = pixel; //If node or in roughness map, keep the pixel color
                            }
                            else
                            {
                                outputImage[x, y] = backgroundColor; // Set to ocean color if not in roughness map
                            }
                        }
                    }

                    outputImage.Save(outputImagePath); // Save the filtered image
                    Console.WriteLine($"Coast node map saved to {outputImagePath}");
                }
            }
        }



        private void loadRoughnessCoastlinecolorMaps(IConfiguration config)
        {
            colorToCoastlineRoughness = new Dictionary<Rgba32, int>();
            // Load the elevation color maps section from the configuration  
            var roughnessColorMapsSection = config.GetSection("RoughnessCoastlineColorMaps");
            if (roughnessColorMapsSection == null)
            {
                throw new Exception("RoughnessCoastlineColorMaps section not found in configuration.");
            }

            foreach (var section in roughnessColorMapsSection.GetChildren())
            {
                colorToCoastlineRoughness[Rgba32.ParseHex(section["color"])] = int.Parse(section["Roughness"]);
            }
        }

        //We want to make clear lines between each node, so we have to validate each non white pixel to ensure that it only touches exactly two other non white pixels within a square.
        //We also want to make sure no non-black pixel touches a pixel other than black or its own color.
        private bool validateCoastlineNodeMap(string parentDirectoryI)
        {
            bool allValid = true;
            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(parentDirectoryI + "coastline-map-filtered.png"))
            {
                var width = image.Width - 1;
                var height = image.Height - 1;
                Console.WriteLine($"Image width:{width} Height: {height}");
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var pixel = image[x, y];
                        if (colorToCoastlineRoughness.ContainsKey(pixel) || pixel == nodeColor)
                        {
                            bool pointValid = validatePointSquare((x, y), image);
                            if (!pointValid)
                            {
                                allValid = false;
                                Console.WriteLine($"Invalid point at ({x}, {y}) with color {pixel}. It does not meet the validation criteria.");
                            }
                        }
                    }
                }
            }
            return allValid;
        }

        //Validate a point in the image to ensure it only touches two other coastline node points in a square pattern
        //They must also either be a matching color, or a node. If it is a node, it can touch any other coastline point.
        private bool validatePointSquare((int x, int y) point, SixLabors.ImageSharp.Image<Rgba32> image)
        {
            var neighbors = Point.getNeighborsSquare(point);
            Rgba32 color = image[point.x, point.y];
            int count = 0;
            bool valid = false;
            for (int i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                //Do not check any out of bounds points
                if (neighbor.Item1 <= 0 || neighbor.Item1 > image.Width || neighbor.Item2 <= 0 || neighbor.Item2 > image.Height)
                {
                    continue; // Skip out of bounds neighbors
                }
                var neighborColor = image[neighbor.Item1, neighbor.Item2];
                if (neighborColor != backgroundColor)
                {
                    if (neighborColor == nodeColor || color == nodeColor || color == neighborColor)
                    {
                        count++;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            if (count != 2)
            {
                return false;
            }
            else
            {
                return true; // Valid point, only touches two other points
            }
        }

        private void processImageIntoFractalCoastlineSegmentList(string parentDirectoryI)
        {
            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(parentDirectoryI + "coastline-map-filtered.png"))
            {
                var width = image.Width - 1;
                var height = image.Height - 1;
                HashSet<(int, int)> processedPoints = new HashSet<(int, int)>();

                Console.WriteLine($"Image width:{width} Height: {height}");
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var pixel = image[x, y];
                        if (pixel == nodeColor && !processedPoints.Contains((x, y)))
                        {
                            traverseBMPPath(x, y, image, processedPoints);
                        }
                    }
                }
            }
        }

        //This travels a path on the BMP and writes each line as a segment in our list of coastline segments
        private void traverseBMPPath(int startX, int startY, SixLabors.ImageSharp.Image<Rgba32> image, HashSet<(int, int)> processedPoints)
        {
            (int x, int y) currentPoint = (startX, startY);
            (int x, int y) initialPoint = currentPoint;
            (int x, int y) lastNode = currentPoint;
            int initialProcessedPoints = processedPoints.Count;
            processedPoints.Add(currentPoint);
            do
            {
                var neighbors = Point.getNeighborsSquare(currentPoint);
                bool foundPoint = false;
                foreach (var neighbor in neighbors)
                {

                    var neighborColor = image[neighbor.Item1, neighbor.Item2];
                    //Check if its the next point on the path
                    if (neighborColor != backgroundColor && !processedPoints.Contains((neighbor.Item1, neighbor.Item2)))
                    {
                        foundPoint = true;
                        //If the next point on the path is out of bounds, throw an exception
                        //As a note, we want the border of the map to be ocean
                        if (neighbor.Item1 <= 0 || neighbor.Item1 >= image.Width - 1 || neighbor.Item2 <= 0 || neighbor.Item2 >= image.Height - 1)
                        {
                            throw new ArgumentOutOfRangeException($"Next point ({neighbor.Item1}, {neighbor.Item2}) is out of bounds of the image dimensions ({image.Width}, {image.Height}). As a note, this may include being on the border of the image.");
                        }
                        if (neighborColor == nodeColor)
                        {
                            //If the point we find is a node, then we create a fractal coastline segment.
                            segments.Add(new FractalCoastlineSegment(lastNode, neighbor, colorToCoastlineRoughness[image[currentPoint.x, currentPoint.y]]));
                            lastNode = neighbor; // Update lastNode to the current neighbor
                        }
                        currentPoint = neighbor;
                        processedPoints.Add(currentPoint);
                        break;
                    }
                    //Check if we reached our initial point
                    if (neighbor == initialPoint && lastNode != neighbor)
                    {
                        //If we reached our initial point, we can stop traversing
                        foundPoint = true;
                        segments.Add(new FractalCoastlineSegment(lastNode, neighbor, colorToCoastlineRoughness[image[currentPoint.x, currentPoint.y]]));
                        currentPoint = neighbor;
                        break;
                    }
                }
                if (!foundPoint)
                {
                    throw new Exception($"No valid neighbor found for point ({currentPoint.x}, {currentPoint.y}) in the image. This may indicate an issue with the image or the processing logic.");
                }
            } while (currentPoint != initialPoint);
            // Process the path as needed
            Console.WriteLine($"Found path of length {processedPoints.Count - initialProcessedPoints} starting at ({startX}, {startY})");
        }

        private void renderCoastlineSegments(string parentDirectoryI, int iteration)
        {
            // Create a new image with a white background
            using (var image = new SixLabors.ImageSharp.Image<Rgba32>(width, height))
            {
                image.Mutate(imageContext => imageContext.Fill(Color.Black));
                for (int i = 0; i < segments.Count; i++)
                {
                    (int x, int y) p1 = segments[i].getP1();
                    (int x, int y) p2 = segments[i].getP2();
                    PointF p1F = new PointF(p1.x, p1.y);
                    PointF p2F = new PointF(p2.x, p2.y);
                    image.Mutate(imageContext => imageContext.DrawLine(drawingOptions, brush, 1.5f, [p1F, p2F]));

                    //To account for node points, make points point colored at the node points
                    image[p1.x, p1.y] = pointColor; // Set the start point color to pointColor
                    image[p2.x, p2.y] = pointColor; // Set the end point color to pointColor
                }


                //Flood fill the map starting at 0,0 with ocean color, not crossing past the continental boundaries
                var stack = new Stack<(int x, int y)>();
                var visited = new bool[width, height];
                stack.Push((0, 0)); // Start from the top-left corner
                while (stack.Count > 0)
                {
                    var (px, py) = stack.Pop();
                    if (px >= 0 && px < width && py >= 0 && py < height && !visited[px, py] && !(image[px, py] == pointColor) && !(image[px, py] == oceanColor))
                    {
                        image[px, py] = oceanColor;
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
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[nx, ny] && !(image[nx, ny] == pointColor) && !(image[nx, ny] == oceanColor))
                            {
                                stack.Push((nx, ny));
                            }
                        }
                    }
                }

                //Finally replace all black pixels with white pixels
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (image[x, y].Equals(Color.Black))
                        {
                            image[x, y] = pointColor; // Replace black pixels with white
                        }
                    }
                }


                image.Save(parentDirectoryI + $"coastline-segments-iteration-{iteration}.png"); // Save the image
            }
        }

        private void subdivideSegments()
        {
            List<FractalCoastlineSegment> newSegments = new List<FractalCoastlineSegment>();
            for (int i = 0; i < segments.Count; i++)
            {
                var newSegment = segments[i].subdivide(r, width, height);
                if (newSegment != null)
                {
                    newSegments.Add(newSegment);
                }
                //Console.WriteLine(newSegments.Count);
            }
            segments.AddRange(newSegments);
        }
    }
}
