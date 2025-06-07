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
var connectionStringBuilder = new MySqlConnectionStringBuilder(connectionString);
dbCon.Server = connectionStringBuilder.Server;
dbCon.DatabaseName = connectionStringBuilder.Database;
dbCon.UserName = connectionStringBuilder.UserID;
dbCon.Password = connectionStringBuilder.Password;

if (dbCon.IsConnect())
{
    dbCon.setSessionVariables();
    // Get the current directory
    string currentDirectory = Environment.CurrentDirectory;

    // Go back three directories to get to the main project folder
    string parentDirectory = Directory.GetParent(currentDirectory).Parent.Parent.FullName;

    // Output the parent directory
    Console.WriteLine(parentDirectory);

    //FractalCoastline fractalCoastlineGenerator = new FractalCoastline(parentDirectory+"\\Maps\\Fractal Coastline\\","coastline-map-base.png",config);
    //fractalCoastlineGenerator.generateFractalCoastlineWithTurbulence();

    WorldGen worldGenerator = new WorldGen(parentDirectory + "\\Maps\\land-map.png", config);

    //worldGenerator.populatePointsAndWorldPointsFromImage(parentDirectory, dbCon,config);

    //worldGenerator.loadViewPointTableFromDB(dbCon);
    //worldGenerator.renderViewPointsAsImage(parentDirectory + "\\Maps\\view-output.png", dbCon, config);

    //MapProvinceCreate.assignRemainingUnallocatedPoints(dbCon, 1000, 1053);

    //MapProvinceCreate.renderProvinces(dbCon, parentDirectory + "\\sf-continent.png", parentDirectory + "\\output-sf-continent-2.png");
    //MapProvinceCreate.populateEdgesTable(dbCon);
    //SVGRenderer.renderEdges(dbCon, parentDirectory + "\\sf-continent-4.svg");
    //SVGRenderer.updatePolygonToPath( parentDirectory + "\\sf-continent-4.svg", parentDirectory + "\\sf-continent-5.svg");
    //Map SingletonMap = Map.Instance;
    //HeightMapGen.generateHeightMap(dbCon, config,parentDirectory, "\\Maps\\base-continent-heightmap.png");
    //HeightMapGen.renderCoastline(dbCon, parentDirectory + "\\sf-continent-coastline.png", 3060, 3604);
    //HeightMapGen.writeElevationsToDbPoints(dbCon);
    HeightMapGen.renderViewHeightmap(dbCon, parentDirectory + "\\Maps\\view-heightmap.png",config);

    dbCon.Close();
}
