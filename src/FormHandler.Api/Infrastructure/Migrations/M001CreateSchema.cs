using FluentMigrator;

namespace FormHandler.Api.Infrastructure.Migrations
{
    [Migration(20260311_0001)]
    public sealed class M001CreateSchema : Migration
    {
        public override void Up()
        {
            this.Execute.Sql("""
            DO $$
            BEGIN
                BEGIN
                    CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;

                BEGIN
                    CREATE EXTENSION IF NOT EXISTS pg_cron;
                    PERFORM cron.schedule(
                        'purge-old-outbox-entries',
                        '0 0 1 */6 *',
                        $cron$
                            DELETE FROM form_submissions_outbox
                            WHERE (processed_at IS NOT NULL OR failed_at IS NOT NULL)
                              AND created_at < now() - interval '6 months'
                        $cron$
                    );
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END;
            $$;

            CREATE TABLE form_submissions (
                id           UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                form_id      TEXT        NOT NULL,
                submitted_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                "values"     JSONB       NOT NULL,
                metadata     JSONB       NOT NULL,
                notified_at  TIMESTAMPTZ
            );

            CREATE INDEX ix_form_submissions_form_id
                ON form_submissions (form_id);

            CREATE TABLE form_submissions_outbox (
                id                UUID        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                submission_id     UUID        NOT NULL REFERENCES form_submissions(id),
                type              TEXT        NOT NULL,
                created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
                attempt_count     INT         NOT NULL DEFAULT 0,
                last_attempted_at TIMESTAMPTZ,
                last_error        TEXT,
                processed_at      TIMESTAMPTZ,
                failed_at         TIMESTAMPTZ
            );

            CREATE INDEX ix_form_submissions_outbox_pending
                ON form_submissions_outbox (created_at)
                WHERE processed_at IS NULL AND failed_at IS NULL;
            """);
        }

        public override void Down()
        {
            this.Delete.Table("form_submissions_outbox");
            this.Delete.Table("form_submissions");
            this.Execute.Sql("""
            DO $$
            BEGIN
                BEGIN
                    PERFORM cron.unschedule('purge-old-outbox-entries');
                    DROP EXTENSION IF EXISTS pg_cron;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;

                BEGIN
                    DROP EXTENSION IF EXISTS pg_stat_statements;
                EXCEPTION WHEN OTHERS THEN NULL;
                END;
            END;
            $$;
            """);
        }
    }
}