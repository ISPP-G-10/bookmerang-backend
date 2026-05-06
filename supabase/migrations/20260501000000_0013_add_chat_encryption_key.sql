-- Migration to add encryption_key to chats
ALTER TABLE chats ADD COLUMN IF NOT EXISTS encryption_key text DEFAULT encode(gen_random_bytes(32), 'hex');

-- Generar una clave para los chats existentes
UPDATE chats SET encryption_key = encode(gen_random_bytes(32), 'hex') WHERE encryption_key IS NULL;

ALTER TABLE chats ALTER COLUMN encryption_key SET NOT NULL;
