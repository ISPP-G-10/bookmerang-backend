DROP INDEX IF EXISTS ux_matches_user1_user2;

CREATE INDEX IF NOT EXISTS ix_matches_user1_user2
ON matches (user1_id, user2_id);
