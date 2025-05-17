using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibNoise.Primitive;
using Microsoft.Extensions.Configuration;

namespace FantasyColonialismMapgen
{
    class HeightQueue
    {
        pointHeight[][] heightMap;
        Random rand;

        private List<(int, int)> plainsToProcess;
        private int plainsWeight;
        private float plainsBaseRoughness;
        private int plainsProcessed = 0;
        private List<(int, int)> hillsToProcess;
        private int hillsWeight;
        private float hillsBaseRoughness;
        private int hillsProcessed = 0;
        private List<(int, int)> mountainsToProcess;
        private List<MountainRange> mountainRangesToProcess;
        private Dictionary<(int, int), int> pointToMountainRange = new Dictionary<(int, int), int>();
        private int mountainsWeight;
        private float mountainsBaseRoughness;
        private int mountainsProcessed = 0;
        private List<(int, int)> deepMountainsToProcess = new List<(int, int)>();
        int deepMountainsWeight;
        private float deepMountainsBaseRoughness;
        private int deepMountainsProcessed = 0;

        private float shortMountainMaximumRiseModifier;

        private bool mountainRangePopulated = false;

        private Perlin perlin = new Perlin();

        private int amplitude = 0;

        public HeightQueue(pointHeight[][] heightMapRef, roughnessColorMap[] roughnessColorMaps, IConfiguration config)
        {
            heightMap = heightMapRef;

            plainsToProcess = new List<(int, int)>();
            hillsToProcess = new List<(int, int)>();
            mountainsToProcess = new List<(int, int)>();
            deepMountainsToProcess = new List<(int, int)>();

            var generationSettings = config.GetSection("HeightGenerationSettings");
            shortMountainMaximumRiseModifier = generationSettings.GetValue<int>("MaxRiseModifier");
            amplitude = generationSettings.GetValue<int>("Amplitude");

            foreach (var roughnessMap in roughnessColorMaps)
            {
                if (roughnessMap.name == "Plains")
                {
                    plainsWeight = roughnessMap.weight;
                    plainsBaseRoughness = roughnessMap.roughness;
                }
                else if (roughnessMap.name == "Hills")
                {
                    hillsWeight = roughnessMap.weight;
                    hillsBaseRoughness = roughnessMap.roughness;
                }
                else if (roughnessMap.name == "Mountains")
                {
                    mountainsWeight = roughnessMap.weight;
                    mountainsBaseRoughness = roughnessMap.roughness;
                }
                else if (roughnessMap.name == "Deep Mountains")
                {
                    deepMountainsWeight = roughnessMap.weight;
                    deepMountainsBaseRoughness = roughnessMap.roughness;
                }
            }
            rand = new Random();
            perlin = new Perlin();
        }

        public void enqueue(int x, int y)
        {
            // If the neighbor is not locked, add it to the list of points to process
            if (heightMap[y][x].roughness <= 0.05f)
            {
                plainsToProcess.Add((x, y));
            }
            else if (heightMap[y][x].roughness > 0.05f && heightMap[y][x].roughness <= 0.3f)
            {
                hillsToProcess.Add((x, y));
            }
            else if (heightMap[y][x].roughness > 0.3f && heightMap[y][x].roughness <= 0.7f)
            {
                mountainsToProcess.Add((x, y));
            }
            else
            {
                deepMountainsToProcess.Add((x, y));
            }
        }

