using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FormHandler.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("Database__ConnectionString")
            ?? (args.Length > 0 ? args[0] : string.Empty);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Database connection string must be supplied via the Database__ConnectionString environment variable or as the first argument.");
            return 1;
        }

        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices(services =>
            {
                services
                    .AddFluentMigratorCore()
                    .ConfigureRunner(runner => runner
                        .AddSQLite()
                        .WithGlobalConnectionString(connectionString)
                        .ScanIn(typeof(FormHandler.Infrastructure.Migrations.M001_CreateContactSubmissions).Assembly).For.Migrations())
                    .AddLogging(lb => lb.AddFluentMigratorConsole());
            })
            .Build();

        using IServiceScope scope = host.Services.CreateScope();
        IMigrationRunner runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();

        return 0;
    }
}
