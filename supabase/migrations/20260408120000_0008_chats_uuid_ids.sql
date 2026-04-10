-- ================================================================
-- Migration: Cambiar chats.id de INT a UUID
-- ================================================================

BEGIN;

-- Step 1: Agregar columna UUID nueva en chats
ALTER TABLE chats ADD COLUMN id_new UUID NOT NULL DEFAULT gen_random_uuid();

-- Step 2: Crear tabla temporal de mapeo int -> UUID
CREATE TEMP TABLE chat_uuid_map AS SELECT id AS old_id, id_new AS new_id FROM chats;

-- Step 3: Agregar columnas UUID nuevas en tablas dependientes
ALTER TABLE chat_participants ADD COLUMN chat_id_new UUID;
ALTER TABLE messages ADD COLUMN chat_id_new UUID;
ALTER TABLE exchanges ADD COLUMN chat_id_new UUID;
ALTER TABLE community_chats ADD COLUMN chat_id_new UUID;
ALTER TABLE typing_indicators ADD COLUMN chat_id_new UUID;

-- Step 4: Rellenar columnas nuevas a partir del mapeo
UPDATE chat_participants SET chat_id_new = m.new_id FROM chat_uuid_map m WHERE chat_participants.chat_id = m.old_id;
UPDATE messages SET chat_id_new = m.new_id FROM chat_uuid_map m WHERE messages.chat_id = m.old_id;
UPDATE exchanges SET chat_id_new = m.new_id FROM chat_uuid_map m WHERE exchanges.chat_id = m.old_id;
UPDATE community_chats SET chat_id_new = m.new_id FROM chat_uuid_map m WHERE community_chats.chat_id = m.old_id;
UPDATE typing_indicators SET chat_id_new = m.new_id FROM chat_uuid_map m WHERE typing_indicators.chat_id = m.old_id;

-- Step 5: Eliminar constraints y PKs existentes que referencian chats.id

-- community_chats: PK compuesta + UNIQUE en chat_id + FK a chats
ALTER TABLE community_chats DROP CONSTRAINT community_chats_pkey;
ALTER TABLE community_chats DROP CONSTRAINT IF EXISTS community_chats_chat_id_key;
ALTER TABLE community_chats DROP CONSTRAINT IF EXISTS community_chats_chat_id_fkey;

-- chat_participants: PK compuesta + FK a chats
ALTER TABLE chat_participants DROP CONSTRAINT chat_participants_pkey;
ALTER TABLE chat_participants DROP CONSTRAINT IF EXISTS chat_participants_chat_id_fkey;

-- messages: FK a chats
ALTER TABLE messages DROP CONSTRAINT IF EXISTS messages_chat_id_fkey;

-- exchanges: UNIQUE en chat_id (no hay FK explícita a chats en la migración original)
ALTER TABLE exchanges DROP CONSTRAINT IF EXISTS exchanges_chat_id_key;

-- typing_indicators: FK a chats + UNIQUE en (chat_id, user_id) + índice
ALTER TABLE typing_indicators DROP CONSTRAINT IF EXISTS typing_indicators_chat_id_fkey;
ALTER TABLE typing_indicators DROP CONSTRAINT IF EXISTS typing_indicators_chat_id_user_id_key;
DROP INDEX IF EXISTS idx_typing_indicators_chat_id;

-- chats: PK
ALTER TABLE chats DROP CONSTRAINT chats_pkey;

-- Step 6: Eliminar columnas int antiguas
ALTER TABLE chats DROP COLUMN id;
ALTER TABLE chat_participants DROP COLUMN chat_id;
ALTER TABLE messages DROP COLUMN chat_id;
ALTER TABLE exchanges DROP COLUMN chat_id;
ALTER TABLE community_chats DROP COLUMN chat_id;
ALTER TABLE typing_indicators DROP COLUMN chat_id;

-- Step 7: Renombrar columnas UUID a nombre definitivo
ALTER TABLE chats RENAME COLUMN id_new TO id;
ALTER TABLE chat_participants RENAME COLUMN chat_id_new TO chat_id;
ALTER TABLE messages RENAME COLUMN chat_id_new TO chat_id;
ALTER TABLE exchanges RENAME COLUMN chat_id_new TO chat_id;
ALTER TABLE community_chats RENAME COLUMN chat_id_new TO chat_id;
ALTER TABLE typing_indicators RENAME COLUMN chat_id_new TO chat_id;

-- Step 8: Añadir NOT NULL donde corresponde
ALTER TABLE chat_participants ALTER COLUMN chat_id SET NOT NULL;
ALTER TABLE messages ALTER COLUMN chat_id SET NOT NULL;
ALTER TABLE exchanges ALTER COLUMN chat_id SET NOT NULL;
ALTER TABLE community_chats ALTER COLUMN chat_id SET NOT NULL;
ALTER TABLE typing_indicators ALTER COLUMN chat_id SET NOT NULL;

-- Step 9: Restaurar PKs y constraints
ALTER TABLE chats ADD PRIMARY KEY (id);
ALTER TABLE chat_participants ADD PRIMARY KEY (chat_id, user_id);
ALTER TABLE community_chats ADD PRIMARY KEY (community_id, chat_id);
ALTER TABLE community_chats ADD UNIQUE (chat_id);
ALTER TABLE exchanges ADD UNIQUE (chat_id);
CREATE UNIQUE INDEX ON typing_indicators (chat_id, user_id);

-- Step 10: Restaurar FKs
ALTER TABLE chat_participants
    ADD CONSTRAINT chat_participants_chat_id_fkey
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE;

ALTER TABLE messages
    ADD CONSTRAINT messages_chat_id_fkey
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE;

ALTER TABLE community_chats
    ADD CONSTRAINT community_chats_chat_id_fkey
    FOREIGN KEY (chat_id) REFERENCES chats(id);

ALTER TABLE typing_indicators
    ADD CONSTRAINT typing_indicators_chat_id_fkey
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE;

-- Step 11: Restaurar índices
CREATE INDEX idx_typing_indicators_chat_id ON typing_indicators(chat_id);

COMMIT;
