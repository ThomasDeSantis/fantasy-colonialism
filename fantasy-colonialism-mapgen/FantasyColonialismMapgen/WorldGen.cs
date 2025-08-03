using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Npgsql;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FantasyColonialismMapgen
{
    class WorldGen
    {
        SixLabors.ImageSharp.Image<Rgba32> image;
        int height;
        int width;

        Rgba32 pointColor;
        Rgba32 oceanColor;

        int mainViewTopLeftX;
        int mainViewTopLeftY;
        int mainViewHeight;
        int mainViewWidth;

        public WorldGen(string bmpPath, IConfiguration config)
        {
            image = SixLabors.ImageSharp.Image.Load<Rgba32>(bmpPath);
            height = image.Height;
            width = image.Width;

            pointColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapPoint"));
            oceanColor = Rgba32.ParseHex(config.GetValue<string>("MapgenStrings:BaseMapOceanPoint"));

            mainViewTopLeftX = config.GetValue<int>("ImageSettings:MapTopLeftWidth");
            mainViewTopLeftY = config.GetValue<int>("ImageSettings:MapTopLeftHeight");
            mainViewHeight = config.GetValue<int>("ImageSettings:ViewHeight");
            mainViewWidth = config.GetValue<int>("ImageSettings:ViewWidth");
        }


        public void populatePointsAndWorldPointsFromImage(string parentDirectory, DBConnection database, IConfiguration config)
        {
            Console.WriteLine($"Begin processing image into world points: {DateTime.UtcNow.ToString()}");
            processImageIntoWorldPoints(parentDirectory+"\\Maps\\land-map.png", database, config);
            Console.WriteLine($"Finished processing image into world points: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin rendering world map points: {DateTime.UtcNow.ToString()}");
            renderWorldPointsAsImage(parentDirectory+"\\Maps\\world-output.png", database, config);
            Console.WriteLine($"Finished rendering world map points: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin loading view points from DB: {DateTime.UtcNow.ToString()}");
            loadViewPointTableFromDB(database,config);
            Console.WriteLine($"Finished loading view points from DB: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin rendering view points as image: {DateTime.UtcNow.ToString()}");
            renderViewPointsAsImage(parentDirectory + "\\Maps\\view-output.png", database, config);
            Console.WriteLine($"Finished rendering view points as image: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin populating latitude and longitude for world points: {DateTime.UtcNow.ToString()}");
            populateLatitudeLongitudeWorldPoints(database, config);
            Console.WriteLine($"Finished populating latitude and longitude for world points: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin populating latitude and longitude for view points: {DateTime.UtcNow.ToString()}");
            populateLatitudeLongitudeDimensionsPoints(database,config);
            Console.WriteLine($"Finished populating latitude and longitude for view points: {DateTime.UtcNow.ToString()}");
            Console.WriteLine("Begin setting coastal provinces: " + DateTime.UtcNow.ToString());
            setCoastalPoints(database);
            Console.WriteLine("End setting coastal provinces: " + DateTime.UtcNow.ToString());
            Console.WriteLine($"Begin assigning distance to coast for points: {DateTime.UtcNow.ToString()}");
            assignDistanceToCoast(database, config);
            Console.WriteLine($"Finished assigning distance to coast for points: {DateTime.UtcNow.ToString()}");
        }

        private void processImageIntoWorldPoints(string inputPath, DBConnection database, IConfiguration config)
        {
            //Check current max_allowed_packet


            database.runStringNonQueryCommand("CALL sp_TRUNCATE_POINTS();");
            Console.WriteLine($"Image processing began: {DateTime.UtcNow.ToString()}");
            int pointId = 0; //Used 

            List<string> batchWorldPointInsertRow = new List<string>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pointId % 200000 == 0)
                    {
                        if (batchWorldPointInsertRow.Count > 0)
                        {
                            Console.WriteLine($"Finished processing world point {pointId} points.");
                            StringBuilder batchWorldPointInsert = new StringBuilder("INSERT INTO \"WorldPoints\" (x,y,land) VALUES ");
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
                        throw new Exception($"Invalid image. Point identified at {x.ToString()}, {y.ToString()} as {image[x, y].ToString()}. Image must only contain pixels of color {config.GetValue<string>("MapgenStrings:BaseMapPoint")}" +
                            $" (land) and {config.GetValue<string>("MapgenStrings:BaseMapOceanPoint")} (water).");
                    }
                    pointId++;
                }
            }

            StringBuilder batchWorldPointInsertFinal = new StringBuilder("INSERT INTO \"WorldPoints\" (x,y,land) VALUES ");
            //Finish the insert statement
            batchWorldPointInsertFinal.Append(string.Join(",", batchWorldPointInsertRow));
            batchWorldPointInsertFinal.Append(";");
            //Insert all of the white points into the DB
            Console.WriteLine(batchWorldPointInsertFinal.Length);
            database.runStringNonQueryCommand(batchWorldPointInsertFinal.ToString());
            
        }

        private void renderWorldPointsAsImage(string outputPath, DBConnection database, IConfiguration config)
        {
            Console.WriteLine("Height: " + height + " Width: " + width);
        

            // Load the height map from the database assuming all points are ocean
            var worldMap = new bool[height][];
            for (int i = 0; i < height; i++)
            {
                worldMap[i] = new bool[width];
                for (int j = 0; j < width; j++)
                {
                    worldMap[i][j] = false;
                }
            }

            //Query the database for all land points
            string query = "SELECT x, y, land FROM \"WorldPoints\" WHERE land = true;";
            NpgsqlDataReader reader = database.runQueryCommand(query);
            while (reader.Read())
            {
                int x = reader.GetInt32(0);
                int y = reader.GetInt32(1);
                bool land = reader.GetBoolean(2);
                if (land)
                {
                    worldMap[y][x] = true;
                }
            }
            reader.Close();

            Image<Rgba32> output = new Image<Rgba32>(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (worldMap[y][x])
                    {
                        output[x, y] = pointColor; // Land points
                    }
                    else
                    {
                        output[x, y] = oceanColor; // Ocean points
                    }
                }
            }
            // Save the rendered image to the specified output path
            output.Save(outputPath);

            return;
        }

        public void renderViewPointsAsImage(string outputPath, DBConnection database, IConfiguration config)
        {

            int pointWidth = 0;
            int pointHeight = 0;

            //Get width/height rather than using the config dimensions to ensure it is the truest representation of the table
            string query = "SELECT MAX(x) as width, MAX(y) as height FROM \"Points\";";
            NpgsqlDataReader reader = database.runQueryCommand(query);

            if (reader.Read())
            {
                pointWidth = reader.GetInt32(0) + 1;
                pointHeight = reader.GetInt32(1) + 1;
            }

            reader.Close();

            Console.WriteLine("Height: " + pointHeight + " Width: " + pointWidth);

            // Load the height map from the database assuming all points are ocean
            var pointMap = new bool[pointHeight][];
            for (int i = 0; i < pointHeight; i++)
            {
                pointMap[i] = new bool[pointWidth];
                for (int j = 0; j < pointWidth; j++)
                {
                    pointMap[i][j] = false;
                }
            }

            //Query the database for all land points
            string queryPoints = "SELECT x, y, land FROM \"Points\" WHERE land = true;";
            NpgsqlDataReader readerPoints = database.runQueryCommand(queryPoints);
            while (readerPoints.Read())
            {
                int x = readerPoints.GetInt32(0);
                int y = readerPoints.GetInt32(1);
                bool land = readerPoints.GetBoolean(2);
                if (land)
                {
                    pointMap[y][x] = true;
                }
            }
            readerPoints.Close();

            Image<Rgba32> output = new Image<Rgba32>(pointWidth, pointHeight);

            for (int y = 0; y < pointHeight; y++)
            {
                for (int x = 0; x < pointWidth; x++)
                {
                    if (pointMap[y][x])
                    {
                        output[x, y] = pointColor; // Land points
                    }
                    else
                    {
                        output[x, y] = oceanColor; // Ocean points
                    }
                }
            }
            // Save the rendered image to the specified output path
            output.Save(outputPath);

            return;
        }

        public void loadViewPointTableFromDB(DBConnection database,IConfiguration config)
        {
            

            //Query the database for all points in the view window
            //This is used to populate the points in the image.
            string query = $"SELECT x, y,id, land FROM \"WorldPoints\" WHERE x >= {mainViewTopLeftX} AND x < {mainViewTopLeftX + mainViewWidth} AND y >= {mainViewTopLeftY} AND y < {mainViewTopLeftY + mainViewHeight};";
            
            List<(int,int,int,bool)> points = new List<(int, int,int, bool)>();
            NpgsqlDataReader reader = database.runQueryCommand(query);

            while(reader.Read())
                {
                int x = reader.GetInt32(0);
                int y = reader.GetInt32(1);
                int id = reader.GetInt32(2);
                bool land = reader.GetBoolean(3);
                points.Add((x, y, id, land));
                //batchViewPointInsertRow.Add(string.Format("({0},{1},{2})", MySqlHelper.EscapeString(x.ToString()), MySqlHelper.EscapeString(y.ToString()), land));
            }


            //This wil be used to load in the view points
            StringBuilder batchViewPointInsert = new StringBuilder("INSERT INTO \"Points\" (x,y,worldPointId,land,waterSalinity) VALUES ");
            //This will hold the individual insert statements for the points to be inserted into the database.
            List<string> batchViewPointInsertRow = new List<string>();

            int xOffset = config.GetValue<int>("ImageSettings:MapTopLeftWidth");
            int yOffset = config.GetValue<int>("ImageSettings:MapTopLeftHeight");

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                float salinity = -1f;
                if (!points[i].Item4)
                {
                    salinity = 3.5f;
                }
                batchViewPointInsertRow.Add(string.Format("({0},{1},{2},{3},NULLIF({4},-1))", (point.Item1-xOffset).ToString(),(point.Item2-yOffset).ToString(), point.Item3.ToString(),point.Item4, salinity));
            }
            database.runStringNonQueryCommandBatchInsert("INSERT INTO \"Points\" (x,y,worldPointId,land,waterSalinity) VALUES ", batchViewPointInsertRow, 20000, true);
        }

        //Populate the latitude longitude for a mercator projection
        //Latitude is within the range of -85.051 to +85.051
        //Longitude is -180.0 to +180.0
        public static void populateLatitudeLongitudeWorldPoints(DBConnection database,IConfiguration config)
        {
            //Will be used for calculating mercator coords
            double maxX = config.GetValue<double>("ImageSettings:WorldWidth") - 1;
            double maxY = config.GetValue<double>("ImageSettings:WorldHeight") - 1;

            string query = "SELECT id, x, y FROM \"WorldPoints\" order by id asc;";
            NpgsqlDataReader reader = database.runQueryCommand(query);
            List<string> batchUpdateCommands = new List<string>();
            Dictionary<int,double> yToLatitude = new Dictionary<int, double>();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                int x = reader.GetInt32(1);
                int y = reader.GetInt32(2);
                //Calculate latitude and longitude based on the x and y coordinates, given the map is a mercator projection
                double latitude;
                if(yToLatitude.ContainsKey(y))
                {
                    //If we have already calculated the latitude for this y coordinate, use it
                    latitude = yToLatitude[y];
                }
                else
                {
                    //Calculate the latitude for this y coordinate
                    double yPi = ((double)y * Math.PI * 2.0) / maxY; // Convert y to a range of 0 to 2*PI
                    yPi -= Math.PI; // Shift to range of -PI to PI
                    yPi *= -1.0; // Invert the y coordinate for mercator projection
                    latitude = GetLatitude(yPi); 
                    yToLatitude[y] = latitude;
                }
                double longitude = ((360.0 * x)/maxX) - 180.0; 
                //Prepare the update command
                batchUpdateCommands.Add($"UPDATE \"WorldPoints\" SET latitude = {latitude}, longitude = {longitude} WHERE id = {id}");
            }
            reader.Close();
            //Run the batch update commands
            if (batchUpdateCommands.Count > 0)
            {
                database.runStringNonQueryCommandBatch("", "", batchUpdateCommands, 2500, ';', true);
            }
        }

        //https://stackoverflow.com/questions/1166059/how-can-i-get-latitude-longitude-from-x-y-on-a-mercator-map-jpeg
        //http://en.wikipedia.org/wiki/Gudermannian_function
        //Returns the Latitude in degrees for a given Y.
        //Y is in the range of +PI to -PI.
        public static double GetLatitude(double y)
        {
            //57.2958 is the conversion factor from radians to degrees
            return Math.Atan(Math.Sinh(y)) * 57.2958;
        }

        public static void populateLatitudeLongitudeDimensionsPoints(DBConnection database, IConfiguration config)
        {
            string query = "SELECT p1.id,p2.latitude,p2.longitude, p1.y from \"Points\" p1 JOIN \"WorldPoints\" p2 on p2.id = p1.worldpointid;";
            int maxY = config.GetValue<int>("ImageSettings:WorldHeight") - 1;
            int maxX = config.GetValue<int>("ImageSettings:WorldWidth") - 1;
            int metersLength = 12756000 / maxX; //As this is a mercator projection, each point will have a constant width. 12767000 is the diameter of the earth, and when we divide by maxX we will get the exact width of each point in meters.
            NpgsqlDataReader reader = database.runQueryCommand(query);
            List<string> batchUpdateCommands = new List<string>();
            Dictionary<int, double> yToLatitudeRateOfChange = new Dictionary<int, double>();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                double latitude = reader.GetDouble(1);
                double longitude = reader.GetDouble(2);
                int y = reader.GetInt32(3);


                double latROC = 0;
                if (!yToLatitudeRateOfChange.ContainsKey(y))
                {
                    double latMinus1 = -91;
                    double latPlus1 = -91;//-361 indicates failure

                    if (y - 1 >= 0)
                    {
                        double yPi = ((double)(y - 1) * Math.PI * 2.0) / maxY;
                        yPi -= Math.PI; // Shift to range of -PI to PI
                        yPi *= -1.0; // Invert the y coordinate for mercator projection
                        latMinus1 = GetLatitude(yPi);
                    }
                    if (y + 1 <= maxY)
                    {
                        double yPi = ((double)(y + 1) * Math.PI * 2.0) / maxY;
                        yPi -= Math.PI; // Shift to range of -PI to PI
                        yPi *= -1.0; // Invert the y coordinate for mercator projection
                        latPlus1 = GetLatitude(yPi);
                    }

                    double latROC1 = 0;
                    double latROC2 = 0;
                    if (latMinus1 != -91 && latPlus1 != -91)
                    {
                        latROC1 = Math.Abs(Math.Abs(latitude) - Math.Abs(latMinus1));
                        latROC2 = Math.Abs(Math.Abs(latitude) - Math.Abs(latPlus1));
                        latROC = (latROC1 + latROC2) / 2.0;
                    }
                    else if (latMinus1 != -91 && latPlus1 == -91)
                    {
                        latROC = Math.Abs(Math.Abs(latitude) - Math.Abs(latMinus1));
                    }
                    else if (latPlus1 != -91 && latMinus1 == -91)
                    {
                        latROC = Math.Abs(Math.Abs(latitude) - Math.Abs(latPlus1));
                    }
                    yToLatitudeRateOfChange[y] = latROC;
                }
                else
                {
                    //If we have already calculated the latitude rate of change for this y coordinate, use it
                    latROC = yToLatitudeRateOfChange[y];
                }

                int metersWidth = (int)((latROC * 12756000) / 180); //Calculate the width of the point in meters based on the latitude rate of change. 12756000 is the diameter of the earth, and when we divide by 180 we will get the exact width of each point in meters.

                //Prepare the update command
                batchUpdateCommands.Add($"UPDATE \"Points\" SET latitude = {latitude}, longitude = {longitude}, length = {metersLength}, width = {metersWidth}, area = {metersLength * metersWidth} WHERE id = {id}");
            }
            reader.Close();
            //Run the batch update commands
            if (batchUpdateCommands.Count > 0)
            {
                database.runStringNonQueryCommandBatch("", "", batchUpdateCommands, 2500, ';', true);
            }
        }

        //Populate the distance to coast column in the Points table
        //The distance is the distance in kilometers to the nearest coast point.
        //This is calculated using the latitude and longitude of the point
        //Slightly off as the latitude and longitude are within the province, not necessarily at the coastal border
        //TODO: Account for this
        //Could make more efficent by taking into account already calculated distances
        public static void assignDistanceToCoast(DBConnection db, IConfiguration config)
        {
            //First query all points that are coastal
            string queryCoastal = "SELECT p1.id,p1.latitude,p1.longitude FROM \"Points\" p1 JOIN \"WorldPoints\" p2 ON p1.worldpointid = p2.id WHERE p2.coastal = true;";
            NpgsqlDataReader readerCoastal = db.runQueryCommand(queryCoastal);
            List<Point> coastalPoints = new List<Point>();
            while (readerCoastal.Read())
            {
                //Add the coastal point to a list containing all coastal points
                coastalPoints.Add(new Point(readerCoastal.GetInt32(0),readerCoastal.GetDouble(1),readerCoastal.GetDouble(2)));
            }
            readerCoastal.Close();

            //Apply this for all points.
            string queryNonCoastal = "SELECT id,latitude,longitude FROM \"Points\";";
            NpgsqlDataReader readerNonCoastal = db.runQueryCommand(queryNonCoastal);
            List<string> batchUpdateCommands = new List<string>();
            int c = 0;
            while (readerNonCoastal.Read())
            {
                if(c % 1000 == 0)
                {
                    Console.WriteLine($"Processed {c} points for distance to coast.");
                }
                int id = readerNonCoastal.GetInt32(0);
                double latitude = readerNonCoastal.GetDouble(1);
                double longitude = readerNonCoastal.GetDouble(2);

                Point.findClosestPoint(latitude, longitude, coastalPoints, out Point closest, out double distance);

                batchUpdateCommands.Add($"UPDATE \"Points\" SET coastalDistance = {distance}, closestCoastalPoint = {closest.Id} WHERE id = {id}");
                c++;
            }
            readerNonCoastal.Close();

            //Run the batch update commands
            if (batchUpdateCommands.Count > 0)
            {
                db.runStringNonQueryCommandBatch("", "", batchUpdateCommands, 2500, ';', true);
            }
        }

        private static void setCoastalPoints(DBConnection db)
        {
            // Create a dictionary to store coastal points
            var points = new Dictionary<(int x, int y), (bool, int)>();

            // Query all points from the database
            string query = "SELECT x, y, land, id FROM \"WorldPoints\";";
            NpgsqlDataReader reader = db.runQueryCommand(query);

            bool first = true;

            // Populate the dictionary with x, y as key and land status as value
            while (reader.Read())
            {
                int x = reader.GetInt32(0);
                int y = reader.GetInt32(1);
                bool isLand = reader.GetBoolean(2);
                int id = reader.GetInt32(3);
                points[(x, y)] = (isLand, id);
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
                            batchCoastalUpdateRow.Add(string.Format("UPDATE \"WorldPoints\" SET coastal = true WHERE id = {0}", MySqlHelper.EscapeString(points[point].Item2.ToString())));
                            break;
                        }
                    }
                }
            }

            db.runStringNonQueryCommandBatch("", "", batchCoastalUpdateRow, 1000, ';', true);
        }

        public static void renderCoastline(DBConnection db, string coastLineMapPath, int height, int width)
        {
            string query = "SELECT x, y, coastal, land FROM \"WorldPoints\"";
            NpgsqlDataReader reader = db.runQueryCommand(query);
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
                        image[x, y] = Rgba32.ParseHex("#0000FF"); // Water color
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

    }
}
