using System.CommandLine;
using SimpleMigrator.Postgres;

namespace SimpleMigrator
{
    class MigrateCommand : RootCommand
    {
        public MigrateCommand() : base("Migrates databases")
        {
            AddCommand(new PostgresCommand());
        }
    }
}