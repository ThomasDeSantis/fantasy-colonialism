using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Configuration;
using ExCSS;

namespace FantasyColonialismMapgen
{
    class MapProvinceCreate
    {
        enum gridOrientation
        {
            topLeft,
            topRight,
            bottomLeft,
            bottomRight,
            topMiddle,
            bottomMiddle,
            leftMiddle,
            rightMiddle
        }
        private static string pointInsertQuery = "INSERT INTO points (x, y,provinceId) VALUES (@x, @y,@provinceId)";
        private static string provinceInsertQuery = "INSERT INTO provinces (id) VALUES (@id)";
        private static string checkIfPointInAProvince = "SELECT provinceId FROM Points WHERE x = @x AND y = @y";
        private static string edgeInsertQuery = "INSERT INTO renderEdges (x1,y1,x2,y2,provinceId) VALUES (@x1,@y1,@x2,@y2,@provinceId)";
        private static string oceanPointInsertQuery = "INSERT INTO points (x,y,land,waterSalinity,type) VALUES (@x,@y,false,3.5,'ocean')";
        private static string lakePointInsertQuery = "INSERT INTO points (x,y,land,waterSalinity,type) VALUES (@x,@y,false,0.5,'lake')";
        private static string truncateRenderEdges = "TRUNCATE TABLE renderEdges";
        //Gets all points in the image array that has not been allocated a point
        private static string getUnallocatedPoints = "WITH RECURSIVE nums_x AS (SELECT 0 AS x UNION ALL SELECT x + 1 FROM nums_x WHERE x < @x ), nums_y AS (SELECT 0 AS y UNION ALL SELECT y + 1 FROM nums_y WHERE y < @y ) SELECT nx.x, ny.y FROM nums_x nx CROSS JOIN nums_y ny LEFT JOIN Points p ON p.x = nx.x AND p.y = ny.y WHERE p.id IS NULL";

        public static void populatePointsAndWorldPointsFromImage(string inputPath, DBConnection database, IConfiguration config)
        {
            Console.WriteLine("Begin processing image into world points: " + DateTime.UtcNow.ToString());
            //Run this to populate the world point table
            //processImageIntoWorldPoints(inputPath, database, config);
            Console.WriteLine("Finished processing image into world points: " + DateTime.UtcNow.ToString());
            Console.WriteLine("Begin rendering world map points: " + DateTime.UtcNow.ToString());
            renderWorldPointsAsImage(inputPath, database, config);
            Console.WriteLine("Finished rendering world map points: " + DateTime.UtcNow.ToString());
        }

