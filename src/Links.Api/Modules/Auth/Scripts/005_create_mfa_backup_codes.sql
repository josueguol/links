-- 005_create_mfa_backup_codes.sql
-- Depends on: 001_create_users.sql

CREATE TABLE IF NOT EXISTS mfa_backup_codes (
    id          BIGSERIAL       PRIMARY KEY,
    user_id     BIGINT          NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    code_hash   TEXT            NOT NULL,
    is_used     BOOLEAN         NOT NULL DEFAULT false,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_mfa_backup_codes_user_id ON mfa_backup_codes (user_id);
