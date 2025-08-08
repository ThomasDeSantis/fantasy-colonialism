using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Npgsql;
using System.ComponentModel;

namespace FantasyColonialismMapgen
{
    
    class RiverErosionGen
    {

        
        string parentDirectory;
        IConfiguration config;
        Map map;
        int height;
        int width;
        List<Lake> lakes = new List<Lake>();
        Dictionary<int,int> pointToLake = new Dictionary<int, int>(); // Maps point ID to lake ID
        public RiverErosionGen(DBConnection db, IConfiguration config, string parentDirectory)
        {
            this.parentDirectory = parentDirectory;
            this.config = config;
            map = new Map(db);

            height = map.height;
            width = map.width;

        }

        public void generateRiversAndLakes(DBConnection db)
        {
            Console.WriteLine($"Begin river generation cycle: {DateTime.UtcNow.ToString()}");
            riverErosionGenerationCycle(db);

        }

        private void riverErosionGenerationCycle(DBConnection db)
        {
            for(int i = 0; i < 1000; i++)
            {
                Dictionary<(int,int),Direction> downhillFlowDirection = generateDownhillFlowDirections();
                generateWaterRunoff(downhillFlowDirection, db);
                List<Lake> lakes = generateLakes(downhillFlowDirection);
            }
        }

