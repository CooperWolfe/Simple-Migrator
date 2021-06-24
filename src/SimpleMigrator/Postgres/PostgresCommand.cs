using System.CommandLine;
using SimpleMigrator.Postgres.Up;

namespace SimpleMigrator.Postgres
{
    class PostgresCommand : Command
    {
        public PostgresCommand() : base("postgres", "Migrates a postgres database")
        {
            AddCommand(new UpCommand());
        }
    }
}