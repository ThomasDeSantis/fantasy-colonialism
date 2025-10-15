using ChGenerateHex;
using DatabaseLibraries;
using MapData;
using System;
using System.Reflection.Metadata;
using System.IO;
using Microsoft.Extensions.Configuration;

class GenerateHexMain
{
    //Param 1 - origin x, param 2 origin y, param 3 - debug boolean
    static void Main(string[] args)
    {
        // Your CLI application logic goes here
        if (args.Length != 3)
        {
            throw new Exception($"Invalid arguments: {args.ToString()}");
        }

        int hexTimestamp = DateTime.UtcNow.ToString().GetHashCode();

        string currentDirectory = Environment.CurrentDirectory;
        string parentDirectory = Directory.GetParent(currentDirectory).Parent.Parent.Parent.Parent.FullName;
        string debugImageDirectory = parentDirectory + $"\\img\\{hexTimestamp}";

        bool dirExists = System.IO.Directory.Exists(debugImageDirectory);
        if (!dirExists)
            System.IO.Directory.CreateDirectory(debugImageDirectory);

        Console.WriteLine($"Generating hex: x {args[0]}, y {args[1]} Debug: {args[2]}");

        (int,int) clientOrigin = (int.Parse(args[0]), int.Parse(args[1]));
        (int,int) origin = Hex.hexCoordsToPointCoords(clientOrigin, 100,85);
        bool debug = bool.Parse(args[2]);

        List<(int, int)> hexPoints = Geometry.generateHexagonPoints(clientOrigin, 100, 86);

        HashSet<ChPoint> chPoints = new HashSet<ChPoint>();
        for (int i = 0; i < hexPoints.Count; i++)
        {
            chPoints.Add(new ChPoint(hexPoints[i].Item1, hexPoints[i].Item2));
        }

        Hex newHex = new Hex(chPoints, origin, clientOrigin);

        Console.WriteLine(newHex);
        Console.WriteLine($"Normalized Bounds: (0,0) to ({newHex.getNormalizedBounds().Item1}, {newHex.getNormalizedBounds().Item2})");
        Console.WriteLine($"Min X: {newHex.getHexBounds().Item1.Item1} Max X: {newHex.getHexBounds().Item2.Item1}");
        Console.WriteLine($"Min Y: {newHex.getHexBounds().Item1.Item2} Max Y: {newHex.getHexBounds().Item2.Item2}");
        //Console.WriteLine(ChPointSerializeJSON.serializeChPointJSON(newHex.Points, origin));


        if (debug && false)
        {
            ((int, int), (int, int)) hexBounds = newHex.getHexBounds();
            Console.WriteLine($"Hex Bounds: Top Left ({hexBounds.Item1.Item1}, {hexBounds.Item1.Item2}) Bottom Right ({hexBounds.Item2.Item1}, {hexBounds.Item2.Item2})");
            ChDrawHex.drawHexBounds(newHex, debugImageDirectory + "\\hex-bounds.png");
        }
        Console.WriteLine(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName);/*
        AppSettings config = new AppSettings(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName, "appsettings.json");*/
        IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName).AddJsonFile("appsettings.json");
        IConfiguration config = builder.Build();
        //string connectionString = config.getSqlConnectionString("DefaultConnection");
        string connectionString = config.GetConnectionString("DatabaseConnection");
        Console.WriteLine($"Connection String: {connectionString}");

        var dbCon = DBConnection.Instance();
        if (dbCon.IsConnect(connectionString))
        {
            Console.WriteLine("Database connection successful.");
        }
    }

    public static void writeHexToDB()
    {

    }
}