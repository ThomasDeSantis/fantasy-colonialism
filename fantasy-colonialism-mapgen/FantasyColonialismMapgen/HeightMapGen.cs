using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using LibNoise;
using LibNoise.Primitive;
using LibNoise.Filter;

namespace FantasyColonialismMapgen
{
    class HeightMapGen
    {
        public static void generateHeightMap(DBConnection db,IConfiguration config, string parentDirectory, string roughnessMap)
        {
            /*
            Console.WriteLine("Begin setting coastal provinces: " + DateTime.UtcNow.ToString());
            setCoastalPoints(db);
            Console.WriteLine("End setting coastal provinces: " + DateTime.UtcNow.ToString());
            */
            

            roughnessColorMap[] roughnessColorMaps = loadRoughnessColorMaps(config);
            int width = config.GetValue<int>("ImageSettings:WorldWidth");
            int height = config.GetValue<int>("ImageSettings:WorldHeight");
            int heightSoftCap = config.GetValue<int>("HeightGenerationSettings:SoftHeightLimit");
            Console.WriteLine($"Width: {width} Height: {height}");
            Console.WriteLine($"Number of roughness color maps: {roughnessColorMaps.Length}");
            // Output each member of the array  
            foreach (roughnessColorMap roughness in roughnessColorMaps)
            {
                Console.WriteLine("Roughness: " + roughness.roughness);
                Console.WriteLine("BMP color: " + roughness.bmpColor);
            }
            Console.WriteLine("Height map load began: " + DateTime.UtcNow.ToString());
            pointHeight[][] heightMap = loadHeightMap(db, width, height,true);
            Console.WriteLine("Height map load ended: " + DateTime.UtcNow.ToString());

            Console.WriteLine("Begin applying roughness: " + DateTime.UtcNow.ToString());
            applyRoughness(heightMap, roughnessColorMaps, parentDirectory + roughnessMap);
            Console.WriteLine("Finished applying roughness: " + DateTime.UtcNow.ToString());

            Console.WriteLine("Begin reassigning typeless points: " + DateTime.UtcNow.ToString());
            reassignTypelessHeightPoints(heightMap, width, height);
            Console.WriteLine("Finished reassigning typeless points: " + DateTime.UtcNow.ToString());

            Console.WriteLine("Begin correcting coastal heights: " + DateTime.UtcNow.ToString());
            correctCoastalHeights(heightMap, width, height, 1, 60, 100, 300);
            Console.WriteLine("Finished correcting coastal heights: " + DateTime.UtcNow.ToString());

            Console.WriteLine("Begin generating height: " + DateTime.UtcNow.ToString());
            generateHeightMapValues(heightMap, roughnessColorMaps,config, heightSoftCap);   
            Console.WriteLine("Finished generating height: " + DateTime.UtcNow.ToString());
            

            int max = getMaxHeight(heightMap);
            int min = getMinHeight(heightMap);
            Console.WriteLine(max);
            renderHeightmap(heightMap, parentDirectory + "\\Maps\\continent-heightmap-render.png", height, width,max,min);

            writeElevationsToDb(db, heightMap);
            //smoothenHeightMap(heightMap, width, height);
            //renderHeightmap(heightMap, parentDirectory + "\\sf-continent-heightmap-render-s1.png", height, width, max);
            //int min = getMinHeight(roughnessColorMaps);
            //Console.WriteLine("Min height: " + min + "Max height: " + max);
        }