        public static void processImageIntoWorldPoints(string inputPath, DBConnection database, IConfiguration config)
        {
            //Check current max_allowed_packet


            database.runStringNonQueryCommand("CALL `sp_TRUNCATE_POINTS`();");
            Console.WriteLine($"Image processing began: {DateTime.UtcNow.ToString()}");
            int pointId = 0; //Used 


            Rgba32 pointColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapPoint"));
            Rgba32 oceanColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapOceanPoint"));

            List<string> batchWorldPointInsertRow = new List<string>();

            using (SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath))
            {
                int height = image.Height;
                int width = image.Width;
                for (int y = 0; y < image.Height; y++)
                {
                  
                    for (int x = 0; x < image.Width; x++)
                    {
                        if (pointId % 200000 == 0)
                        {
                            if(batchWorldPointInsertRow.Count > 0)
                            {
                                Console.WriteLine("Finished processing world point " + pointId + " points.");
                                StringBuilder batchWorldPointInsert = new StringBuilder("INSERT INTO WorldPoints (x,y,land) VALUES ");
                                //Finish the insert statement
                                batchWorldPointInsert.Append(string.Join(",", batchWorldPointInsertRow));
                                batchWorldPointInsert.Append(";");
                                //Insert all of the white points into the DB
                                Console.WriteLine(batchWorldPointInsert.Length);
                                database.runStringNonQueryCommand(batchWorldPointInsert.ToString());
                                Console.WriteLine(batchWorldPointInsertRow.Count);
                                batchWorldPointInsertRow.Clear();
                                Console.WriteLine(batchWorldPointInsertRow.Count);
                            }
                        }
                        if (image[x, y] == pointColor)
                        {
                            //This pixel represents a land point. Add it to the list of lands points to be inserted.
                            batchWorldPointInsertRow.Add(string.Format("({0},{1},true)", MySqlHelper.EscapeString(x.ToString()), MySqlHelper.EscapeString(y.ToString())));
                            pointId++;
                        }
                        else if (image[x, y] == oceanColor)
                        {
                            //This is a ocean pixel. Add it to the list of ocean points to be inserted.
                            batchWorldPointInsertRow.Add(string.Format("({0},{1},false)", MySqlHelper.EscapeString(x.ToString()), MySqlHelper.EscapeString(y.ToString())));
                        }
                        else
                        {
                            //If it is not either then you know the image is invalid. Throw an exception.
                            throw new Exception("Invalid image. Point identified at " + x.ToString() + ", " + y.ToString() + " as " + image[x, y].ToString() +
                                ". Image must only contain pixels of color " + config.GetValue<string>("MapgenStrings:BaseMapPoint") + " (land) and " + config.GetValue<string>("MapgenStrings:BaseMapOceanPoint") + " (water).");
                        }
                        pointId++;
                    }
                }

                StringBuilder batchWorldPointInsertFinal = new StringBuilder("INSERT INTO WorldPoints (x,y,land) VALUES ");
                //Finish the insert statement
                batchWorldPointInsertFinal.Append(string.Join(",", batchWorldPointInsertRow));
                batchWorldPointInsertFinal.Append(";");
                //Insert all of the white points into the DB
                Console.WriteLine(batchWorldPointInsertFinal.Length);
                database.runStringNonQueryCommand(batchWorldPointInsertFinal.ToString());
            }
        }

        public static void renderWorldPointsAsImage(string outputPath, DBConnection database,IConfiguration config)
        {
            int height = config.GetValue<int>("ImageSettings:WorldHeight");
            int width = config.GetValue<int>("ImageSettings:WorldWidth");
            Console.WriteLine("Height: " + height + " Width: " + width);
            Rgba32 oceanColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapOceanPoint"));
            // Query all world points
            string query = "SELECT x, y, land FROM WorldPoints;";
            var cmd = new MySqlCommand(query, database.Connection);
            int maxX = 0, maxY = 0;
            // Load the height map from the database
            bool[][] worldLand = new bool[height][];
            for (int i = 0; i < height; i++)
            {
                worldLand[i] = new bool[width];
            }
            Console.WriteLine(worldLand[0][0]);

            return;
            /*
                using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    int x = rdr.GetInt32(0);
                    int y = rdr.GetInt32(1);
                    bool land = rdr.GetBoolean(2);
                    points.Add((x, y, land));
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            // Create image
            using (var image = new SixLabors.ImageSharp.Image<Rgba32>(maxX + 1, maxY + 1))
            {
                foreach (var (x, y, land) in points)
                {
                    image[x, y] = land ? Rgba32.ParseHex("FFFFFF") : Rgba32.ParseHex("0000FF");
                }
                image.SaveAsPng(outputPath);
            }
            */
        }













