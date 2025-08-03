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

        private List<Lake> generateLakes(Dictionary<(int, int), Direction> downhillFlowDirection)
        {
            List<Lake> lakes = new List<Lake>();
            //Key: point id
            //Value: whether it has been visited or not
            Dictionary<int,bool> visited = new Dictionary<int,bool>();
            List<Point> sinkPoints = getPointsWithoutFlowDirection(downhillFlowDirection, map.getListOfPointsByHeight());
            foreach (Point sink in sinkPoints)
            {
                if (visited[sink.Id]) continue;
                // Determine lake basin by flood-fill up to rim
                decimal lakeSurfaceHeight = determineLakeFillHeight(sink);
                Lake newLake = new Lake { surfaceHeight = lakeSurfaceHeight, waterSalinity = 0.0m};//Freshwater
                Queue<Point> queue = new Queue<Point>();
                queue.Enqueue(sink);
                while (queue.Count > 0)
                {
                    Point q = queue.Dequeue();
                    if (visited[q.id]) continue;
                    visited[q.id] = true;
                    // Mark this point as part of the lake
                    newLake.Points.Add(q);
                    // If q is lower than lake surface, it gets flooded
                    newLake.depth = Math.Max(newLake.depth, lakeSurfaceHeight - q.height);
                    // Enqueue neighbors that are below the water surface height (part of basin)
                    foreach (Point n in q.GetNeighbors8())
                    {
                        if (n.land && n.height <= lakeSurfaceHeight)
                        {
                            queue.Enqueue(n);
                        }
                    }
                }
                lakes.Add(newLake);
                return lakes;
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

        private int determineLakeFillHeight(Point sink)
        {
            HashSet<(int,int)> visited = new HashSet<(int, int)>();
            PriorityQueue<Point, int> pointQueue = new PriorityQueue<Point, int>();
            visited.Add((sink.X, sink.Y));
            pointQueue.Enqueue(sink, sink.Height);

            //The current water level in meters above sea level
            int currentWaterLevel = sink.Height;
            //Tracks the lowest boundary height
            int lowestRimHeight = int.MaxValue;

            //Iterate while the point queue has points to process
            while (pointQueue.Count > 0)
            {
                Point currentPoint = pointQueue.Dequeue();
                //If the current point is lower than the current water level, we should raise the current water level to match it
                if(currentPoint.Height > currentWaterLevel)
                {
                    currentWaterLevel = currentPoint.Height;
                }
                List<Point> neighbors = map.getNeighborsSquare(currentPoint);
            }
            return -1;
        }
        
    }
}
