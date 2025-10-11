using ChGenerateHex;
using MapData;
using System;
using System.Reflection.Metadata;

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

        int originX = int.Parse(args[0]);
        int originY = int.Parse(args[1]);
        bool debug = bool.Parse(args[2]);

        List<(int, int)> hexPoints = Geometry.generateHexagonPoints((originX, originY), 126.0, 110.0);

        HashSet<ChPoint> chPoints = new HashSet<ChPoint>();
        for (int i = 0; i < hexPoints.Count; i++)
        {
            chPoints.Add(new ChPoint(hexPoints[i].Item1, hexPoints[i].Item2));
        }

        Hex newHex = new Hex(chPoints, (originX, originY));

        Console.WriteLine(newHex);


        if (debug) {
            ((int, int), (int, int)) hexBounds = newHex.getHexBounds();
            Console.WriteLine($"Hex Bounds: Top Left ({hexBounds.Item1.Item1}, {hexBounds.Item1.Item2}) Bottom Right ({hexBounds.Item2.Item1}, {hexBounds.Item2.Item2})");
            ChDrawHex.drawHexBounds(newHex, debugImageDirectory + "\\hex-bounds.png");
        }
    }
}