        public (int, int) dequeue()
        {
            //Check if any lists have coastal points
            if (plainsToProcess.Count > 0 && heightMap[plainsToProcess[0].Item2][plainsToProcess[0].Item1].coastal)
            {
                int x = plainsToProcess[0].Item1;
                int y = plainsToProcess[0].Item2;
                plainsToProcess.RemoveAt(0);
                return (x, y);
            }
            else if (hillsToProcess.Count > 0 && heightMap[hillsToProcess[0].Item2][hillsToProcess[0].Item1].coastal)
            {
                int x = hillsToProcess[0].Item1;
                int y = hillsToProcess[0].Item2;
                hillsToProcess.RemoveAt(0);
                return (x, y);
            }
            else if (mountainsToProcess.Count > 0 && heightMap[mountainsToProcess[0].Item2][mountainsToProcess[0].Item1].coastal)
            {
                int x = mountainsToProcess[0].Item1;
                int y = mountainsToProcess[0].Item2;
                mountainsToProcess.RemoveAt(0);
                return (x, y);
            }
            else if (deepMountainsToProcess.Count > 0 && heightMap[deepMountainsToProcess[0].Item2][deepMountainsToProcess[0].Item1].coastal)
            {
                int x = deepMountainsToProcess[0].Item1;
                int y = deepMountainsToProcess[0].Item2;
                deepMountainsToProcess.RemoveAt(0);
                return (x, y);
            }

            // Calculate total weight based on non-empty lists
            int totalWeight = 0;
            if (plainsToProcess.Count > 0)
            {
                totalWeight += plainsWeight;
            }
            if (hillsToProcess.Count > 0)
            {
                totalWeight += hillsWeight;
            }
            if (mountainsToProcess.Count > 0)
            {
                totalWeight += mountainsWeight;
            }
            if (deepMountainsToProcess.Count > 0)
            {
                totalWeight += deepMountainsWeight;
            }

            if (totalWeight == 0)
            {
                return (-1,-1); // No items to dequeue
            }

            int choice = rand.Next(totalWeight);
            List<(int, int)> selectedList = null;

            if (plainsToProcess.Count > 0 && choice < plainsWeight)
            {
                selectedList = plainsToProcess;
                plainsProcessed++;
            }

            else if (hillsToProcess.Count > 0 && choice < plainsWeight + hillsWeight)
            {
                selectedList = hillsToProcess;
                hillsProcessed++;
            }
            else if (mountainsToProcess.Count > 0 && choice < plainsWeight + hillsWeight + mountainsWeight)
            {
                selectedList = mountainsToProcess;
                mountainsProcessed++;
            }
            else if (deepMountainsToProcess.Count > 0)
            {
                selectedList = deepMountainsToProcess;
                deepMountainsProcessed++;
            }

            // Fallback in case of logic error
            if (selectedList == null || selectedList.Count == 0)
            {
                return (-1,-1);
            }

            int idx = rand.Next(selectedList.Count);
            var point = selectedList[idx];
            selectedList.RemoveAt(idx);
            return point;
        }

        public (int, int) dequeueWeightless()
        {

            //Check if any lists have coastal points
            if (plainsToProcess.Count > 0)
            {
                if (heightMap[plainsToProcess[0].Item2][plainsToProcess[0].Item1].coastal)
                {
                    int x = plainsToProcess[0].Item1;
                    int y = plainsToProcess[0].Item2;
                    plainsToProcess.RemoveAt(0);
                    plainsProcessed++;
                    return (x, y);
                }
                else
                {
                    int i = rand.Next(0, plainsToProcess.Count - 1);
                    (int, int) point = plainsToProcess[i];
                    plainsToProcess.RemoveAt(i);
                    plainsProcessed++;
                    return point;
                }
            }
            else if (hillsToProcess.Count > 0)
            {
                if (heightMap[hillsToProcess[0].Item2][hillsToProcess[0].Item1].coastal)
                {
                    int x = hillsToProcess[0].Item1;
                    int y = hillsToProcess[0].Item2;
                    hillsToProcess.RemoveAt(0);
                    hillsProcessed++;
                    return (x, y);
                }
                else
                {
                    int i = rand.Next(0, hillsToProcess.Count - 1);
                    (int, int) point = hillsToProcess[i];
                    hillsToProcess.RemoveAt(i);
                    hillsProcessed++;
                    return point;
                }
            }
            else if (mountainsToProcess.Count > 0)
            {
                if (mountainRangePopulated == false)
                {
                    populateListOfSeperateMountainRanges();
                    Console.WriteLine("Mountain chain populated: " + mountainRangePopulated);
                    mountainRangePopulated = true;
                }
                if (heightMap[mountainsToProcess[0].Item2][mountainsToProcess[0].Item1].coastal)
                {
                    int x = mountainsToProcess[0].Item1;
                    int y = mountainsToProcess[0].Item2;
                    mountainsToProcess.RemoveAt(0);
                    mountainsProcessed++;
                    return (x, y);
                }
                else
                {
                    int i = rand.Next(0, mountainsToProcess.Count - 1);
                    (int, int) point = mountainsToProcess[i];
                    mountainsToProcess.RemoveAt(i);
                    mountainsProcessed++;
                    return point;
                }
            }
            else if (deepMountainsToProcess.Count > 0)
            {
                if (mountainRangePopulated == false)
                {
                    populateListOfSeperateMountainRanges();
                    Console.WriteLine("Mountain chain populated: " + mountainRangePopulated);
                    mountainRangePopulated = true;
                }
                if (heightMap[deepMountainsToProcess[0].Item2][deepMountainsToProcess[0].Item1].coastal)
                {
                    int x = deepMountainsToProcess[0].Item1;
                    int y = deepMountainsToProcess[0].Item2;
                    deepMountainsToProcess.RemoveAt(0);
                    deepMountainsProcessed++;
                    return (x, y);
                }
                else
                {
                    int i = rand.Next(0, deepMountainsToProcess.Count - 1);
                    (int, int) point = deepMountainsToProcess[i];
                    deepMountainsToProcess.RemoveAt(i);
                    deepMountainsProcessed++;
                    return point;
                }
            }

            return (-1, -1);
        }

