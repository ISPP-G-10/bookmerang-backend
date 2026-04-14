-- ================================================================
-- Migration: Añadir campos cosméticos a user_progress
-- active_frame_id: id del marco activo elegido por el usuario
-- active_color_id: id del color de nombre activo elegido por el usuario
-- ================================================================

ALTER TABLE user_progress
    ADD COLUMN IF NOT EXISTS active_frame_id VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS active_color_id VARCHAR(64) NULL;