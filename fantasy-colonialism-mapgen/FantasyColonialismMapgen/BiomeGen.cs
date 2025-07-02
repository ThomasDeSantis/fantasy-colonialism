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
using static Org.BouncyCastle.Asn1.Cmp.Challenge;

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

        IConfiguration config = null;


        //Rainfall params
        double baseRainfallLat0;
        double baseRainfallLat30;
        double baseRainfallLat60;
        double baseRainfallLat90;

        double coastalDecayKm;

        double orographicBoostPer1000m;
        double orographicMaxElevation;

        double rainfallNoiseVariability;

        double shadowMinFactor;
        double blockerThreshold;
        double shadowLengthPerMeter;

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

            this.config = config;

            //Set variables used by rainfall gen
            // Base annual rainfall (mm) at key latitudes:
            baseRainfallLat0 = config.GetValue<int>("RainfallGenSettings:BaseRainfallLat0"); // Peak at equator (0° latitude)
            baseRainfallLat30 = config.GetValue<int>("RainfallGenSettings:BaseRainfallLat30"); // Minimum at 30° (desert belts)
            baseRainfallLat60 = config.GetValue<int>("RainfallGenSettings:BaseRainfallLat60"); // Secondary peak at 60° (mid-latitudes)
            baseRainfallLat90 = config.GetValue<int>("RainfallGenSettings:BaseRainfallLat90"); // Drop again by 90° (poles) 

            //Coastal decay: 
            coastalDecayKm = config.GetValue<double>("RainfallGenSettings:CoastalDecay"); // e-folding distance for moisture (in km)

            // Orographic uplift parameters:
            //https://en.wikipedia.org/wiki/Orographic_lift
            orographicBoostPer1000m = config.GetValue<double>("RainfallGenSettings:OrographicBoostPer1000M"); // Percent boost per 1000 m
            orographicMaxElevation = config.GetValue<double>("RainfallGenSettings:OrographicMaxElevation"); // cap boost at 3000 m (30% max)

            rainfallNoiseVariability = config.GetValue<double>("RainfallGenSettings:RainfallNoiseVariability"); // Variability of the noise

            //Rain shadow parameters:
            shadowMinFactor = config.GetValue<double>("RainShadowSettings:RainShadowReduction"); // Minimum factor for rain shadow 
            blockerThreshold = config.GetValue<double>("RainShadowSettings:BlockerThreshold"); // Height difference to block rain (in meters)
            shadowLengthPerMeter = config.GetValue<double>("RainShadowSettings:ShadowLengthPerMeter"); // Length of rain shadow per meter of height difference (in km)

        }

        public void generateBiomes(DBConnection db)
        {
            Console.WriteLine($"Begin assigning heat maps: {DateTime.UtcNow.ToString()}");
            //assignHeatMap();
            Console.WriteLine($"Finished assigning heat maps: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin rendering heat maps: {DateTime.UtcNow.ToString()}");
            //renderHeatMap(true, "heatmap_summer.png");
            //renderHeatMap(false, "heatmap_winter.png");
            Console.WriteLine($"Finished rendering heat maps: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin writing temperatures to DB: {DateTime.UtcNow.ToString()}");
            //writeTempsToDB(db);
            Console.WriteLine($"Finished writing temperatures to DB: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin assigning rainfall: {DateTime.UtcNow.ToString()}");
            assignAverageRainfall();
            Console.WriteLine($"Finished assigning rainfall: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin rendering rainfall map: {DateTime.UtcNow.ToString()}");
            renderAverageRainfallMap("average_rainfall_map.png");
            Console.WriteLine($"Finished rendering rainfall map: {DateTime.UtcNow.ToString()}");
            Console.WriteLine($"Begin writing rainfall to DB: {DateTime.UtcNow.ToString()}");
            writeRainfallToDB(db);
            Console.WriteLine($"Finished writing rainfall to DB: {DateTime.UtcNow.ToString()}");
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
                        double normalizedTemp = normalizeTemp(minTemp, maxTemp, temp);

                        if (biomeMap[i][j].coastal)
                        {
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
            if (minTemp < 0)
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
            string query = "SELECT p1.x, p1.y , p1.height, p1.latitude, p1.longitude, p1.land, p2.coastal, p1.id, p1.coastalDistance, p1.length, p1.width FROM \"Points\" p1 LEFT JOIN \"WorldPoints\" p2 ON p1.worldPointId = p2.id ORDER BY p1.y, p1.x;";
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
                        averageTempWinter = 0.0,
                        averageRainfall = 0.0, // mm/year
                        length = 0,
                        width = 0
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
                biomeMap[yI][xI].coastalDistance = rdr.GetInt32(8);
                biomeMap[yI][xI].length = rdr.GetInt32(9);
                biomeMap[yI][xI].width = rdr.GetInt32(10);
            }
            rdr.Close();


            return biomeMap;
        }


        public void assignAverageRainfall()
        {
            MultifractalNoise noise = new MultifractalNoise();
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (j == 0 && (i % 100) == 0)
                    {
                        Console.WriteLine("Processing line: " + i);
                    }
                    if (!biomeMap[i][j].land)
                    {
                        biomeMap[i][j].averageRainfall = 0.0; // For stub purposes no rainfall over water
                    }
                    else
                    {
                        // 1. Base rainfall by latitude
                        double baseRain = calculateBaseRainfallAtLatitude(biomeMap[i][j].latitude);

                        // 2. Exponential decay factor for distance from coast
                        // (moisture decreases inland; COASTAL_DECAY_KM is the 1/e distance)
                        double coastalFactor = Math.Exp((biomeMap[i][j].coastalDistance * -1) / coastalDecayKm);

                        // 3. Orographic boost for elevation (10% per 1000 m, capped at 3000 m)
                        double effectiveElev = biomeMap[i][j].height < 0 ? 0.0 : biomeMap[i][j].height;  // treat below sea level as 0
                        double elevationFactor = 1.0 + orographicBoostPer1000m * (Math.Min(effectiveElev, orographicMaxElevation) / 1000.0);

                        // 4. Random variation factor (±factor%) using Noise generator
                        double randomFactor = Math.Abs((noise.GetNoise(biomeMap[i][j].x, biomeMap[i][j].y) * rainfallNoiseVariability) + 1.0);
                        // This yields a multiplier between (1-rainfallnoisevariability and 1+rainfallnoisevariability).

                        //5. Compute rain shadow factor
                        double rainShadowFactor = getRainShadowFactor(j, i); // x, y are reversed in the map

                        //6. If rainShadowFactor is less than .9, null out the elevation factor
                        if (rainShadowFactor < 0.9)
                        {
                            elevationFactor = 1.0; // No elevation boost in rain shadow
                        }

                        // 7. Compute final average annual rainfall (mm)
                        double rainfallmm = baseRain * coastalFactor * elevationFactor * randomFactor * rainShadowFactor;

                        if (rainfallmm < 20.0)
                        {
                            rainfallmm = 20.0; // Ensure a minimum rainfall
                        }

                        // Store the result in the point's averageRainfall field
                        biomeMap[i][j].averageRainfall = rainfallmm;
                    }
                }
            }

        }

        private double calculateBaseRainfallAtLatitude(double latitudeDegrees)
        {
            // Use absolute latitude (treat N/S symmetrically), clamp to 90
            double lat = Math.Min(Math.Abs(latitudeDegrees), 90.0);
            if (lat <= 30.0)
            {
                // Interpolate from 0° to 30°
                // At 0° => BASE_RAINFALL_LAT0, at 30° => BASE_RAINFALL_LAT30
                double t = lat / 30.0;
                return baseRainfallLat0 + t * (baseRainfallLat30 - baseRainfallLat0);
            }
            else if (lat <= 60.0)
            {
                // Interpolate from 30° to 60°
                // At 30° => BASE_RAINFALL_LAT30, at 60° => BASE_RAINFALL_LAT60
                double t = (lat - 30.0) / 30.0;
                return baseRainfallLat30 + t * (baseRainfallLat60 - baseRainfallLat30);
            }
            else
            {
                // Interpolate from 60° to 90°
                // At 60° => BASE_RAINFALL_LAT60, at 90° => BASE_RAINFALL_LAT90
                double t = (lat - 60.0) / 30.0;
                return baseRainfallLat60 + t * (baseRainfallLat90 - baseRainfallLat60);
            }
        }

        //Given an x/y point determines if it is in a rain shadow based on the elevation of the point and the elevation of the upwind points
        // Computes rain shadow factor between 0.5 and 1.0 depending on distance from blocker
        private double getRainShadowFactor(int x, int y)
        {

            if (!biomeMap[y][x].land) return 1.0;

            double windDir = getPrevailingWindDirection(biomeMap[y][x].latitude);
            int dx = 0;

            if (windDir == 90) dx = -1;         // easterlies (wind from east)
            else if (windDir == 270) dx = 1;    // westerlies (wind from west)
            else return 1.0;

            int curX = width;
            double cellLengthKm = biomeMap[y][x].length / 1000.0;

            for (int step = 1; ; step++)
            {
                int xS = curX + dx;
                if (xS < 0 || xS >= width) break;

                biomePoint upwind = biomeMap[y][xS];
                if (!upwind.land) return 1.0;

                double heightDiff = upwind.height - biomeMap[y][x].height;
                if (heightDiff >= blockerThreshold)
                {
                    double maxShadowKm = heightDiff * shadowLengthPerMeter;
                    double distanceKm = step * cellLengthKm;

                    if (distanceKm > maxShadowKm)
                        break;

                    // Interpolate reduction: stronger close to mountain, weaker farther out
                    double t = distanceKm / maxShadowKm;
                    double shadowFactor = shadowMinFactor + (1.0 - shadowMinFactor) * t;
                    return shadowFactor;
                }

                curX = xS;
            }

            return 1.0;
        }

        // Returns direction wind is coming FROM, in degrees
        // 90 = east → wind from east → goes westward
        // 270 = west → wind from west → goes eastward
        private static double getPrevailingWindDirection(double latitude)
        {
            double latAbs = Math.Abs(latitude);
            if (latAbs <= 30 || latAbs >= 60)
                return 90;  // Easterlies: wind from east
            else
                return 270; // Westerlies: wind from west
        }

        private void renderAverageRainfallMap(string filename)
        {
            // Define rainfall breakpoints and corresponding colors
            var breakpoints = new (double max, Rgba32 color)[]
            {
                (25,      new Rgba32(139, 0, 0)),        // Dark red
                (100,     new Rgba32(255, 0, 0)),        // Red
                (200,     new Rgba32(255, 140, 0)),      // Orange
                (400,     new Rgba32(255, 180, 50)),     // Yellowish-orange
                (600,     new Rgba32(255, 255, 0)),      // Yellow
                (800,     new Rgba32(255, 255, 102)),    // Bright yellow
                (1000,    new Rgba32(191, 255, 0)),      // Lime green
                (1200,    new Rgba32(0, 255, 0)),        // Green
                (1400,    new Rgba32(0, 100, 0)),        // Dark green
                (1600,    new Rgba32(42, 82, 190)),      // Cerulean
                (1800,    new Rgba32(0, 0, 255)),        // Blue
                (2000,    new Rgba32(75, 0, 130)),       // Indigo
                (double.MaxValue, new Rgba32(148, 0, 211)) // Violet
            };

            string filenamePath = parentDirectory + filename;
            using (Image<Rgba32> image = new Image<Rgba32>(width, height))
            {
                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        double rainfall = biomeMap[i][j].averageRainfall;
                        Rgba32 color = new Rgba32(0, 0, 0); // Default: black for water or missing data

                        if (!biomeMap[i][j].land)
                        {
                            color = new Rgba32(0, 0, 0); // Black for water
                        }
                        else
                        {
                            foreach (var bp in breakpoints)
                            {
                                if (rainfall <= bp.max)
                                {
                                    color = bp.color;
                                    break;
                                }
                            }
                        }
                        image[j, i] = color;
                    }
                }
                image.Save(filenamePath);
            }
        }

        private void writeRainfallToDB(DBConnection db)
        {
            List<string> batchTempUpdateRows = new List<string>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    batchTempUpdateRows.Add(string.Format($"UPDATE \"Points\" SET averageRainfall = {biomeMap[y][x].averageRainfall} WHERE id = {biomeMap[y][x].id}"));
                }
            }
            db.runStringNonQueryCommandBatch("", "", batchTempUpdateRows, 2500, ';', true);
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
            public double averageRainfall; // mm/year
            public int coastalDistance;
            public int length;//EW
            public int width;//NS
        }
    }

}
