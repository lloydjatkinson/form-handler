using System.Threading.Tasks;
using FormHandler.Api.Infrastructure.Migrations;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FormHandler.Tests.Infrastructure.Persistence
{
    [CollectionDefinition("Postgres")]
    public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
    { }

    public sealed class PostgresContainerFixture : IAsyncLifetime
    {
        private PostgreSqlContainer container = null!;

        public string ConnectionString { get; private set; } = null!;
        public NpgsqlDataSource DataSource { get; private set; } = null!;

        private void RunMigrations()
        {
            var services = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(runner => runner
                    .AddPostgres()
                    .WithGlobalConnectionString(this.container.GetConnectionString())
                    .ScanIn(typeof(M001CreateSchema).Assembly).For.Migrations());

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
        }

        public async ValueTask InitializeAsync()
        {
            this.container = new PostgreSqlBuilder("postgres:17")
                .Build();

            await this.container.StartAsync();
            this.ConnectionString = this.container.GetConnectionString();
            this.DataSource = NpgsqlDataSource.Create(this.ConnectionString);
            this.RunMigrations();
        }

        public async ValueTask DisposeAsync()
        {
            await this.DataSource.DisposeAsync();
            await this.container.DisposeAsync();
        }
    }
}