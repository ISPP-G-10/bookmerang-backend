-- ============================================================
-- Migración: Agregar tabla de typing_indicators para chat
-- ============================================================

BEGIN;

-- Crear tabla typing_indicators
CREATE TABLE IF NOT EXISTS typing_indicators (
    id BIGSERIAL PRIMARY KEY,
    chat_id INTEGER NOT NULL,
    user_id UUID NOT NULL,
    started_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    FOREIGN KEY (chat_id) REFERENCES chats(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES base_users(id) ON DELETE CASCADE,
    
    UNIQUE(chat_id, user_id)
);

-- Crear índice para búsquedas rápidas
CREATE INDEX idx_typing_indicators_chat_id ON typing_indicators(chat_id);
CREATE INDEX idx_typing_indicators_user_id ON typing_indicators(user_id);

COMMIT;
