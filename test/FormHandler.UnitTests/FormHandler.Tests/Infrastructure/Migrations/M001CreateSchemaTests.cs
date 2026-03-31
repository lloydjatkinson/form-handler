using System.Threading;
using System.Threading.Tasks;
using FluentMigrator.Runner;
using FormHandler.Api.Infrastructure.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FormHandler.Tests.Infrastructure.Migrations
{
    public sealed class M001CreateSchemaTests : IAsyncLifetime
    {
        private PostgreSqlContainer container = null!;
        private NpgsqlDataSource dataSource = null!;

        public async ValueTask InitializeAsync()
        {
            this.container = new PostgreSqlBuilder("postgres:17").Build();
            await this.container.StartAsync();
            this.dataSource = NpgsqlDataSource.Create(this.container.GetConnectionString());
        }

        public async ValueTask DisposeAsync()
        {
            await this.dataSource.DisposeAsync();
            await this.container.DisposeAsync();
        }

        private IMigrationRunner CreateRunner()
        {
            var services = new ServiceCollection()
                .AddFluentMigratorCore()
                .ConfigureRunner(runner => runner
                    .AddPostgres()
                    .WithGlobalConnectionString(this.container.GetConnectionString())
                    .ScanIn(typeof(M001CreateSchema).Assembly).For.Migrations());

            return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IMigrationRunner>();
        }

        private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
        {
            await using var command = this.dataSource.CreateCommand(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = $1");
            command.Parameters.AddWithValue(tableName);
            return (long)(await command.ExecuteScalarAsync(cancellationToken))! > 0;
        }

        [Fact]
        public async Task MigrationUpCreatesSchemaThenDownRemovesIt()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var runner = this.CreateRunner();

            (await this.TableExistsAsync("form_submissions", cancellationToken)).ShouldBeFalse();
            (await this.TableExistsAsync("form_submissions_outbox", cancellationToken)).ShouldBeFalse();
            (await this.TableExistsAsync("VersionInfo", cancellationToken)).ShouldBeFalse();

            runner.MigrateUp();

            (await this.TableExistsAsync("form_submissions", cancellationToken)).ShouldBeTrue();
            (await this.TableExistsAsync("form_submissions_outbox", cancellationToken)).ShouldBeTrue();
            (await this.TableExistsAsync("VersionInfo", cancellationToken)).ShouldBeTrue();

            runner.MigrateDown(0);

            (await this.TableExistsAsync("form_submissions", cancellationToken)).ShouldBeFalse();
            (await this.TableExistsAsync("form_submissions_outbox", cancellationToken)).ShouldBeFalse();
        }
    }
}
