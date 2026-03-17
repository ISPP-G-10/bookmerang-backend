-- created_by_user_id
ALTER TABLE bookspots
DROP CONSTRAINT bookspots_created_by_user_id_fkey;

ALTER TABLE bookspots
ADD CONSTRAINT bookspots_created_by_user_id_fkey
FOREIGN KEY (created_by_user_id)
REFERENCES users(id);

-- owner_id
ALTER TABLE bookspots
DROP CONSTRAINT bookspots_owner_id_fkey;

ALTER TABLE bookspots
ADD CONSTRAINT bookspots_owner_id_fkey
FOREIGN KEY (owner_id)
REFERENCES bookdrop_users(id);