        public (int, int) dequeueWeightlessPlainsHillsWeightedMountains()
        {

            //Check if any lists have coastal points
            if (plainsToProcess.Count > 0)
            {
                if (heightMap[plainsToProcess[0].Item2][plainsToProcess[0].Item1].coastal)
                {
                    int x = plainsToProcess[0].Item1;
                    int y = plainsToProcess[0].Item2;
                    plainsToProcess.RemoveAt(0);
                    plainsProcessed++;
                    return (x, y);
                }
                else
                {
                    int i = rand.Next(0, plainsToProcess.Count - 1);
                    (int, int) point = plainsToProcess[i];
                    plainsToProcess.RemoveAt(i);
                    plainsProcessed++;
                    return point;
                }
            }
            else if (hillsToProcess.Count > 0)
            {
                if (heightMap[hillsToProcess[0].Item2][hillsToProcess[0].Item1].coastal)
                {
                    int x = hillsToProcess[0].Item1;
                    int y = hillsToProcess[0].Item2;
                    hillsToProcess.RemoveAt(0);
                    hillsProcessed++;
                    return (x, y);
                }
                else
                {
                    int i = rand.Next(0, hillsToProcess.Count - 1);
                    (int, int) point = hillsToProcess[i];
                    hillsToProcess.RemoveAt(i);
                    hillsProcessed++;
                    return point;
                }
            }
            else 
            {
                //Calculate the base range of the mountain ranges to scale rise
                if (mountainRangePopulated == false)
                {
                    populateListOfSeperateMountainRanges();
                    Console.WriteLine("Mountain chain populated: " + mountainRangePopulated);
                    mountainRangePopulated = true;
                }
                //Run any coastal mountains as a priority
                if (heightMap[mountainsToProcess[0].Item2][mountainsToProcess[0].Item1].coastal)
                {
                    int x = mountainsToProcess[0].Item1;
                    int y = mountainsToProcess[0].Item2;
                    mountainsToProcess.RemoveAt(0);
                    mountainsProcessed++;
                    return (x, y);
                }
               

                // Calculate total weight based on non-empty lists
                int totalWeight = 0;
                if (mountainsToProcess.Count > 0)
                {
                    totalWeight += mountainsWeight;
                }
                if (deepMountainsToProcess.Count > 0)
                {
                    totalWeight += deepMountainsWeight;
                }
                int choice = rand.Next(totalWeight);
                List<(int, int)> selectedList = null;

                if (mountainsToProcess.Count > 0 && choice < mountainsWeight)
                {
                    selectedList = mountainsToProcess;
                    mountainsProcessed++;
                }
                else if (deepMountainsToProcess.Count > 0)
                {
                    selectedList = deepMountainsToProcess;
                    deepMountainsProcessed++;
                } else
                {
                    return (-1, -1);
                }

                int i = rand.Next(0, selectedList.Count - 1);
                (int, int) point = selectedList[i];
                selectedList.RemoveAt(i);
                return point;
            }
        }

