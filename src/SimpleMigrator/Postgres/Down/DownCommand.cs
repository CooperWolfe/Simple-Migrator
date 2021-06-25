using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Data;
using System.IO;
using System.Linq;
using Dapper;
using Npgsql;

namespace SimpleMigrator.Postgres.Down
{
    class DownCommand : Command
    {
        private const string ConnectionStringOptionName = "--connection-string";
        private const string FolderOptionName = "--folder";
        private const string ToOptionName = "--to";
        
        public DownCommand() : base("down", "Migrates the postgres database forward")
        {
            var connectionStringOption = new Option<string?>(
                ConnectionStringOptionName,
                description: "The connection string to the Postgres database",
                arity: ArgumentArity.ExactlyOne);
            connectionStringOption.AddAlias("-c");
            AddOption(connectionStringOption);

            var folderOption = new Option<DirectoryInfo?>(
                FolderOptionName,
                description: "The folder within which the SQL scripts to run live",
                arity: ArgumentArity.ExactlyOne);
            folderOption.AddAlias("-f");
            AddOption(folderOption);

            var toOption = new Option<string?>(
                ToOptionName,
                description: "The down migration to migrate down to",
                arity: ArgumentArity.ZeroOrOne);
            AddOption(toOption);
            
            Handler = CommandHandler.Create<string?, DirectoryInfo?, string?>(Down);
        }

        private static void Down(string? connectionString, DirectoryInfo? folder, string? to)
        {
            if (!ValidateArgs(connectionString, folder))
            {
                return;
            }

            using var connection = new NpgsqlConnection(connectionString!);
            CheckForMigrationTable(connection);

            var executedScripts = connection.Query<string>("SELECT name FROM migration.script");
            if (!string.IsNullOrWhiteSpace(to))
            {
                if (to.EndsWith(".down")) to = to[..^5];
                if (!executedScripts.Contains(to))
                {
                    WriteError($@"Migration ""{to}"" does not exist");
                    return;
                }
            }

            var filesToExecute = folder!.GetFiles()
                .Select(file => (path: file.FullName, migrationName: Path.GetFileNameWithoutExtension(file.FullName)!))
                .Where(tuple => tuple.migrationName.EndsWith(".down"))
                .Select(tuple => (path: tuple.path, migrationName: tuple.migrationName[..^5]))
                .Where(tuple => executedScripts.Contains(tuple.migrationName))
                .Where(tuple => string.IsNullOrWhiteSpace(to) || string.Compare(tuple.migrationName, to) > 0)
                .OrderByDescending(tuple => tuple.migrationName);

            if (!filesToExecute.Any())
            {
                Console.WriteLine("Nothing to do!");
                return;
            }

            foreach (var fileInfo in filesToExecute)
            {
                Console.WriteLine($"Downgrading: {fileInfo.migrationName}");
                connection.Execute(File.ReadAllText(fileInfo.path));
                connection.Execute(
                    @"DELETE FROM migration.script WHERE name = @name",
                    new { name = fileInfo.migrationName });
            }

            Console.WriteLine("Done!");
        }

        private static void CheckForMigrationTable(NpgsqlConnection connection)
        {
            bool tableExists = connection.ExecuteScalar<int>(@"SELECT count(1) FROM information_schema.tables WHERE table_schema = 'migration' AND table_name = 'script'") > 0;
            if (!tableExists)
            {
                WriteError("Database does not contain a migration table.");
                return;
            }
        }

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
            else if (!folder.Exists)
            {
                errors.Add($"Folder {folder.FullName} does not exist.");
            }

            if (errors.Any())
            {
                WriteError(string.Join(Environment.NewLine, errors));
                return false;
            }
            return true;
        }

        private static void WriteError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(error);
        }
    }
}