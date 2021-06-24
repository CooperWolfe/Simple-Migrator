using System.CommandLine;

namespace SimpleMigrator
{
    class Program
    {
        static int Main(string[] args)
        {
            var migrateCommand = new MigrateCommand();
            return migrateCommand.InvokeAsync(args).Result;
        }
    }
}