        public float getPerlinOffset(int x, int y)
        {
            string type = heightMap[y][x].type;
            float perlinValue = perlin.getPerlin(x, y);
            if (perlinValue > 1f)
            {
                perlinValue = 1f;
            }
            else if(perlinValue < -1f)
            {
                perlinValue = -1f;
            }
                float offset = 0f;
            if (type == "Plains")
            {
                offset = perlinValue * amplitude * plainsBaseRoughness;
            }
            else if (type == "Hills")
            {
                offset = perlinValue * amplitude * hillsBaseRoughness;
            }
            else if (type == "Mountains")
            {
                offset = perlinValue * amplitude * mountainsBaseRoughness;
            }
            else if (type == "Deep Mountains")
            {
                offset = perlinValue * amplitude * deepMountainsBaseRoughness;
            }
            //Console.WriteLine("Perlin offset: " + perlinValue + " Offset: " + offset);
            return offset * 7.5f;
        }
        public float getOffset(int x, int y, float avgHeight)
        {
            if (heightMap[y][x].type == "Mountains" || heightMap[y][x].type == "Deep Mountains")
            {
                int mountainChainIndex = pointToMountainRange[(x, y)];
                float rangeBaseHeight = mountainRangesToProcess[mountainChainIndex].avgHeightOnBorders;
                float difference = rangeBaseHeight - avgHeight;
                if (difference > 0)
                {
                    return difference/100f;
                }
                else
                {
                    return 0f;
                }
            }
            return 0f;
        }

