using FantasyColonialismMapgen;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System.IO;

Console.WriteLine("Hello, World!");

//Build configuration
//Look for app settings in the folder the project is written in (two directories up)
IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName).AddJsonFile("appsettings.json");
IConfiguration config = builder.Build();

//Retrieve connection string
string connectionString = config.GetConnectionString("DatabaseConnection");

//Parse connection string
var dbCon = DBConnection.Instance();

if (dbCon.IsConnect(connectionString))
{
    Console.WriteLine("Connected to database successfully!");
    //dbCon.setSessionVariables();
    // Get the current directory
    string currentDirectory = Environment.CurrentDirectory;

    // Go back three directories to get to the main project folder
    string parentDirectory = Directory.GetParent(currentDirectory).Parent.Parent.FullName;

    // Output the parent directory
    Console.WriteLine(parentDirectory);

    //FractalCoastline fractalCoastlineGenerator = new FractalCoastline(parentDirectory+"\\Maps\\Fractal Coastline\\","coastline-map-base.png",config);
    //fractalCoastlineGenerator.generateFractalCoastlineWithTurbulence();

    //WorldGen worldGenerator = new WorldGen(parentDirectory + "\\Maps\\land-map.png", config);
    WorldGen.assignDistanceToCoast(dbCon, config);

    //worldGenerator.populatePointsAndWorldPointsFromImage(parentDirectory, dbCon,config);



    //SVGRenderer.renderEdges(dbCon, parentDirectory + "\\sf-continent-4.svg");
    //SVGRenderer.updatePolygonToPath( parentDirectory + "\\sf-continent-4.svg", parentDirectory + "\\sf-continent-5.svg");

    //HeightMapGen.generateHeightMap(dbCon, config,parentDirectory, "\\Maps\\base-continent-heightmap.png");
    //HeightMapGen.renderCoastline(dbCon, parentDirectory + "\\sf-continent-coastline.png", 3060, 3604);
    //HeightMapGen.writeElevationsToDbPoints(dbCon);
    //worldGenerator.populateLatitudeLongitude(dbCon, config);
    //worldGenerator.populateLatitudeLongitudeDimensionsPoints(dbCon,config);
    //BiomeGen biomeGenerator = new BiomeGen(dbCon, config, parentDirectory + "\\Maps\\");
    //biomeGenerator.generateBiomes(dbCon);
    //HeightMapGen.renderViewHeightmap(dbCon, parentDirectory + "\\Maps\\view-heightmap.png",config);

    dbCon.Close();
}
