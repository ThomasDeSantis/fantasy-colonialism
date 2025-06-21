using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SixLabors.ImageSharp;

using SixLabors.ImageSharp.PixelFormats;
using MySql.Data.MySqlClient;

namespace FantasyColonialismMapgen
{
    class BiomeGen
    {
        int heightMin;
        int heightMax;
        int height;

        int widthMin;
        int widthMax;
        int width;

        biomePoint[][] biomeMap;
        string parentDirectory;

        public BiomeGen(DBConnection db, IConfiguration config, string parentDirectoryI)
        {
            heightMin = (db.getIntFromQuery("SELECT MIN(y) FROM \"Points\";"));
            heightMax = (db.getIntFromQuery("SELECT MAX(y) FROM \"Points\";"));
            height = heightMax - heightMin + 1;

            widthMin = (db.getIntFromQuery("SELECT MIN(x) FROM \"Points\";"));
            widthMax = (db.getIntFromQuery("SELECT MAX(x) FROM \"Points\";"));
            width = widthMax - widthMin + 1;

            biomeMap = getBiomePointArray(db);

            parentDirectory = parentDirectoryI;
        }

        public void generateBiomes(DBConnection db)
        {
            Console.WriteLine($"Begin assigning heat maps: {DateTime.UtcNow.ToString()}");
            assignHeatMap();
            Console.WriteLine($"Finished assigning heat maps: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin rendering heat maps: {DateTime.UtcNow.ToString()}");
            renderHeatMap(true, "heatmap_summer.png");
            renderHeatMap(false, "heatmap_winter.png");
            Console.WriteLine($"Finished rendering heat maps: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin writing temperatures to DB: {DateTime.UtcNow.ToString()}");
            writeTempsToDB(db);
            Console.WriteLine($"Finished writing temperatures to DB: {DateTime.UtcNow.ToString()}");
        }

