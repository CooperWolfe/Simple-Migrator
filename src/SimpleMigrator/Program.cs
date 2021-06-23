using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Dapper;

namespace SimpleMigrator
{
    class Program
    {
        static void Main(string connectionString, string folder)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // check if the migrations table exists, otherwise execute the first script (which creates that table)
                if (connection.ExecuteScalar<int>(@"SELECT count(1) FROM sys.tables WHERE T.Name = 'migrationscripts'") == 0)
                {
                    connection.Execute(GetSql("00001-Init"));
                    connection.Execute(
                        @"INSERT INTO MigrationScripts (Name, ExecutionDate) VALUES (@Name, GETDATE())",
                        new { Name = "00001-Init" });
                }

                // Get all scripts that have been executed from the database
                var executedScripts = connection.Query<string>("SELECT Name FROM MigrationScripts");
                // Get all scripts from the filesystem
                var files = Directory.GetFiles(folder)
                    // strip out the extensions
                    .Select(fileName => Path.GetFileNameWithoutExtension(fileName)!)
                    // filter the ones that have already been executed
                    .Where(fileName => !executedScripts.Contains(fileName))
                    // order by filename
                    .OrderBy(fileName => fileName);

                foreach (var file in files)
                {
                    // Execute each one of the scripts
                    connection.Execute(GetSql(file));
                    // record that it was executed in the migrationscripts table
                    connection.Execute(
                        @"INSERT INTO MigrationScripts (Name, ExecutionDate) VALUES (@Name, GETDATE())",
                        new { Name = file });
                }
            }
        }

        static string GetSql(string fileName) => File.ReadAllText(fileName);
    }
}
