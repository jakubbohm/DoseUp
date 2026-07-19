DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'membership') THEN
        CREATE SCHEMA membership;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS membership.__ef_migrations_history (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'membership') THEN
            CREATE SCHEMA membership;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    CREATE TABLE membership.user_accounts (
        id uuid NOT NULL,
        external_user_id text NOT NULL,
        display_name text NOT NULL,
        created_at_utc timestamp with time zone NOT NULL,
        iana_time_zone_id text NOT NULL,
        primary_contact_email_address text NOT NULL,
        primary_contact_mobile_phone_number text,
        CONSTRAINT pk_user_accounts PRIMARY KEY (id),
        CONSTRAINT ak_user_accounts_external_user_id UNIQUE (external_user_id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    CREATE TABLE membership.medication_profiles (
        id uuid NOT NULL,
        user_account_id uuid NOT NULL,
        profile_display_name text NOT NULL,
        daily_dose_limit integer NOT NULL,
        is_archived boolean NOT NULL,
        CONSTRAINT pk_medication_profiles PRIMARY KEY (id),
        CONSTRAINT ck_medication_profiles_daily_dose_limit_positive CHECK (daily_dose_limit > 0),
        CONSTRAINT fk_medication_profiles_user_accounts_user_account_id FOREIGN KEY (user_account_id) REFERENCES membership.user_accounts (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    CREATE TABLE membership.dose_log_entries (
        id uuid NOT NULL,
        medication_profile_id uuid NOT NULL,
        taken_at_utc timestamp with time zone NOT NULL,
        amount_taken numeric(9,3) NOT NULL,
        CONSTRAINT pk_dose_log_entries PRIMARY KEY (id),
        CONSTRAINT fk_dose_log_entries_medication_profiles_medication_profile_id FOREIGN KEY (medication_profile_id) REFERENCES membership.medication_profiles (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    CREATE INDEX ix_dose_log_entries_medication_profile_id_taken_at_utc ON membership.dose_log_entries (medication_profile_id, taken_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    CREATE UNIQUE INDEX ix_medication_profiles_user_account_id_profile_display_name ON membership.medication_profiles (user_account_id, profile_display_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    CREATE UNIQUE INDEX ix_user_accounts_display_name ON membership.user_accounts (display_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM membership.__ef_migrations_history WHERE "migration_id" = '20260719200807_InitialMembership') THEN
    INSERT INTO membership.__ef_migrations_history (migration_id, product_version)
    VALUES ('20260719200807_InitialMembership', '11.0.0-preview.5.26302.115');
    END IF;
END $EF$;
COMMIT;

