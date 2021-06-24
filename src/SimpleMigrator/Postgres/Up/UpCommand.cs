using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using Dapper;
using Npgsql;

namespace SimpleMigrator.Postgres.Up
{
    class UpCommand : Command
    {
        private const string ConnectionStringOptionName = "--connection-string";
        private const string FolderOptionName = "--folder";
        
        public UpCommand() : base("up", "Migrates the postgres database forward")
        {
            AddOption(new Option<string?>(
                ConnectionStringOptionName,
                description: "The connection string to the Postgres database",
                arity: ArgumentArity.ExactlyOne));
            AddOption(new Option<DirectoryInfo?>(
                FolderOptionName,
                description: "The folder within which the SQL scripts to run live",
                arity: ArgumentArity.ExactlyOne));
            Handler = CommandHandler.Create<string?, DirectoryInfo?>(Up);
        }

        private static void Up(string? connectionString, DirectoryInfo? folder)
        {
            if (!ValidateArgs(connectionString, folder))
            {
                return;
            }

            using (var connection = new NpgsqlConnection(connectionString!))
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
                var files = Directory.GetFiles(folder!.FullName)
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
        
        private static string GetSql(string fileName) => File.ReadAllText(fileName);

        private static bool ValidateArgs(string? connectionString, DirectoryInfo? folder)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                errors.Add($"{ConnectionStringOptionName} is required.");
            }
            if (folder == null)
            {
                errors.Add($"{FolderOptionName} is required.");
            }

            if (errors.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(string.Join(Environment.NewLine, errors));
                return false;
            }
            return true;
        }
    }
}