        //Generate a dictionary of downhill flow directions for each point on the map.
        //A cardinal direction indicates which direction it flows
        //An undefined direction indicates that the point is invalid to receive flow
        //A center direction indicates that the point has nowhere to flow.
        private Dictionary<(int,int),Direction> generateDownhillFlowDirections()
        {
            Dictionary<(int,int), Direction> downhillFlowDirection = new Dictionary<(int, int), Direction>();
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    Point p = map.getPoint(j, i);
                    //Check if point is land
                    if (!p.Land)
                    {
                        downhillFlowDirection[(j,i)] = Direction.undefined; // Ocean or water points do not flow further
                        continue;
                    }
                    //Find the lowest neighboring point
                    //TODO: Add variation so that the flow direction is not always the constant
                    int x = j, y = i;
                    decimal minHeight = p.Height;

                    foreach (Point neighbor in map.getNeighborsPlus(j,i))
                    {
                        if (neighbor.Height < minHeight)
                        {
                            minHeight = neighbor.Height;
                            x = neighbor.X;
                            y = neighbor.Y;
                        }
                    }
                    downhillFlowDirection[(j, i)] = map.findRelativeSinglePointDirection(j, i, x, y);
                }
            }
            return downhillFlowDirection;
        }

        private void generateWaterRunoff(Dictionary<(int, int), Direction> downhillFlowDirection, DBConnection db)
        {
            List<Point> pointsByHeight = map.getListOfPointsByHeight();
            // Initialize each point's water flux to its local rainfall contribution (volume per second).
            foreach (Point p in pointsByHeight)
            {
                // rainfall (mm/year) -> m/year by dividing 1000, then to m^3/sec:
                double rainVolumePerYear = (p.AverageRainfall / 1000.0) * p.Area; // m^3 of water per year
                double rainVolumePerSecond = rainVolumePerYear / (365 * 24 * 3600);
                p.waterRunoff = rainVolumePerSecond;
            }

            //Now we need to carry the water down to its downhill neighbor
            foreach (Point p in pointsByHeight)
            {
                //If direction is center then it is a sink and stays in the point, where it may become a potential lake
                //If it is undefined it is invalid
                Direction dir = downhillFlowDirection[(p.X,p.Y)];
                if (dir != Direction.center || dir != Direction.undefined)
                {
                    Point dP = map.getPointInDirection(p.X, p.Y, dir);
                    // pass all its water to the downhill neighbor
                    dP.waterRunoff += p.waterRunoff;
                }
            }

            return;
        }

        public void generateLakesWithVolume(Dictionary<(int, int), Direction> downhillFlowDirection)
        {
            //Key: point id
            //Value: whether it has been visited or not
            HashSet<(int,int)> visited = new HashSet<(int, int)>();
            List<Point> sinkPoints = getPointsWithoutFlowDirection(downhillFlowDirection, map.getListOfPointsByHeight());

            Console.WriteLine($"Found {sinkPoints.Count} sink points to process for lakes.");

            foreach (Point sink in sinkPoints)
            {
                //This sink has already been processed so we may ignore it
                if (visited.Contains(sink.getCoordinates())){
                    continue;
                }

                Lake lake = floodFillLake(sink, visited);
            }
        }

        private List<(int,int)> queryPointsByHeight(DBConnection db)
        {
            List<(int, int)> pointsByHeight = new List<(int, int)>();
            string query = "SELECT x, y FROM \"Points\" WHERE land = true ORDER BY height DESC;";
            NpgsqlDataReader rdr = db.runQueryCommand(query);
            while (rdr.Read())
            {
                int x = rdr.GetInt32(0);
                int y = rdr.GetInt32(1);
                pointsByHeight.Add((x, y));
            }
            rdr.Close();
            return pointsByHeight;
        }

        //Finds points that are sinks, i.e. have no neighbors that are lower than them
        private List<Point> getPointsWithoutFlowDirection(Dictionary<(int, int), Direction> downhillFlowDirection, List<Point> points)
        {
            List<Point> sinkPoints = new List<Point>();
            foreach(Point p in points)
            {
                //If the point has no flow direction, it is a sink
                if (downhillFlowDirection[(p.X, p.Y)] == Direction.center)
                {
                    sinkPoints.Add(p);
                }
            }
            return sinkPoints;
        }

        private Lake floodFillLake(Point sink, HashSet<(int, int)> visited)
        {
            //Create new priority queue with the priority being the height of the point
            PriorityQueue<Point, int> pointQueue = new PriorityQueue<Point, int>();
            HashSet<(int,int)> basinVisited = new HashSet<(int,int)>();

            int currentWaterLevel = sink.Height;
            int lowestRimHeight = int.MaxValue;
            Point rimOutflow = null;

            pointQueue.Enqueue(sink, sink.Height);
            visited.Add(sink.getCoordinates());
            basinVisited.Add(sink.getCoordinates());

            List<Point> basinPoints = new List<Point>();

            while(pointQueue.Count > 0)
            {
                Point point = pointQueue.Dequeue();

                if (point.Height > currentWaterLevel)
                {
                    currentWaterLevel = point.Height;
                }

                basinPoints.Add(point);

                foreach (Point neighbor in map.getNeighborsSquare(point))
                {
                    (int,int) neighborCoords = neighbor.getCoordinates();

                    if (basinVisited.Contains(neighborCoords))
                    {
                        continue;
                    }

                    basinVisited.Add(neighborCoords);
                    visited.Add(neighborCoords);

                    if (!neighbor.Land)
                    {
                        if (neighbor.Height < lowestRimHeight)
                        {
                            lowestRimHeight = neighbor.Height;
                        }
                        continue;
                    }

                    if (neighbor.Height <= currentWaterLevel)
                    {
                        pointQueue.Enqueue(neighbor, neighbor.Height);
                    }
                    else
                    {
                        if (neighbor.Height < lowestRimHeight)
                        {
                            lowestRimHeight = neighbor.Height;
                            rimOutflow = neighbor;
                        }
                        pointQueue.Enqueue(neighbor, neighbor.Height);
                    }
                }
            }
            // Calculate inflow
            double V_in = basinPoints.Sum(c => c.WaterRunoff);
            if (V_in <= 0.0)
            {
                return null;
            }

            // Calculate capacity to rim
            double V_rim = basinPoints
                .Where(c => c.Height < lowestRimHeight)
                .Sum(c => (double)(lowestRimHeight - c.Height) * c.Area);

            int surfaceHeight;
            double finalVolume;
            double outflow;
            int minHeight = basinPoints.Min(c => c.Height);
            if (V_in >= V_rim)
            {
                surfaceHeight = lowestRimHeight;
                finalVolume = V_rim;
                outflow = V_in - V_rim;
            }
            else
            {
                surfaceHeight = findFillHeight(basinPoints, V_in, minHeight, lowestRimHeight);
                finalVolume = V_in;
                outflow = 0.0;
            }

            Lake lake = new Lake(basinPoints, surfaceHeight, minHeight, outflow, finalVolume);

            if (outflow > 0 && rimOutflow != null)
            {
                rimOutflow.WaterRunoff += outflow;
                lake.OutflowPoint = rimOutflow;
            }

            return lake;
        }


        // Finds the fill height for a partial lake when V_in < V_rim
        //TODO: Account for porousness of the soil
        private decimal findFillHeight(List<Point> basinPoints, double V_in, decimal minHeight, int rimHeight)
        {
            var sorted = basinPoints.OrderBy(c => c.Height).ToList();
            double volumeRemaining = V_in;
            double currentHeight = (double)minHeight;

            for (int i = 1; i <= sorted.Count; i++)
            {
                double nextHeight = (i < sorted.Count) ? (double)sorted[i].Height : rimHeight;
                int cellsBelow = i;
                double volumeNeeded = (nextHeight - currentHeight) * cellsBelow * sorted.Average(c => c.Area); 

                if (volumeRemaining >= volumeNeeded)
                {
                    volumeRemaining -= volumeNeeded;
                    currentHeight = nextHeight;
                }
                else
                {
                    currentHeight += volumeRemaining / (cellsBelow * sorted[0].Area);
                    break;
                }
            }

            return (decimal)currentHeight;
        }

    }
}
