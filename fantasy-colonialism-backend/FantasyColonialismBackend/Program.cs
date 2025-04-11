// See https://aka.ms/new-console-template for more information
using FantasyColonialismBackend;
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

    MapProvinceCreate.processImageIntoPoints(parentDirectory + "\\sf-continent.png", dbCon,config);

    //MapProvinceCreate.assignRemainingUnallocatedPoints(dbCon, 1000, 1053);
    
    MapProvinceCreate.renderProvinces(dbCon, parentDirectory + "\\sf-continent.png", parentDirectory + "\\output-sf-continent-2.png");
    //MapProvinceCreate.populateEdgesTable(dbCon);
    //SVGRenderer.renderEdges(dbCon, parentDirectory + "\\sf-continent-2.svg");
    //SVGRenderer.updatePolygonToPath( parentDirectory + "\\sf-continent-2.svg", parentDirectory + "\\sf-continent-3.svg");
    dbCon.Close();
}
