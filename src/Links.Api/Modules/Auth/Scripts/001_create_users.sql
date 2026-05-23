-- 001_create_users.sql
-- Depends on: nothing

CREATE TABLE IF NOT EXISTS users (
    id              BIGSERIAL       PRIMARY KEY,
    public_id       UUID            NOT NULL UNIQUE,
    email           VARCHAR(320)    NOT NULL UNIQUE,
    password_hash   TEXT            NOT NULL,
    display_name    VARCHAR(100)    NOT NULL,
    email_verified_at TIMESTAMPTZ   NULL,
    mfa_enabled     BOOLEAN         NOT NULL DEFAULT false,
    mfa_secret      TEXT            NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users (email);
CREATE INDEX idx_users_public_id ON users (public_id);
