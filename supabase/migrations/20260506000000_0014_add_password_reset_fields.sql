ALTER TABLE base_users ADD COLUMN IF NOT EXISTS password_reset_token TEXT;
ALTER TABLE base_users ADD COLUMN IF NOT EXISTS password_reset_token_expiry TIMESTAMPTZ;