        public void populateListOfSeperateMountainRanges()
        {
            mountainRangesToProcess = new List<MountainRange>();
            HashSet<(int, int)> processedMountains = new HashSet<(int, int)>();
            int height = heightMap.Length;
            int width = heightMap[0].Length;
            int i = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!heightMap[y][x].water)
                    {
                        if ((heightMap[y][x].type == "Mountains" || heightMap[y][x].type == "Deep Mountains") && !processedMountains.Contains((x, y)))
                        {
                            MountainRange chain = new MountainRange();
                            Queue<(int, int)> queue = new Queue<(int, int)>();
                            queue.Enqueue((x, y));
                            processedMountains.Add((x, y));
                            chain.points.Add((x, y));
                            pointToMountainRange.Add((x, y), i);

                            while (queue.Count > 0)
                            {
                                var (cx, cy) = queue.Dequeue();
                                foreach (var (nx, ny) in Point.getNeighborsSquare((cx, cy)))
                                {
                                    // Bounds check
                                    if (ny >= 0 && ny < heightMap.Length && nx >= 0 && nx < heightMap[ny].Length)
                                    {
                                        if ((heightMap[ny][nx].type == "Mountains" || heightMap[ny][nx].type == "Deep Mountains") && !processedMountains.Contains((nx, ny)))
                                        {
                                            queue.Enqueue((nx, ny));
                                            processedMountains.Add((nx, ny));
                                            chain.points.Add((nx, ny));
                                            pointToMountainRange.Add((nx, ny), i);
                                        }
                                    }
                                }
                            }
                            mountainRangesToProcess.Add(chain);
                            i++;
                        }
                    }
                }
            }
            populateMountainRangesAvgHeight();
            Console.WriteLine("Mountain chains found: " + mountainRangesToProcess.Count);
            foreach (var chain in mountainRangesToProcess)
            {
                Console.WriteLine("Chain size: " + chain.points.Count + " Average Height: " + chain.avgHeightOnBorders);
            }
        }

        public void populateMountainRangesAvgHeight()
        {
            for(int i = 0; i < mountainRangesToProcess.Count;i++)
            {
                int totalHeight = 0;
                int count = 0;
                foreach (var point in mountainRangesToProcess[i].points)
                {
                    var neighbors = Point.getNeighborsSquare((point.Item1, point.Item2));
                    foreach (var neighbor in neighbors)
                    {
                        int xN = neighbor.Item1;
                        int yN = neighbor.Item2;
                        if (!heightMap[yN][xN].water && heightMap[yN][xN].locked)
                        {
                            totalHeight += heightMap[yN][xN].height;
                            //Console.WriteLine(point.Item1 + " " + point.Item2 + " " + heightMap[yN][xN].height);
                            count++;
                        }
                    }
                }
                int avgHeight = (int)((float)totalHeight / (float)count);
                mountainRangesToProcess[i].setAvgHeight(avgHeight);
                Console.WriteLine("Mountain chain " + i + " average height: " + mountainRangesToProcess[i].avgHeightOnBorders);
            }
        }

        public void loadCoastalPointsIntoLists(int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (heightMap[y][x].coastal)
                    {
                        if (heightMap[y][x].roughness <= plainsBaseRoughness)
                        {
                            plainsToProcess.Add((x, y));
                        }
                        else if (heightMap[y][x].roughness > plainsBaseRoughness && heightMap[y][x].roughness <= hillsBaseRoughness)
                        {
                            hillsToProcess.Add((x, y));
                        }
                        else if (heightMap[y][x].roughness > hillsBaseRoughness && heightMap[y][x].roughness <= mountainsBaseRoughness)
                        {
                            mountainsToProcess.Add((x, y));
                        }
                        else
                        {
                            deepMountainsToProcess.Add((x, y));
                        }
                    }
                }
            }
        }

        public void writePointsProcessed()
        {
            Console.WriteLine("Plains processed: " + plainsProcessed + " Hills processed: " + hillsProcessed + " Mountains processed: " + mountainsProcessed + " Deep Mountains processed: " + deepMountainsProcessed);
        }

        public int getCumulativePointsToCalc()
        {
            return (plainsToProcess.Count + hillsToProcess.Count + mountainsToProcess.Count + deepMountainsToProcess.Count);
        }

        public int getPlainsCount()
        {
            return plainsToProcess.Count;
        }

        public int getHillsCount()
        {
            return hillsToProcess.Count;
        }

        public int getMountainsCount()
        {
            return mountainsToProcess.Count;
        }

        public int getDeepMountainsCount()
        {
            return deepMountainsToProcess.Count;
        }

        public bool getMountainRangePopulated()
        {
            return mountainRangePopulated;
        }

        public float getMountainRiseRatio(int x, int y, int avgHeight)
        {
            if (heightMap[y][x].type != "Mountains" && heightMap[y][x].type != "Deep Mountains")
            {
                return 1f;
            }
            else
            {
                float rangeBaseHeight = mountainRangesToProcess[pointToMountainRange[(x,y)]].avgHeightOnBorders;

                float ratio = rangeBaseHeight / (float)avgHeight;
                if(ratio > shortMountainMaximumRiseModifier)
                {
                    return shortMountainMaximumRiseModifier;
                }
                if (ratio < 1f)
                {
                    return 1f;
                }
                else
                {
                
                    return ratio;
                }
            }
        }

        public int getAverageHeightAndPushPoints(int x, int y)
        {
            int totalHeight = 0;
            int count = 0;
            // Get the neighbors of the point
            var neighbors = Point.getNeighborsSquare((x, y));
            foreach (var neighbor in neighbors)
            {
                int xN = neighbor.Item1;
                int yN = neighbor.Item2;
                if (heightMap[yN][xN].locked && !heightMap[yN][xN].water)
                {
                    totalHeight += heightMap[yN][xN].height;
                    count++;
                }
                else if (!heightMap[yN][xN].locked && !heightMap[yN][xN].water)
                {
                    // If the neighbor is not locked, add it to the list of points to process
                    if (heightMap[yN][xN].roughness <= plainsBaseRoughness)
                    {
                        plainsToProcess.Add((xN, yN));
                    }
                    else if (heightMap[yN][xN].roughness > plainsBaseRoughness && heightMap[yN][xN].roughness <= hillsBaseRoughness)
                    {
                        hillsToProcess.Add((xN, yN));
                    }
                    else if (heightMap[yN][xN].roughness > hillsBaseRoughness && heightMap[yN][xN].roughness <= mountainsBaseRoughness)
                    {
                        mountainsToProcess.Add((xN, yN));
                    }
                    else
                    {
                        deepMountainsToProcess.Add((xN, yN));
                    }
                }
            }
            if (count > 0)
            {
                return (int)((float)totalHeight / (float)count);
            }
            else
            {
                return 1; // No valid neighbors found
            }
        }
    }
}