        public static void processImageIntoPointsAndProvinces(string inputPath, DBConnection database,IConfiguration config)
        {
            Console.WriteLine("Image processing began: " + DateTime.UtcNow.ToString());
            int pointId = 0;// Point ID that will be stored in DB
            int height = 0;
            int width = 0;
            List<(int, int)> blackPoints = new List<(int, int)>(); //This list will store all the black points in the image
            List<(int, int)> oceanPoints = new List<(int, int)>(); //This will store all the ocean points in the image
            List<(int, int)> lakePoints = new List<(int, int)>(); //This will store all the ocean points in the image
            HashSet<(int, int)> whitePoints = new HashSet<(int, int)>(); //This hashset will store all the white points in the image. Is a hashset for faster lookup times.



            Rgba32 borderColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapBorder"));
            Rgba32 pointColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapPoint"));
            Rgba32 oceanColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapOceanPoint"));
            Rgba32 lakeColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapLakePoint"));


            var pointCmd = new MySqlCommand(pointInsertQuery, database.Connection);
            using (SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath))
            {
                height = image.Height;
                width = image.Width;
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        var color = image[x, y];
                        if (color == oceanColor)
                        {
                            //This is a ocean pixel
                            //Add it to the list of ocean points
                            oceanPoints.Add((x, y));
                        }
                        else if (color == pointColor)
                        {
                            //This is a white pixel
                            //Add it to the list of white points
                            whitePoints.Add((x, y));
                        }
                        else if (color == borderColor)
                        {
                            //This is a black pixel
                            //Add it to the list of black points
                            blackPoints.Add((x, y));
                        }
                        else if (color == lakeColor)
                        {
                            //This is a lake pixel
                            //Add it to the list of lake points
                            lakePoints.Add((x, y));
                        }
                    }
                }
            }
            Console.WriteLine("White Points: " + whitePoints.Count() + "Black Points: " + blackPoints.Count() + "Ocean Points: " + oceanPoints.Count() + "Lake Points: " + lakePoints.Count());
            Console.WriteLine("Finished processing image: " + DateTime.UtcNow.ToString());

            insertWhitePointsToDB(database, whitePoints);


            Console.WriteLine("Finished INSERTing white points into DB: " + DateTime.UtcNow.ToString());

            //Update the average center of each province in the DB
            Province.calculateProvincesAverages(database);
            Console.WriteLine("Finished calculating province average x & y: " + DateTime.UtcNow.ToString());

            //Now assign all border points to the province with the most bordering points
            pointId = insertBorderPointsToDB(database, blackPoints, pointId);

            Console.WriteLine("Finished INSERTing border points into DB: " + DateTime.UtcNow.ToString());

            insertWaterPointsToDB(database, oceanPoints, 3.5m, "ocean", pointId);

            Console.WriteLine("Finished INSERTing ocean points into DB: " + DateTime.UtcNow.ToString());

            insertWaterPointsToDB(database, lakePoints, 0.5m, "lake", pointId);

            Console.WriteLine("Finished INSERTing lake points into DB: " + DateTime.UtcNow.ToString());

            Console.WriteLine("Height:" + height + " Width: " + width);


            //Assign any point in the bitmap that has not been allocated
            assignRemainingUnallocatedPoints(database, height, width);


            Console.WriteLine("Finished allocating additional points into DB: " + DateTime.UtcNow.ToString());

            //Update the average center of each province in the DB now that it is complete
            Province.calculateProvincesAverages(database);

            Console.WriteLine("Finished calculating province average x & y: " + DateTime.UtcNow.ToString());
        }


        //Check how many points are unallocated and iterate until all are assigned
        public static void assignRemainingUnallocatedPoints(DBConnection database, int height, int width)
        {
            int pointsRemaining;
            do
            {
                var cmd = new MySqlCommand(getUnallocatedPoints, database.Connection);
                cmd.Parameters.AddWithValue("@x", width - 1);
                cmd.Parameters.AddWithValue("@y", height - 1);
                MySqlDataReader rdr = cmd.ExecuteReader();
                cmd.Parameters.Clear();
                //Store each id, x, and y in a list
                List<(int, int)> unallocatedPoints = new List<(int, int)>();
                while (rdr.Read())
                {
                    unallocatedPoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
                }
                rdr.Close();
                Console.WriteLine("Unallocated points before allocation: " + unallocatedPoints.Count);
                //For each point that is unallocated, assign it to a random province
                List<(int, int)> allocatedPoints = new List<(int, int)>();
                foreach ((int, int) point in unallocatedPoints)
                {
                    if(assignUnallocatedPoints(database, point))
                    {
                        allocatedPoints.Add(point);
                    }                  
                }
                unallocatedPoints = unallocatedPoints.Except(allocatedPoints).ToList();
                pointsRemaining = unallocatedPoints.Count;
                Console.WriteLine("Unallocated points after allocation: " + pointsRemaining);
            } while (pointsRemaining > 0);
        }


        //Returns the number of points process
        //Returns -1 if failure
        private static int insertWhitePointsToDB(DBConnection database, HashSet<(int, int)> whitePoints)
        {
            try
            {
                HashSet<(int, int)> visited = new HashSet<(int, int)>();

                int provinceId = 0;//Province ID that will be stored in DB
                int pointId = 0; //Used for logging purposes

                //Do a depth first search
                //At this point all white points will be divided into provinces and pushed into the DB

                var provinceCmd = new MySqlCommand(provinceInsertQuery, database.Connection);
                List<string> batchWhitePointInsertRows = new List<string>();
                foreach ((int, int) point in whitePoints)
                {
                    if (!visited.Contains(point))
                    {
                        //This point has not been visited yet
                        //Create a new province and add it to the DB
                        int currentProvinceId = provinceId++;
                        provinceCmd.Parameters.AddWithValue("@id", currentProvinceId);
                        provinceCmd.ExecuteNonQuery();
                        provinceCmd.Parameters.Clear();


                        Stack<(int, int)> stack = new Stack<(int, int)>();
                        stack.Push(point);
                        while (stack.Count > 0)
                        {
                            
                            (int, int) currentPoint = stack.Pop();
                            if (visited.Contains(currentPoint))
                            {
                                continue;
                            }
                            visited.Add(currentPoint);
                            //Add point to batch insert
                            batchWhitePointInsertRows.Add(string.Format("({0},{1},{2})", MySqlHelper.EscapeString(currentPoint.Item1.ToString()), MySqlHelper.EscapeString(currentPoint.Item2.ToString()), MySqlHelper.EscapeString(currentProvinceId.ToString())));
                            pointId++;

                            //Check all the neighbors of the current point
                            foreach ((int, int) neighbor in Point.getNeighborsPlus(currentPoint))
                            {
                                if (whitePoints.Contains(neighbor) && !visited.Contains(neighbor))
                                {
                                    stack.Push(neighbor);
                                }
                            }
                            if (pointId % 1000 == 0)
                            {
                                Console.WriteLine("Finished processing white" + pointId + " points." + " Current province id: " + provinceId);

                            }
                        }
                    }
                }

                StringBuilder batchWhitePointInsert = new StringBuilder("INSERT INTO Points (x,y,provinceId) VALUES ");
                //Finish the insert statement
                batchWhitePointInsert.Append(string.Join(",", batchWhitePointInsertRows));
                batchWhitePointInsert.Append(";");
                //Insert all of the white points into the DB

                database.runStringNonQueryCommand(batchWhitePointInsert.ToString());

                return pointId;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        private static int insertBorderPointsToDB(DBConnection database, List<(int,int)> borderPoints,int pointId)
        {
            try
            {

                //Get a list of all points that are currently valid with a province
                //While iterating through the black points, assign them to the province with the most bordering points
                //If an equal number of province points exist (EX: 2 provinces with 1 point each) then assign it to a random province
                //Only do this for a single loop. All other black points will be handled during a final cleanup pass at the end.

                List<string> batchBorderPointInsertRows = new List<string>();

                //Get all valid land points with their respective province ids
                Dictionary<(int, int), int> validLandPoints = Point.retrieveAllValidLandPoints(database);

                //For each black point that was found in the initial search
                foreach ((int, int) point in borderPoints)
                {
                    
                    //Query possible provinces.
                    int possibleProvinceId = Point.getNeighborValidPoint(validLandPoints, point);
                    if (possibleProvinceId != -1)
                    {
                        batchBorderPointInsertRows.Add(string.Format("({0},{1},{2})", MySqlHelper.EscapeString(point.Item1.ToString()), MySqlHelper.EscapeString(point.Item2.ToString()), MySqlHelper.EscapeString(possibleProvinceId.ToString())));
                        pointId++;
                    }
                    if (pointId % 250 == 0)
                    {
                        Console.WriteLine("Finished processing border " + pointId + " points." + " Current province id: " + possibleProvinceId);

                    }
                }

                StringBuilder batchBorderPointInsert = new StringBuilder("INSERT INTO Points (x,y,provinceId) VALUES ");
                //Finish the insert statement
                batchBorderPointInsert.Append(string.Join(",", batchBorderPointInsertRows));
                batchBorderPointInsert.Append(";");
                //Insert all of the white points into the DB

                database.runStringNonQueryCommand(batchBorderPointInsert.ToString());

                return pointId;


            } catch(Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        
        //Insert ocean points to DB
        private static int insertWaterPointsToDB(DBConnection database, List<(int, int)> waterPoints, decimal salinity, string type, int pointId)
        {
            try
            {
                List<string> batchWaterPointInsertRows = new List<string>();
                //Now that all land points have been added to the DB, we can add the ocean/lake points
                foreach ((int, int) point in waterPoints)
                {
                    batchWaterPointInsertRows.Add(string.Format("({0},{1},{2},{3},{4})", MySqlHelper.EscapeString(point.Item1.ToString()), MySqlHelper.EscapeString(point.Item2.ToString()), MySqlHelper.EscapeString(salinity.ToString()), "'" + MySqlHelper.EscapeString(type) + "'", MySqlHelper.EscapeString("false")));
                }

                StringBuilder batchOceanPointInsert = new StringBuilder("INSERT INTO Points (x,y,waterSalinity,type,land) VALUES ");
                //Finish the insert statement
                batchOceanPointInsert.Append(string.Join(",", batchWaterPointInsertRows));
                batchOceanPointInsert.Append(";");
                //Insert all of the white points into the DB


                database.runStringNonQueryCommand(batchOceanPointInsert.ToString());

                return pointId;


            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
        }

        //Pull a random neighboring province to assign the unallocated point to.
        public static bool assignUnallocatedPoints(DBConnection database, (int, int) point)
        {
            string query = "SELECT x,y,waterSalinity,type,provinceId FROM Points WHERE (x = (@x + 1) AND y = @y) OR (x = (@x - 1) AND y = @y) OR (x = @x AND y = (@y + 1)) OR (x = @x AND y = (@y - 1)) ORDER BY RAND() LIMIT 1;";

            var cmd = new MySqlCommand(query, database.Connection);
            cmd.Parameters.AddWithValue("@x", point.Item1);
            cmd.Parameters.AddWithValue("@y", point.Item2);

            MySqlDataReader rdr = cmd.ExecuteReader();
            int x = -1;
            int y = -1;
            decimal waterSalinity = -1;
            string type = null;
            int provinceId = -1;

            List<int> provinces = new List<int>();

            while (rdr.Read())
            {

                x = Int32.Parse(rdr[0].ToString());
                y = Int32.Parse(rdr[1].ToString());
                decimal.TryParse(rdr[2].ToString(), out waterSalinity);
                type = rdr[3].ToString();
                Int32.TryParse(rdr[4].ToString(),out provinceId);
            }
            rdr.Close();

            switch (type)
            {
                case "ocean":
                    var oceanPointCmd = new MySqlCommand(oceanPointInsertQuery, database.Connection);
                    oceanPointCmd.Parameters.AddWithValue("@x", point.Item1);
                    oceanPointCmd.Parameters.AddWithValue("@y", point.Item2);
                    oceanPointCmd.ExecuteNonQuery();
                    oceanPointCmd.Parameters.Clear();
                    return true;
                    break;
                case "land":
                    var pointCmd = new MySqlCommand(pointInsertQuery, database.Connection);
                    pointCmd.Parameters.AddWithValue("@x", point.Item1);
                    pointCmd.Parameters.AddWithValue("@y", point.Item2);
                    pointCmd.Parameters.AddWithValue("@provinceId", provinceId);
                    pointCmd.ExecuteNonQuery();
                    pointCmd.Parameters.Clear();
                    return true;
                case "lake":
                    var lakePointCmd = new MySqlCommand(lakePointInsertQuery, database.Connection);
                    lakePointCmd.Parameters.AddWithValue("@x", point.Item1);
                    lakePointCmd.Parameters.AddWithValue("@y", point.Item2);
                    lakePointCmd.ExecuteNonQuery();
                    lakePointCmd.Parameters.Clear();
                    return true;
                default:
                    return false;
            }
        }

        //This function will populate the borders of each province in the DB
        public static void populateEdgesTable(DBConnection database)
        {

            Console.WriteLine("Begin processing edges: " + DateTime.UtcNow.ToString());
            // Truncate the renderEdges table
            var truncateCmd = new MySqlCommand(truncateRenderEdges, database.Connection);
            truncateCmd.ExecuteNonQuery();

            List<int> provinces = Province.getListOfProvinces(database);

            string borderQuery = "SELECT x, y FROM borderPoints WHERE centerProvince = @provinceId;";
            string provinceQuery = "SELECT x, y FROM Points WHERE provinceId = @provinceId;";

            for(int i = 0; i < provinces.Count; i++)
            {
                var cmd = new MySqlCommand(borderQuery, database.Connection);
                cmd.Parameters.AddWithValue("@provinceId", provinces[i]);
                MySqlDataReader rdr = cmd.ExecuteReader();

                //Store each id, x, and y in a list
                //These are the borders for your province
                List<(int, int)> borderPoints = new List<(int, int)>();
                while (rdr.Read())
                {
                    borderPoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
                }
                rdr.Close();

                var allPointsCmd = new MySqlCommand(provinceQuery, database.Connection);
                allPointsCmd.Parameters.AddWithValue("@provinceId", provinces[i]);
                rdr = allPointsCmd.ExecuteReader();

                //Store all points of a province in a hashset so you can reference it to check if a point is bordered on that side or not
                HashSet<(int, int)> provincePoints = new HashSet<(int, int)>();
                while (rdr.Read())
                {
                    provincePoints.Add((rdr.GetInt32(0), rdr.GetInt32(1)));
                }
                rdr.Close();

                foreach ((int, int) point in borderPoints)
                {
                    writeProvinceEdges(point, provincePoints, provinces[i], database);
                }
                Console.WriteLine("Finished province " + provinces[i]);
            }

            Console.WriteLine("Finished populating edges: " + DateTime.UtcNow.ToString());

        }

        //point is the point for which you are writing edges to the database
        //province points is a hash set that contains all points in the province
        //provinceid is the id for the province which the point is running in
        //database is the connection to the database
        //This function runs through a point, checks the point on each sides, and calls a function to write the edge to the database
        private static void writeProvinceEdges((int,int)point, HashSet<(int, int)> provincePoints,int provinceId, DBConnection database)
        {
            //Get the neighbors of the point
            //0 - West
            // 1 - East
            // 2 - North
            // 3 - South
            var neighbors = Point.getNeighborsPlus(point);
            //Check the western point
            //x1 y1 vs x2 y2 should be in counter clockwise order
            if (!provincePoints.Contains(neighbors[0]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.topLeft), convertPointToGridPoint(point, gridOrientation.bottomLeft),provinceId,database);
            }
            if (!provincePoints.Contains(neighbors[1]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.bottomRight), convertPointToGridPoint(point, gridOrientation.topRight), provinceId, database);
            }
            if (!provincePoints.Contains(neighbors[2]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.topRight), convertPointToGridPoint(point, gridOrientation.topLeft), provinceId, database);
            }
            if (!provincePoints.Contains(neighbors[3]))
            {
                writeEdgeToDB(convertPointToGridPoint(point, gridOrientation.bottomLeft), convertPointToGridPoint(point, gridOrientation.bottomRight), provinceId, database);
            }
        }

        private static void writeEdgeToDB((decimal, decimal) p1, (decimal, decimal) p2, int provinceId, DBConnection database)
        {

            var edgeCmd = new MySqlCommand(edgeInsertQuery, database.Connection);
            edgeCmd.Parameters.AddWithValue("@x1", p1.Item1);
            edgeCmd.Parameters.AddWithValue("@y1", p1.Item2);

            edgeCmd.Parameters.AddWithValue("@x2", p2.Item1);
            edgeCmd.Parameters.AddWithValue("@y2", p2.Item2);


            edgeCmd.Parameters.AddWithValue("@provinceId", provinceId);

            edgeCmd.ExecuteNonQuery();
        }

        //We want to start looking for patterns at a top left corner
        //This is defined by a point with nothing north and nothing east of it
        //TODO:Handle case where there is no top left corner
        private static (int, int) findStartingPoint(HashSet<(int, int)> borderPoints, HashSet<(int,int)> provincePoints)
        {
            var startingPoint = borderPoints.First();
            Console.WriteLine(startingPoint.ToString() + " is used to start iteration.");
            //While creating the border, start at a point in the province where there is no valid point north of it and no valid point east of it
            bool topRight = false;
            while (!topRight)
            {


                Console.WriteLine("Checking " + startingPoint.ToString() + ".");

                //Get possible neighbors for the borders
                var possiblePoints = Point.getNeighborsPlus((startingPoint.Item1, startingPoint.Item2));
                Console.WriteLine("Center point...\nBorder point: " + borderPoints.Contains(startingPoint) + ", Province point: " + provincePoints.Contains(startingPoint) + "\nOther points...\n" );
                foreach ((int, int) point in possiblePoints)
                {
                    Console.WriteLine("Border point: " + borderPoints.Contains(point) + ", Province point: " + provincePoints.Contains(point));
                }
               

                //Check if the point north of it is not in the province
                if (!borderPoints.Contains(possiblePoints[2]))
                {
                    //Check if the point to the east of it is not in the province
                    if (!borderPoints.Contains(possiblePoints[1]))
                    {
                        //Check if point north of it is in the province and diagonal to the left is a border province
                        if (provincePoints.Contains(possiblePoints[2]) && borderPoints.Contains((startingPoint.Item1 - 1, startingPoint.Item2 - 1))){
                            startingPoint = (startingPoint.Item1 - 1, startingPoint.Item2 - 1);
                            Console.WriteLine("Moving diagonally to the left.\n");
                        }
                        else
                        {
                            //Check if the point east of it is in the province and diagonal to the right is a border provice
                            if (provincePoints.Contains(possiblePoints[1]) && borderPoints.Contains((startingPoint.Item1 + 1, startingPoint.Item2 - 1)))
                            {
                                startingPoint = (startingPoint.Item1 + 1, startingPoint.Item2 - 1);
                                Console.WriteLine("Moving diagonally to the right.\n");
                            }
                            else
                            {
                                //If all possibilities are elimnated we have found the top left.
                                Console.WriteLine("Identified top right.\n");
                                topRight = true;
                            }
                        }
                    }
                    else
                    {
                        //If the point to the right is in the province, then we must move to the east
                        startingPoint = possiblePoints[1];
                        Console.WriteLine("Moving to the right.\n");
                    }
                }
                else
                {
                    //If the point above is in the province, then we must move up north
                    startingPoint = possiblePoints[2];
                    Console.WriteLine("Moving up.\n");
                }
            }
            return startingPoint;
        }

        //Query the DB for provinces and render each as a different color, using the base image as a template
        public static void renderProvinces(DBConnection database, string inputPath,string outputPath)
        {
            Random r = new Random();
            SixLabors.ImageSharp.Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath);

            List<(int,int,int)> provinces = Point.getListOfAllPointsWithProvinces(database);
            int provinceId = 0;
            int R = r.Next(0, 255);
            int G = r.Next(0, 255);
            int B = r.Next(0, 255);
            for (int i = 0; i < provinces.Count; i++)
            {
                if (provinces[i].Item1 != provinceId)
                {
                    provinceId = provinces[i].Item1;
                    R = r.Next(0, 255);
                    G = r.Next(0, 255);
                    B = r.Next(0, 255);
                }
                image[provinces[i].Item2, provinces[i].Item3] = new Rgba32((byte)R, (byte)G, (byte)B);
                if(i%100 == 0)
                {
                    Console.WriteLine("Finished rendering " + i + " points");
                }
            }

            List<(int, int)> oceanPoints = Point.getListOfAllOceanPointsWithProvinces(database);
            for(int i = 0; i < oceanPoints.Count; i++)
            {
                image[oceanPoints[i].Item1, oceanPoints[i].Item2] = Rgba32.ParseHex("00FFFF");
            }

            List<(int,int)> lakePoints = Point.getListOfAllLakePointsWithProvinces(database);
            for (int i = 0; i < lakePoints.Count; i++)
            {
                image[lakePoints[i].Item1, lakePoints[i].Item2] = Rgba32.ParseHex("0000FF");
            }


            image.SaveAsPng(outputPath);
        }

        //Convert a point into a grid point
        //A grid point is the cross point of 4 points
        //[0] is the xLeft,[1] is the xRight,[2] is the yUp,[3] is the yDown
        private static (decimal, decimal) convertPointToGridPoint((int, int) point,gridOrientation orientation)
        {

            decimal shift = .5m;
            switch (orientation)
            {
                case gridOrientation.topLeft:
                    return ((Convert.ToDecimal(point.Item1)-shift), (Convert.ToDecimal(point.Item2) - shift));
                    break;
                case gridOrientation.topRight:
                    return  ((Convert.ToDecimal(point.Item1) + shift), (Convert.ToDecimal(point.Item2) - shift));
                    break;
                case gridOrientation.bottomLeft:
                    return  ((Convert.ToDecimal(point.Item1) - shift), (Convert.ToDecimal(point.Item2) + shift));
                    break;
                case gridOrientation.bottomRight:
                    return ((Convert.ToDecimal(point.Item1) + shift), (Convert.ToDecimal(point.Item2) + shift));
                    break;
                case gridOrientation.topMiddle:
                    return ((Convert.ToDecimal(point.Item1)), (Convert.ToDecimal(point.Item2) - shift));
                    break;
                case gridOrientation.bottomMiddle:
                    return ((Convert.ToDecimal(point.Item1)), (Convert.ToDecimal(point.Item2) + shift));
                    break;
                case gridOrientation.leftMiddle:
                    return ((Convert.ToDecimal(point.Item1) - shift), (Convert.ToDecimal(point.Item2)));
                    break;
                case gridOrientation.rightMiddle:
                    return ((Convert.ToDecimal(point.Item1) + shift), (Convert.ToDecimal(point.Item2)));
                    break;
                default:return (-1m,-1m);
            }

        }


        private static string getFormattedCommandText(MySqlCommand command)
        {
            string query = command.CommandText;
            foreach (MySqlParameter parameter in command.Parameters)
            {
                query = query.Replace(parameter.ParameterName, parameter.Value.ToString());
            }
            return query;
        }

       
    }
}
