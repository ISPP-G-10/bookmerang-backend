-- ============================================================
-- SEED MÍNIMO DE PRUEBA
-- 3 usuarios (Alice, Bob, Carlos) en Sevilla
-- 6 libros (2 por usuario)
-- Match + Chat + Exchange entre Alice y Bob
-- Contraseña de todos los usuarios: Test1234
-- ============================================================

BEGIN;

-- UUIDs fijos para los 3 usuarios de prueba
DO $$
DECLARE
  alice_id  UUID := 'a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1';
  bob_id    UUID := 'b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2';
  carlos_id UUID := 'c3c3c3c3-c3c3-c3c3-c3c3-c3c3c3c3c3c3';
  pwd_hash  TEXT := crypt('Test1234', gen_salt('bf'));
  now_ts    TIMESTAMPTZ := NOW();
BEGIN

-- ──────────────────────────────────────────────────────────────
-- 0. AUTH
-- ──────────────────────────────────────────────────────────────
INSERT INTO auth.users (id, instance_id, aud, role, email, encrypted_password,
  email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
  created_at, updated_at, is_anonymous, is_sso_user,
  confirmation_token, recovery_token,
  email_change, email_change_token_new, email_change_token_current,
  reauthentication_token, phone_change, phone_change_token)
VALUES
  (alice_id,  '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated',
   'alice@test.com',  pwd_hash, now_ts,
   '{"provider":"email","providers":["email"]}', '{}', now_ts, now_ts, false, false,
   '', '', '', '', '', '', '', ''),
  (bob_id,    '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated',
   'bob@test.com',    pwd_hash, now_ts,
   '{"provider":"email","providers":["email"]}', '{}', now_ts, now_ts, false, false,
   '', '', '', '', '', '', '', ''),
  (carlos_id, '00000000-0000-0000-0000-000000000000', 'authenticated', 'authenticated',
   'carlos@test.com', pwd_hash, now_ts,
   '{"provider":"email","providers":["email"]}', '{}', now_ts, now_ts, false, false,
   '', '', '', '', '', '', '', '')
ON CONFLICT (id) DO NOTHING;

INSERT INTO auth.identities (id, user_id, provider_id, provider, identity_data, last_sign_in_at, created_at, updated_at)
VALUES
  (gen_random_uuid(), alice_id,  alice_id::text,  'email',
   jsonb_build_object('sub', alice_id::text,  'email', 'alice@test.com',  'email_verified', true),
   now_ts, now_ts, now_ts),
  (gen_random_uuid(), bob_id,    bob_id::text,    'email',
   jsonb_build_object('sub', bob_id::text,    'email', 'bob@test.com',    'email_verified', true),
   now_ts, now_ts, now_ts),
  (gen_random_uuid(), carlos_id, carlos_id::text, 'email',
   jsonb_build_object('sub', carlos_id::text, 'email', 'carlos@test.com', 'email_verified', true),
   now_ts, now_ts, now_ts)
ON CONFLICT (provider_id, provider) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 1. LANGUAGES
-- ──────────────────────────────────────────────────────────────
INSERT INTO languages (id, language) VALUES
  (1, 'Español'),
  (2, 'Inglés')
ON CONFLICT (language) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 2. BASE_USERS
-- ──────────────────────────────────────────────────────────────
INSERT INTO base_users (id, supabase_id, email, username, nombre, foto_perfil_url, type, location, created_at, updated_at) VALUES
  (alice_id,  alice_id::text,  'alice@test.com',  'alice_reader', 'Alice García',   '', 2, ST_MakePoint(-5.9845, 37.3891)::geography, now_ts, now_ts),
  (bob_id,    bob_id::text,    'bob@test.com',    'bob_books',    'Bob Martínez',   '', 2, ST_MakePoint(-5.9700, 37.3950)::geography, now_ts, now_ts),
  (carlos_id, carlos_id::text, 'carlos@test.com', 'carlos_lit',   'Carlos López',   '', 2, ST_MakePoint(-6.0020, 37.3800)::geography, now_ts, now_ts)
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 3. USERS
-- ──────────────────────────────────────────────────────────────
INSERT INTO users (id, rating_mean, finished_exchanges) VALUES
  (alice_id,  0, 0),
  (bob_id,    0, 0),
  (carlos_id, 0, 0)
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 4. USER_PREFERENCES
-- ──────────────────────────────────────────────────────────────
INSERT INTO user_preferences (id, user_id, location, radio_km, extension, created_at, updated_at) VALUES
  (1, alice_id,  ST_MakePoint(-5.9845, 37.3891)::geography, 15, 'MEDIUM', now_ts, now_ts),
  (2, bob_id,    ST_MakePoint(-5.9700, 37.3950)::geography, 10, 'LONG',   now_ts, now_ts),
  (3, carlos_id, ST_MakePoint(-6.0020, 37.3800)::geography, 20, 'SHORT',  now_ts, now_ts)
