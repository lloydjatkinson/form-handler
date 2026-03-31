using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FormHandler.Api.Infrastructure.HealthChecks;
using FormHandler.Tests.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace FormHandler.Tests.Infrastructure.HealthChecks
{
    [Collection("Postgres")]
    public sealed class HealthCheckTests : IAsyncLifetime
    {
        private static readonly HealthCheckContext context = new()
        {
            Registration = new HealthCheckRegistration("test", (Func<IServiceProvider, IHealthCheck>)(_ => null!), HealthStatus.Unhealthy, [])
        };

        private readonly PostgresContainerFixture fixture;

        public HealthCheckTests(PostgresContainerFixture fixture)
        {
            this.fixture = fixture;
        }

        public async ValueTask InitializeAsync()
        {
            await using var connection = await this.fixture.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "TRUNCATE form_submissions CASCADE";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        [Fact]
        public async Task PostgresConnectivityReturnsHealthyWhenDatabaseIsReachable()
        {
            var check = new PostgresConnectivityCheck(this.fixture.DataSource);

            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }

        [Fact]
        public async Task PostgresConnectionCountReturnsHealthyUnderNormalLoad()
        {
            var check = new PostgresConnectionCheck(this.fixture.DataSource);

            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }

        [Fact]
        public async Task PostgresActivityReturnsHealthyWithNoProblematicActivity()
        {
            var check = new PostgresActivityCheck(this.fixture.DataSource);

            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }

        [Fact]
        public async Task PostgresDeadlockReturnsHealthyOnFreshDatabase()
        {
            var check = new PostgresDeadlockCheck(this.fixture.DataSource);

            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }

        [Fact]
        public async Task OutboxHealthReturnsHealthyWhenOutboxIsEmpty()
        {
            var check = new OutboxHealthCheck(this.fixture.DataSource);

            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Healthy);
        }

        [Fact]
        public async Task OutboxHealthReturnsDegradedWhenDeadLetteredEntriesExist()
        {
            await using var connection = await this.fixture.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);

            await using var insertSubmission = connection.CreateCommand();
            insertSubmission.CommandText = "INSERT INTO form_submissions (form_id, \"values\", metadata) VALUES ('inbox', '{}', '{}') RETURNING id";
            var submissionId = (Guid)(await insertSubmission.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

            await using var insertOutbox = connection.CreateCommand();
            insertOutbox.CommandText = "INSERT INTO form_submissions_outbox (submission_id, type, failed_at) VALUES ($1, 'email', now())";
            insertOutbox.Parameters.AddWithValue(submissionId);
            await insertOutbox.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            var check = new OutboxHealthCheck(this.fixture.DataSource);

            var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            result.Status.ShouldBe(HealthStatus.Degraded);
        }

        [Fact]
        public async Task LoggingPublisherLogsNothingWhenHealthy()
        {
            var fakeLogger = new FakeLogger<LoggingHealthCheckPublisher>();
            var publisher = new LoggingHealthCheckPublisher(fakeLogger);
            var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), HealthStatus.Healthy, TimeSpan.Zero);

            await publisher.PublishAsync(report, TestContext.Current.CancellationToken);

            fakeLogger.Collector.GetSnapshot().ShouldBeEmpty();
        }

        [Fact]
        public async Task LoggingPublisherLogsWarningWhenDegraded()
        {
            var fakeLogger = new FakeLogger<LoggingHealthCheckPublisher>();
            var publisher = new LoggingHealthCheckPublisher(fakeLogger);
            var entries = new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = new(HealthStatus.Degraded, "slow", TimeSpan.Zero, null, null)
            };
            var report = new HealthReport(entries, HealthStatus.Degraded, TimeSpan.Zero);

            await publisher.PublishAsync(report, TestContext.Current.CancellationToken);

            fakeLogger.Collector.GetSnapshot().ShouldContain(record => record.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task LoggingPublisherLogsErrorWhenUnhealthy()
        {
            var fakeLogger = new FakeLogger<LoggingHealthCheckPublisher>();
            var publisher = new LoggingHealthCheckPublisher(fakeLogger);
            var entries = new Dictionary<string, HealthReportEntry>
            {
                ["postgres"] = new(HealthStatus.Unhealthy, "down", TimeSpan.Zero, null, null)
            };
            var report = new HealthReport(entries, HealthStatus.Unhealthy, TimeSpan.Zero);

            await publisher.PublishAsync(report, TestContext.Current.CancellationToken);

            fakeLogger.Collector.GetSnapshot().ShouldContain(record => record.Level == LogLevel.Error);
        }
    }
}