        private static void setCoastalPoints(DBConnection db)
        {
            // Create a dictionary to store coastal points
            var points = new Dictionary<(int x, int y), (bool,int)>();

            // Query all points from the database
            string query = "SELECT x, y, land, id FROM WorldPoints;";
            var command = new MySqlCommand(query, db.Connection);
            MySqlDataReader reader = command.ExecuteReader();

            bool first = true;

            // Populate the dictionary with x, y as key and land status as value
            while (reader.Read())
            {
                int x = reader.GetInt32(0);
                int y = reader.GetInt32(1);
                bool isLand = reader.GetBoolean(2);
                int id = reader.GetInt32(3);
                points[(x, y)] = (isLand,id);
            }
            reader.Close();

            List<string> batchCoastalUpdateRow = new List<string>();

            // Iterate through the dictionary to identify coastal points
            foreach (var point in points.Keys.ToList())
            {
                if (points[point].Item1)
                {
                    // Check neighboring points to determine if the current point is coastal
                    var neighbors = Point.getNeighborsSquare((point.x, point.y));

                    foreach (var neighbor in neighbors)
                    {
                        if (points.ContainsKey(neighbor) && !points[neighbor].Item1)
                        {
                            batchCoastalUpdateRow.Add(string.Format("UPDATE WorldPoints SET coastal = true WHERE id = {0}", MySqlHelper.EscapeString(points[point].Item2.ToString())));
                            break;
                        }
                    }
                }
            }

            db.runStringNonQueryCommandBatch("","", batchCoastalUpdateRow, 1000,';', true);
        }

        public static void renderCoastline(DBConnection db, string coastLineMapPath, int height, int width)
        {
            string query = "SELECT x, y, coastal, land FROM WorldPoints";
            var command = new MySqlCommand(query, db.Connection);
            MySqlDataReader reader = command.ExecuteReader();
            // Create a new image with the specified dimensions
            using (SixLabors.ImageSharp.Image<Rgba32> image = new SixLabors.ImageSharp.Image<Rgba32>(width, height))
            {
                while (reader.Read())
                {
                    int x = reader.GetInt32(0);
                    int y = reader.GetInt32(1);
                    bool isCoastal = reader.GetBoolean(2);
                    bool isLand = reader.GetBoolean(3);
                    if (!isLand)
                    {
                        image[x,y] = Rgba32.ParseHex("#0000FF"); // Water color
                    }
                    else
                    {
                        // Set the pixel color based on the coastal status
                        if (isCoastal)
                        {
                            image[x, y] = Rgba32.ParseHex("#FF0000"); // Coastal color
                        }
                        else
                        {
                            image[x, y] = Rgba32.ParseHex("#FFFFFF"); // Non-coastal color
                        }
                    }
                }

                reader.Close();
                image.Save(coastLineMapPath);
            }
        }

        private static pointHeight[][] loadHeightMap(DBConnection db, int width, int height, bool defaultInit)
        {

            // Load the height map from the database
            var heightMap = new pointHeight[height][];
            for (int i = 0; i < height; i++)
            {
                heightMap[i] = new pointHeight[width];
                for (int j = 0; j < width; j++)
                {
                    heightMap[i][j] = new pointHeight
                    {
                        height = -1, // Default initialization
                        locked = false,
                        water = true,
                        coastal = false,
                        roughness = 0,
                        y = i,
                        x = j,
                        type = "land" // Default type
                    };
                }
            }

            //Query all points sorted by x and y
            string getAllPointsQuery = "SELECT x, y, land, height, coastal, id FROM WorldPoints ORDER BY x, y;";
            var queryCmd = new MySqlCommand(getAllPointsQuery, db.Connection);
            MySqlDataReader rdr = queryCmd.ExecuteReader();
            while (rdr.Read())
            {
                if (rdr.GetInt32(0) == 0 && (rdr.GetInt32(1) % 100) == 0)
                {
                    Console.WriteLine("Processing line: " + rdr.GetInt32(1));
                }
                int x = rdr.GetInt32(0);
                int y = rdr.GetInt32(1);
                heightMap[y][x].coastal = rdr.GetBoolean(4);
                if (defaultInit && (!heightMap[y][x].coastal))
                {
                    heightMap[y][x].height = -1;
                }
                else if (defaultInit && heightMap[y][x].coastal)
                {
                    heightMap[y][x].height = 1;
                }
                else
                {
                    heightMap[y][x].height = rdr.GetInt32(3);
                }
                heightMap[y][x].water = !rdr.GetBoolean(2);
                heightMap[y][x].id = rdr.GetInt32(5);
                heightMap[y][x].roughness = 0.01f; // Default roughness value
            }
            rdr.Close();
            return heightMap;
        }

