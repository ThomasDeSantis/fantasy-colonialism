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
    MapProvinceCreate.processImageIntoPoints("C:\\Users\\Thomas\\Documents\\fantasy-colonialism\\fantasy-colonialism-backend\\FantasyColonialismBackend\\test-continent-2.png", dbCon);
    MapProvinceCreate.renderProvinces(dbCon, "C:\\Users\\Thomas\\Documents\\fantasy-colonialism\\fantasy-colonialism-backend\\FantasyColonialismBackend\\test-continent-2.png", "C:\\Users\\Thomas\\Documents\\fantasy-colonialism\\fantasy-colonialism-backend\\FantasyColonialismBackend\\output-2.png");
    dbCon.Close();
}
