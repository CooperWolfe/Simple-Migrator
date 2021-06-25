using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using Dapper;
using Npgsql;

namespace SimpleMigrator.Postgres.Up
{
    class UpCommand : Command
    {
        private const string ConnectionStringOptionName = "--connection-string";
        private const string FolderOptionName = "--folder";
        private const string ToOptionName = "--to";
        private const string InitScriptName = "20210624-01-init-migration-scripts";
        
        public UpCommand() : base("up", "Migrates the postgres database forward")
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
                description: "The migration to migrate the database up to",
                arity: ArgumentArity.ZeroOrOne);
            AddOption(toOption);

            Handler = CommandHandler.Create<string?, DirectoryInfo?, string?>(Up);
        }

        private static void Up(string? connectionString, DirectoryInfo? folder, string? to)
        {
            if (!ValidateArgs(connectionString, folder))
            {
                return;
            }

            using var connection = new NpgsqlConnection(connectionString!);
            EnsureMigrationsTableExists(connection);

            var executedScripts = connection.Query<string>("SELECT name FROM migration.script");
            var filesToExecute = folder!.GetFiles()
                .Select(file => (path: file.FullName, migrationName: Path.GetFileNameWithoutExtension(file.FullName)!))
                .Where(tuple => !executedScripts.Contains(tuple.migrationName) && !tuple.migrationName.EndsWith(".down"))
                .Where(tuple => string.IsNullOrWhiteSpace(to) || string.Compare(tuple.migrationName, to) <= 0)
                .OrderBy(tuple => tuple.migrationName);

            if (!string.IsNullOrWhiteSpace(to) && filesToExecute.All(tuple => tuple.migrationName != to))
            {
                WriteError($@"Migration ""{to}"" does not exist.");
                return;
            }

            if (!filesToExecute.Any())
            {
                Console.WriteLine("Nothing to do!");
                return;
            }

            foreach (var fileInfo in filesToExecute)
            {
                Console.WriteLine($"Upgrading: {fileInfo.migrationName}");
                connection.Execute(File.ReadAllText(fileInfo.path));
                connection.Execute(
                    @"INSERT INTO migration.script (name, created_at) VALUES (@name, NOW() at time zone 'utc')",
                    new { name = fileInfo.migrationName });
            }

            Console.WriteLine("Done!");
        }

        private static void EnsureMigrationsTableExists(NpgsqlConnection connection)
        {
            bool tableExists = connection.ExecuteScalar<int>(@"SELECT count(1) FROM information_schema.tables WHERE table_schema = 'migration' AND table_name = 'script'") > 0;
            if (tableExists)
            {
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();
            var initMigrationScriptName = resources.Single(res => res.Contains(InitScriptName));
            using var initMigrationScriptStream = assembly.GetManifestResourceStream(initMigrationScriptName)!;
            using var initMigrationScriptReader = new StreamReader(initMigrationScriptStream);

            Console.WriteLine("Enabling migrations");
            connection.Execute(initMigrationScriptReader.ReadToEnd());
            connection.Execute(
                @"INSERT INTO migration.script (name, created_at) VALUES (@name, NOW() at time zone 'utc')",
                new { name = InitScriptName });
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