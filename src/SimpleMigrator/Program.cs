using System;
using System.Data.SqlClient;
using System.IO;
using Dapper;

namespace SimpleMigrator
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var connection = new SqlConnection(connectionString))
            { 
                // check if the migrations table exists, otherwise execute the first script (which creates that table) 
                if (connection.ExecuteScalar<int>(@"SELECT count(1) FROM sys.tables 
                                                    WHERE T.Name = 'migrationscripts'") == 0) 
                { 
                    connection.Execute(GetSql("20151204-1030-Init")); 
                    connection.Execute(@"INSERT INTO MigrationScripts (Name, ExecutionDate) 
                                        VALUES (@Name, GETDATE())", 
                                        new { Name = "20151204-1030-Init" }); 
                } 
                                        
                // Get all scripts that have been executed from the database 
                var executedScripts = connection.Query<string>("SELECT Name FROM MigrationScripts"); 
                // Get all scripts from the filesystem
                Directory.GetFiles(folder) 
                        // strip out the extensions
                        .Select(Path.GetFileNameWithoutExtension)
                        // filter the ones that have already been executed 
                        .Where(fileName => !executedScripts.Contains(fileName)) 
                        // Order by the date in the filename 
                        .OrderBy(fileName => DateTime.ParseExact(fileName.Substring(0, 13), "yyyyMMdd-HHmm", null)) 
                        .ForEach(script => 
                        { 
                            // Execute each one of the scripts 
                            connection.Execute(GetSql(script)); 
                            // record that it was executed in the migrationscripts table 
                            connection.Execute(@"INSERT INTO MigrationScripts (Name, ExecutionDate) 
                                                VALUES (@Name, GETDATE())", 
                                                new { Name = script }); 
                        }); 
            } 
        } 
                
        static string GetSql(string fileName) => File.ReadAllText(fileName);
    }
}
