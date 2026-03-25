CREATE UNIQUE INDEX IF NOT EXISTS ux_matches_user1_user2
ON matches (user1_id, user2_id);