        private static void applyRoughness(pointHeight[][] heightMap, roughnessColorMap[] roughnessColorMaps, string roughnessMapPath)
        {
            //Load the roughness bmp and apply the roughness to the heightmap
            using (SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(roughnessMapPath))
            {
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        if (!heightMap[y][x].water)
                        {
                            // Get the pixel color
                            Rgba32 color = image[x, y];
                            // Find the elevation range for the given color
                            heightMap[y][x].roughness = getRoughnessFromColor(color, roughnessColorMaps);
                            heightMap[y][x].type = getRoughnessTypeFromColor(color, roughnessColorMaps);
                        }
                    }
                }
            }
        }

        //Correct the heights of coastal points based on their type
        private static void correctCoastalHeights(pointHeight[][] heightMap, int width, int height, int plainsCoastalOffset, int hillsCoastalOffset, int mountainsCoastalOffset, int deepMountainsCoastalOffset)
        {
            // Correct the heights of coastal points
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!heightMap[y][x].water && heightMap[y][x].coastal)
                    {
                        if (heightMap[y][x].type == "Deep Mountains")
                        {
                            heightMap[y][x].height = deepMountainsCoastalOffset;
                        }
                        if (heightMap[y][x].type == "Mountains")
                        {
                            heightMap[y][x].height = mountainsCoastalOffset;
                        }
                        else if (heightMap[y][x].type == "Hills")
                        {
                            heightMap[y][x].height = hillsCoastalOffset;
                        }
                        else if (heightMap[y][x].type == "Plains")
                        {
                            heightMap[y][x].height = plainsCoastalOffset;
                        }
                        else
                        {
                            heightMap[y][x].height = plainsCoastalOffset;
                        }
                    }
                }
            }
        }

        //This searches the height map for all points that are of type "land" and have no height assigned to them.
        //It then uses a BFS to find the closest point that is not of type "land" and assigns the type of that point to the typeless point.
        //This is used to ensure the height map uses the intended type for the coastal offset + sloping.
        private static void reassignTypelessHeightPoints(pointHeight[][] heightMap, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!heightMap[y][x].water && heightMap[y][x].type == "land")
                    {
                        // BFS to find the closest point not of type "land"
                        var visited = new bool[height, width];
                        var queue = new Queue<(int x, int y)>();
                        queue.Enqueue((x, y));
                        visited[y, x] = true;
                        bool found = false;
                        string foundType = "land";
                        float foundRoughness = 0.01f;
                        while (queue.Count > 0 && !found)
                        {
                            var (cx, cy) = queue.Dequeue();
                            // Check 4 neighbors (up, down, left, right)
                            var neighbors = Point.getNeighborsPlus((cx, cy));
                            foreach (var (nx, ny) in neighbors)
                            {
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !visited[ny, nx])
                                {
                                    visited[ny, nx] = true;
                                    if (!heightMap[ny][nx].water && heightMap[ny][nx].type != "land")
                                    {
                                        foundType = heightMap[ny][nx].type;
                                        foundRoughness = heightMap[ny][nx].roughness;
                                        found = true;
                                        break;
                                    }
                                    queue.Enqueue((nx, ny));
                                }
                            }
                        }
                        if (found && foundType != "land")
                        {
                            heightMap[y][x].type = foundType;
                            heightMap[y][x].roughness = foundRoughness;
                        }
                    }
                }
            }
        }

        private static int getMaxHeight(pointHeight[][] heightMap)
        {
            int max = 0;
            for (int y = 0; y < heightMap.Length; y++)
            {
                for (int x = 0; x < heightMap[y].Length; x++)
                {
                    if (heightMap[y][x].height > max)
                    {
                        max = heightMap[y][x].height;
                    }
                }
            }
            return max;
        }

        private static int getMinHeight(pointHeight[][] heightMap)
        {
            int min = 0;
            for (int y = 0; y < heightMap.Length; y++)
            {
                for (int x = 0; x < heightMap[y].Length; x++)
                {
                    if (heightMap[y][x].height < min)
                    {
                        min = heightMap[y][x].height;
                    }
                }
            }
            return min;
        }

        private static void generateHeightMapValues(pointHeight[][] heightMap, roughnessColorMap[] roughnessColorMaps,IConfiguration config, int heightSoftCap)
        {
            var generationSettings = config.GetSection("HeightGenerationSettings");
            int amplitude = generationSettings.GetValue<int>("Amplitude");
            HeightQueue heightQueue = new HeightQueue(heightMap,roughnessColorMaps,config);
            var noise = new MultifractalNoise();

            // Generate heightmap with noise
            int width = heightMap[0].GetLength(0);
            int height = heightMap.GetLength(0);
            int c = 0;

            Console.WriteLine("Width: " + width + " Height: " + height);
            heightQueue.loadCoastalPointsIntoLists(width, height);

            Console.WriteLine("Plains to process: " + heightQueue.getPlainsCount() + " Hills to process: " + heightQueue.getHillsCount() + " Mountains to Process: " + heightQueue.getMountainsCount() + " Deep Mountains to Process: " + heightQueue.getDeepMountainsCount());

            while (heightQueue.getCumulativePointsToCalc() > 0)
            {
                if ((c % 50000) == 0)
                {
                    Console.WriteLine("Cumulative points to calc: " + heightQueue.getCumulativePointsToCalc());
                    heightQueue.writePointsProcessed();
                    if (heightQueue.getMountainRangePopulated())
                    {
                        heightQueue.populateMountainRangesAvgHeight();
                    }
                }
                (int, int) coords = heightQueue.dequeueWeightlessPlainsHillsWeightedMountains();
                int avgHeight = heightQueue.getAverageHeightAndPushPoints(coords.Item1, coords.Item2);
                float noiseOffset = heightQueue.getNoiseOffset(coords.Item1, coords.Item2);
                float offset = heightQueue.getOffset(coords.Item1, coords.Item2, (float)avgHeight) + noiseOffset;

                //Lower offset if above the soft height cap
                if (avgHeight < heightSoftCap + 5000)
                {
                    offset *= .8f;
                } else if (avgHeight < heightSoftCap + 4000)
                {
                    offset *= .20f;
                } else if (avgHeight < heightSoftCap + 3000)
                {
                    offset *= .35f;
                }
                else if (avgHeight < heightSoftCap + 2000)
                {
                    offset *= .55f;
                }
                else if (avgHeight < heightSoftCap + 1000)
                {
                    offset *= .75f;
                }
                else if (avgHeight < heightSoftCap)
                {
                    offset *= .8f;
                }

                float riseRatio = heightQueue.getMountainRiseRatio(coords.Item1, coords.Item2, avgHeight);

                //Console.WriteLine($"Coords: {coords} avgHeight: {avgHeight} noiseOffset: {noiseOffset} offset: {offset}");
                heightMap[coords.Item2][coords.Item1].height = (int)((avgHeight + ((float)amplitude * heightMap[coords.Item2][coords.Item1].roughness * riseRatio)) + offset);
                //Console.WriteLine(avgHeight - heightMap[coords.Item2][coords.Item1].height);
                if(avgHeight - heightMap[coords.Item2][coords.Item1].height > 1000)
                {
                    heightMap[coords.Item2][coords.Item1].height += 10;
                    //Console.WriteLine("Height increased by 10");
                }
                if (heightMap[coords.Item2][coords.Item1].height < 1 && heightMap[coords.Item2][coords.Item1].coastal)
                {
                    heightMap[coords.Item2][coords.Item1].height = 1;
                }
                heightMap[coords.Item2][coords.Item1].locked = true;
                c++;
            }
        }

        private static void renderHeightmap(pointHeight[][] heightMap, string heightMapOutputPath, int height, int width, int max, int min)
        {
            using (SixLabors.ImageSharp.Image<Rgba32> image = new SixLabors.ImageSharp.Image<Rgba32>(width, height))
            {
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        // Set the pixel color based on the height value
                        if (heightMap[y][x].water)
                        {
                            image[x, y] = Rgba32.ParseHex("#0000FF"); // Water color
                        }
                        else
                        {
                            // You can customize this mapping based on your needs
                            Rgba32 color = getColorFromElevation(heightMap[y][x].height, min, (max + (int)((float)max*.1f)));
                            image[x, y] = color;
                        }
                    }
                }
                image.Save(heightMapOutputPath);
            }
        }

        private static float getRoughnessFromColor(Rgba32 color, roughnessColorMap[] roughnessColorMaps)
        {
            // Find the elevation range for the given color
            foreach (roughnessColorMap roughnessColor in roughnessColorMaps)
            {
                if (roughnessColor.bmpColor == color)
                {
                    return roughnessColor.roughness;
                }
            }
            return 0.01f; // Default case if no match found
        }

        private static string getRoughnessTypeFromColor(Rgba32 color, roughnessColorMap[] roughnessColorMaps)
        {
            // Find the elevation range for the given color
            foreach (roughnessColorMap roughnessColor in roughnessColorMaps)
            {
                if (roughnessColor.bmpColor == color)
                {
                    return roughnessColor.name;
                }
            }
            return "land"; // Default case if no match found
        }

        private static Rgba32 getColorFromElevation(int elevation, int min, int max)
        {
            // Map the elevation to a color based on the elevation range
            float ratio = (float)(elevation - min) / (max - min);
            byte r = (byte)(255 * ratio);
            byte g = (byte)(255 * (1 - ratio));
            byte b = 0; // You can customize this based on your needs
            return new Rgba32(r, g, b);
        }

        private static roughnessColorMap[] loadRoughnessColorMaps(IConfiguration config)
        {
            // Load the elevation color maps section from the configuration  
            var roughnessColorMapsSection = config.GetSection("RoughnessColorMaps");
            if (roughnessColorMapsSection == null)
            {
                Console.WriteLine("RoughnessColorMaps section not found in configuration.");
                return Array.Empty<roughnessColorMap>();
            }

            var elevationColorMaps = roughnessColorMapsSection.GetChildren()
                .Select(section => new roughnessColorMap
                {
                    roughness = float.Parse(section["roughness"]),
                    name = section["name"],
                    bmpColor = Rgba32.ParseHex(section["color"]),
                    weight = int.Parse(section["weight"]),
                    noiseAmplitude = float.Parse(section["noiseAmplitude"])
                })
                .ToArray();

            return elevationColorMaps;
        }

        private static void writeElevationsToDb(DBConnection db, pointHeight[][] heightMap)
        {

            int width = heightMap[0].GetLength(0);
            int height = heightMap.GetLength(0);
            Console.WriteLine("Width: " + width + " Height: " + height);

            List<string> batchHeightUpdateRow = new List<string>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!heightMap[y][x].water)
                    {
                        batchHeightUpdateRow.Add(string.Format("UPDATE WorldPoints SET height = {0} WHERE id = {1}", MySqlHelper.EscapeString(heightMap[y][x].height.ToString()), MySqlHelper.EscapeString(heightMap[y][x].id.ToString())));
                    }
                }
            }


            db.runStringNonQueryCommandBatch("","",batchHeightUpdateRow,2500,';',true);
        }

        private static int getAverageHeightInRadius(int x, int y, int radius, pointHeight[][] heightMap)
        {
            int totalHeight = 0;
            int count = 0;
            int heightMapHeight = heightMap.Length;
            int heightMapWidth = heightMap[0].Length;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < heightMapWidth && ny >= 0 && ny < heightMapHeight)
                    {
                        // Use Euclidean distance to check if within radius
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            if (!heightMap[ny][nx].water)
                            {
                                totalHeight += heightMap[ny][nx].height;
                                count++;
                            }
                        }
                    }
                }
            }
            if (count > 0)
            {
                return (int)((float)totalHeight / count);
            }
            else
            {
                return 0;
            }
        }
    }
    public struct roughnessColorMap
    {
        public float roughness;
        public string name;
        public Rgba32 bmpColor;
        public int weight;
        public float noiseAmplitude;
    }
    public struct pointHeight
    {
        public int x;
        public int y;
        public int height;
        public float roughness;
        public bool locked;
        public bool water;
        public bool coastal;
        public string type;
        public int id;
    }
   
}