        //Currently a stub method
        //Final version will use climate simulation logic
        public void assignHeatMap()
        {
            //Loop through the biome map and assign the temperature
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    //We must consider the following

                    //A base height with the following formula, T(ϕ,d) = Tequator - ΔT * (ϕ / 90)^n + S(ϕ) * cos((2π(d−ds)/365)
                    double lat = biomeMap[i][j].latitude;//ϕ: Latitude (° from –90 to +90)
                    //d: Day of the year (1 to 354), year is slightly shorter to allow for 12 months on a lunar calendar
                    double Teq = 31.0;//Tequator: Average temperature at the equator (31 C)
                    double deltaT = 50.0;//ΔT: Temperature difference between the equator and the poles (50 C)
                    double n = 1.5;//n: Falloff exponent to control the temperature drop from poles (1.5)
                    double ds = 167;//ds: Day of the year at the summer solstice (167) or winter solstice (344)
                    //S(ϕ): Seasonal amplitude function. S(ϕ) = A * (∣ϕ∣/90)^m * sign(ϕ)
                    double a = 20.0;//A: Maxmum seasonal swing at poles (+-20 C)
                    double m = 1.2;//m: Exponent controlling how sharply seasonal effect increases toward poles (1.2)
                    double signLat = biomeMap[i][j].latitude >= 0 ? 1 : -1;//sign(ϕ): +1 for northern hemisphere, -1 for southern hemisphere

                    double latFactor = Math.Pow(Math.Abs(lat) / 90.0, n);
                    double seasonalFactorSummer = Math.Cos(2 * Math.PI * (167 - ds) / 354.0);
                    double seasonalFactorWinter = Math.Cos(2 * Math.PI * (344 - ds) / 354.0);

                    double seasonalAmplitudeSummer = Math.Sign(lat) * Math.Pow(Math.Abs(lat) / 90.0, m) * a * seasonalFactorSummer;
                    double seasonalAmplitudeWinter = Math.Sign(lat) * Math.Pow(Math.Abs(lat) / 90.0, m) * a * seasonalFactorWinter;

                    double baseTemperatureSummer = Teq - deltaT * latFactor + seasonalAmplitudeSummer;
                    double baseTemperatureWinter = Teq - deltaT * latFactor + seasonalAmplitudeWinter;

                    double lapseRate = 0.0065; // Lapse rate in C per meter
                    double altitude = biomeMap[i][j].height; // Altitude in meters
                    double temperatureDrop = lapseRate * altitude; // Temperature drop due to altitude

                    baseTemperatureSummer -= temperatureDrop;
                    baseTemperatureWinter -= temperatureDrop; 

                    if (j == width / 2 & i % 100 == 0)
                    {
                        if (j == width / 2 & i % 100 == 0)
                        {
                            Console.WriteLine($"Base Temperature Summer: {baseTemperatureSummer}, Base Temperature Winter: {baseTemperatureWinter}, for latiude/longitude ({biomeMap[i][j].latitude}, {biomeMap[i][j].longitude})");
                        }
                    }
                    

                    biomeMap[i][j].averageTempSummer = baseTemperatureSummer;
                    biomeMap[i][j].averageTempWinter = baseTemperatureWinter;


                }
            }
        }

        public void renderHeatMap(bool summer, string filename)
        {
            (double, double) minMaxTemp = getMinimumMaximumTemp();
            double minTemp = minMaxTemp.Item1;
            double maxTemp = minMaxTemp.Item2;

            Console.WriteLine($"Minimum Temperature: {minTemp}, Maximum Temperature: {maxTemp}");

            using (Image<Rgba32> image = new Image<Rgba32>(width, height))
            {
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        double temp = summer ? biomeMap[i][j].averageTempSummer : biomeMap[i][j].averageTempWinter;
                        double normalizedTemp = normalizeTemp(minTemp,maxTemp,temp);
                        
                        if (biomeMap[i][j].coastal) {
                            image[j, i] = Color.Black;
                        }
                        else
                        {
                            image[j, i] = new Rgba32((float)normalizedTemp, 0f, (float)(1f - normalizedTemp));
                        }
                    }
                }
                image.Save(parentDirectory + filename);
            }
        }

        //Returns a value between 0 and 1 depending on range between minTemp and maxTemp;
        //Assumes max temp is greater than min temp, and greater than 0
        public double normalizeTemp(double minTemp, double maxTemp, double temp)
        {
            if(minTemp < 0)
            {
                maxTemp += Math.Abs(minTemp);
                temp += Math.Abs(minTemp);
                minTemp = 0;
            }
            else if (minTemp > 0)
            {
                maxTemp -= Math.Abs(minTemp);
                temp -= Math.Abs(minTemp);
                minTemp = 0;
            }//Ensure min temp is set to a floor of 0, adjusting current temp/max temp by that value
            return temp / maxTemp;
        }

        private void writeTempsToDB(DBConnection db)
        {
            List<string> batchTempUpdateRows = new List<string>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    batchTempUpdateRows.Add(string.Format($"UPDATE \"Points\" SET summerSolsticeAverageTemperature = {biomeMap[y][x].averageTempSummer}, winterSolsticeAverageTemperature = {biomeMap[y][x].averageTempWinter} WHERE id = {biomeMap[y][x].id}"));
                }
            }
            db.runStringNonQueryCommandBatch("", "", batchTempUpdateRows, 2500, ';', true);
        }

        //Iterate through the biome point array to find the lowest & highest temperature
        private (double minTemp, double maxTemp) getMinimumMaximumTemp()
        {
            double minTemp = double.MaxValue;
            double maxTemp = double.MinValue;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    double summerTemp = biomeMap[i][j].averageTempSummer;
                    double winterTemp = biomeMap[i][j].averageTempWinter;
                    if (summerTemp < minTemp)
                        minTemp = summerTemp;
                    if (winterTemp < minTemp)
                        minTemp = winterTemp;
                    if (summerTemp > maxTemp)
                        maxTemp = summerTemp;
                    if (winterTemp > maxTemp)
                        maxTemp = winterTemp;
                }
            }
            return (minTemp, maxTemp);
        }

        

        private biomePoint[][] getBiomePointArray(DBConnection db)
        {
            string query = "SELECT p1.x, p1.y , p1.height, p1.latitude, p1.longitude, p1.land, p2.coastal, p1.id FROM \"Points\" p1 LEFT JOIN \"WorldPoints\" p2 ON p1.worldPointId = p2.id ORDER BY p1.y, p1.x;";
            var biomeMap = new biomePoint[height][];
            for (int i = 0; i < height; i++)
            {
                biomeMap[i] = new biomePoint[width];
                for (int j = 0; j < width; j++)
                {
                    biomeMap[i][j] = new biomePoint
                    {
                        height = -1, // Default initialization
                        land = false,
                        coastal = false,
                        y = i + height,
                        x = j + width,
                        latitude = -91,
                        longitude = -181,
                        id = -1,
                        averageTempSummer = 0.0,
                        averageTempWinter = 0.0
                    };
                }
            }

            NpgsqlDataReader rdr = db.runQueryCommand(query);

            while (rdr.Read())
            {
                if (rdr.GetInt32(0) == widthMin && (rdr.GetInt32(1) % 100) == 0)
                {
                    Console.WriteLine("Processing line: " + rdr.GetInt32(1));
                }
                //Console.WriteLine($"Processing point: {rdr.GetInt32(0)}, {rdr.GetInt32(1)}");
                int x = rdr.GetInt32(0);
                int y = rdr.GetInt32(1);

                int xI = x - widthMin;
                int yI = y - heightMin;

                biomeMap[yI][xI].id = rdr.GetInt32(7);
                biomeMap[yI][xI].x = x;
                biomeMap[yI][xI].y = y;
                biomeMap[yI][xI].height = rdr.GetInt32(2);
                biomeMap[yI][xI].latitude = rdr.GetDouble(3);
                biomeMap[yI][xI].longitude = rdr.GetDouble(4);
                biomeMap[yI][xI].land = rdr.GetBoolean(5);
                biomeMap[yI][xI].coastal = rdr.GetBoolean(6);
            }
            rdr.Close();


            return biomeMap;
        }

        public struct biomePoint
        {
            public int x;
            public int y;
            public int height;
            public double latitude;
            public double longitude;
            public bool land;
            public bool coastal;
            public int id;
            public double averageTempSummer;
            public double averageTempWinter;
        }


    }
}