ON CONFLICT (user_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 5. USER_PREFERENCES_GENRES
-- ──────────────────────────────────────────────────────────────
INSERT INTO user_preferences_genres (preferences_id, genre_id) VALUES
  (1, 1), (1, 3), (1, 4),   -- Alice: Ficción, Fantasía, Romance
  (2, 1), (2, 5), (2, 6),   -- Bob:   Ficción, Misterio, Ciencia ficción
  (3, 3), (3, 12), (3, 5)   -- Carlos: Fantasía, Terror, Misterio
ON CONFLICT (preferences_id, genre_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 6. USER_PROGRESS
-- ──────────────────────────────────────────────────────────────
INSERT INTO user_progress (user_id, xp_total, streak_weeks, updated_at) VALUES
  (alice_id,  0, 0, now_ts),
  (bob_id,    0, 0, now_ts),
  (carlos_id, 0, 0, now_ts)
ON CONFLICT (user_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 7. BOOKS (2 por usuario)
-- ──────────────────────────────────────────────────────────────
INSERT INTO books (id, owner_id, isbn, titulo, autor, editorial, num_paginas, cover, condition, observaciones, status, created_at, updated_at) VALUES
  (1, alice_id,  '9788408253297', 'Cien años de soledad',    'Gabriel García Márquez', 'DeBolsillo',    471,  'PAPERBACK', 'GOOD',       'Algunas páginas con marcas de lápiz', 'PUBLISHED', now_ts, now_ts),
  (2, alice_id,  '9788445000663', 'El Señor de los Anillos', 'J.R.R. Tolkien',        'Minotauro',     1200, 'HARDCOVER', 'VERY_GOOD',  NULL,                                  'PUBLISHED', now_ts, now_ts),
  (3, bob_id,    '9788420412146', 'El nombre de la rosa',    'Umberto Eco',           'DeBolsillo',    640,  'PAPERBACK', 'LIKE_NEW',   'Sin uso, regalo duplicado',            'PUBLISHED', now_ts, now_ts),
  (4, bob_id,    '9780316769488', '1984',                    'George Orwell',         'Signet Classic', 328,  'PAPERBACK', 'ACCEPTABLE', 'Lomo algo dañado',                     'PUBLISHED', now_ts, now_ts),
  (5, carlos_id, '9788408063216', 'La sombra del viento',    'Carlos Ruiz Zafón',     'Planeta',       576,  'PAPERBACK', 'GOOD',       NULL,                                   'PUBLISHED', now_ts, now_ts),
  (6, carlos_id, '9788401352836', 'It',                      'Stephen King',          'Plaza & Janés', 1504, 'HARDCOVER', 'VERY_GOOD',  'Edición de coleccionista',             'PUBLISHED', now_ts, now_ts)
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 7b. BOOK_PHOTOS
-- ──────────────────────────────────────────────────────────────
INSERT INTO book_photos (id, book_id, url, orden) VALUES
  (1, 1, 'https://covers.openlibrary.org/b/id/12703917-L.jpg', 0),
  (2, 2, 'https://covers.openlibrary.org/b/id/14122138-L.jpg', 0),
  (3, 3, 'https://covers.openlibrary.org/b/id/1055772-L.jpg',  0),
  (4, 4, 'https://covers.openlibrary.org/b/id/15158861-L.jpg',  0),
  (5, 5, 'https://covers.openlibrary.org/b/id/10107644-L.jpg', 0),
  (6, 6, 'https://covers.openlibrary.org/b/id/14655624-L.jpg',  0)
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 8. BOOKS_GENRES
-- ──────────────────────────────────────────────────────────────
INSERT INTO books_genres (book_id, genre_id) VALUES
  (1, 1),          -- Cien años de soledad → Ficción
  (2, 3),          -- El Señor de los Anillos → Fantasía
  (3, 5), (3, 1),  -- El nombre de la rosa → Misterio, Ficción
  (4, 6), (4, 1),  -- 1984 → Ciencia ficción, Ficción
  (5, 1), (5, 5),  -- La sombra del viento → Ficción, Misterio
  (6, 12)          -- It → Terror
ON CONFLICT (book_id, genre_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 9. BOOKS_LANGUAGES
-- ──────────────────────────────────────────────────────────────
INSERT INTO books_languages (book_id, language_id) VALUES
  (1, 1), (2, 1), (3, 1), (4, 2), (5, 1), (6, 1)
ON CONFLICT (book_id, language_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 10. SWIPES: Alice ↔ Bob (RIGHT mutuos)
-- ──────────────────────────────────────────────────────────────
INSERT INTO swipes (id, swiper_id, book_id, direction, created_at) VALUES
  (1, alice_id, 3, 'RIGHT', now_ts - INTERVAL '2 hours'),
  (2, bob_id,   1, 'RIGHT', now_ts - INTERVAL '1 hour')
ON CONFLICT (swiper_id, book_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 11. MATCH: Alice ↔ Bob
-- ──────────────────────────────────────────────────────────────
INSERT INTO matches (id, user1_id, user2_id, book1_id, book2_id, status, created_at) VALUES
  (1, alice_id, bob_id, 1, 3, 'CHAT_CREATED', now_ts - INTERVAL '1 hour')
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 12. CHAT + PARTICIPANTES
-- ──────────────────────────────────────────────────────────────
INSERT INTO chats (id, type, created_at) VALUES
  (1, 'EXCHANGE', now_ts - INTERVAL '1 hour')
ON CONFLICT (id) DO NOTHING;

INSERT INTO chat_participants (chat_id, user_id, joined_at) VALUES
  (1, alice_id, now_ts - INTERVAL '1 hour'),
  (1, bob_id,   now_ts - INTERVAL '1 hour')
ON CONFLICT (chat_id, user_id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 13. MESSAGES
-- ──────────────────────────────────────────────────────────────
INSERT INTO messages (id, chat_id, sender_id, body, sent_at) VALUES
  (1, 1, alice_id, 'Hola Bob! Me interesa mucho tu libro El nombre de la rosa. Quedamos para intercambiar?', now_ts - INTERVAL '50 minutes'),
  (2, 1, bob_id,   'Genial Alice! A mi me encantaria Cien años de soledad. Te viene bien el centro de Sevilla?', now_ts - INTERVAL '45 minutes')
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 14. EXCHANGE (en negociación)
-- ──────────────────────────────────────────────────────────────
INSERT INTO exchanges (id, chat_id, match_id, status, created_at, updated_at) VALUES
  (1, 1, 1, 'NEGOTIATING', now_ts - INTERVAL '45 minutes', now_ts - INTERVAL '45 minutes')
ON CONFLICT (id) DO NOTHING;

-- ──────────────────────────────────────────────────────────────
-- 15. EXCHANGE_MEETING
-- ──────────────────────────────────────────────────────────────
INSERT INTO exchange_meetings (id, exchange_id, mode, custom_location, proposer_id, status, scheduled_at) VALUES
  (1, 1, 'CUSTOM', ST_MakePoint(-5.9845, 37.3891)::geography, alice_id, 'PROPOSAL', now_ts + INTERVAL '1 day')
ON CONFLICT (id) DO NOTHING;



END $$;

COMMIT;
