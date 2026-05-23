-- 002_create_refresh_tokens.sql
-- Depends on: 001_create_users.sql

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id              BIGSERIAL       PRIMARY KEY,
    user_id         BIGINT          NOT NULL REFERENCES users(id),
    token_hash      TEXT            NOT NULL,
    family          UUID            NOT NULL,
    expires_at      TIMESTAMPTZ     NOT NULL,
    is_used         BOOLEAN         NOT NULL DEFAULT false,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_token_hash ON refresh_tokens (token_hash);
CREATE INDEX idx_refresh_tokens_family ON refresh_tokens (family, user_id);
CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens (user_id);
