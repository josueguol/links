-- 003_create_email_verification_tokens.sql
-- Depends on: 001_create_users.sql

CREATE TABLE IF NOT EXISTS email_verification_tokens (
    id              BIGSERIAL       PRIMARY KEY,
    user_id         BIGINT          NOT NULL REFERENCES users(id),
    token_hash      TEXT            NOT NULL,
    expires_at      TIMESTAMPTZ     NOT NULL,
    used_at         TIMESTAMPTZ     NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_email_verif_tokens_token_hash ON email_verification_tokens (token_hash);
CREATE INDEX idx_email_verif_tokens_user_id ON email_verification_tokens (user_id);
