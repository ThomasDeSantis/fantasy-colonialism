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
            loadViewPointTableFromDB(database);
            Console.WriteLine($"Finished loading view points from DB: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin rendering view points as image: {DateTime.UtcNow.ToString()}");
            renderViewPointsAsImage(parentDirectory + "\\Maps\\view-output.png", database, config);
            Console.WriteLine($"Finished rendering view points as image: {DateTime.UtcNow.ToString()}");
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
            string query = "SELECT MAX(x) - MIN(x) as width, MAX(y) - MIN(y) as height FROM \"Points\";";
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
                int x = readerPoints.GetInt32(0) - mainViewTopLeftX;
                int y = readerPoints.GetInt32(1) - mainViewTopLeftY;
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

        public void loadViewPointTableFromDB(DBConnection database)
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

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                float salinity = -1f;
                if (!points[i].Item4)
                {
                    salinity = 3.5f;
                }
                batchViewPointInsertRow.Add(string.Format("({0},{1},{2},{3},NULLIF({4},-1))", MySqlHelper.EscapeString(point.Item1.ToString()), MySqlHelper.EscapeString(point.Item2.ToString()), MySqlHelper.EscapeString(point.Item3.ToString()),point.Item4, salinity));
                /*if (i % 20000 == 0 && i > 0)
                {
                    Console.WriteLine($"Finished processing view point {i} points."); 
                    //Finish the insert statement
                    batchViewPointInsert.Append(string.Join(",", batchViewPointInsertRow));
                    batchViewPointInsert.Append(";");
                    //Insert all of the points into the DB
                    database.runStringNonQueryCommand(batchViewPointInsert.ToString());
                    batchViewPointInsert.Clear();
                    batchViewPointInsert.Append("INSERT INTO \"Points\" (x,y,worldPointId,land,waterSalinity) VALUES ");
                    batchViewPointInsertRow.Clear();
                }*/
            }
            database.runStringNonQueryCommandBatchInsert("INSERT INTO \"Points\" (x,y,worldPointId,land,waterSalinity) VALUES ", batchViewPointInsertRow, 20000, true);
        }
    }
}
