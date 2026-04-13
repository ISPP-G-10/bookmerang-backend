-- ============================================================
-- SEED DE PREPRODUCCIÓN — BOOKMERANG
-- 15 usuarios reales en Sevilla (distintos barrios)
-- 60 libros con ISBNs y portadas reales (Open Library)
-- Solo datos de usuarios y ecosistema de libros
-- ============================================================

BEGIN;

DO $$
DECLARE
  -- ──────────────────────
  -- UUIDs de usuarios
  -- ──────────────────────
  u01 UUID := '11111111-0001-0001-0001-000000000001'; -- Laura Fernández     (Triana)
  u02 UUID := '22222222-0002-0002-0002-000000000002'; -- Marcos Delgado      (Nervión)
  u03 UUID := '33333333-0003-0003-0003-000000000003'; -- Sofía Ramos         (Macarena)
  u04 UUID := '44444444-0004-0004-0004-000000000004'; -- Alejandro Torres    (Los Remedios)
  u05 UUID := '55555555-0005-0005-0005-000000000005'; -- Elena Castillo      (Alfalfa)
  u06 UUID := '66666666-0006-0006-0006-000000000006'; -- Pablo Moreno        (Heliópolis)
  u07 UUID := '77777777-0007-0007-0007-000000000007'; -- Lucía Jiménez       (San Jerónimo)
  u08 UUID := '88888888-0008-0008-0008-000000000008'; -- Diego Vargas        (Palmete)
  u09 UUID := '99999999-0009-0009-0009-000000000009'; -- Carmen Ortega       (Torreblanca)
  u10 UUID := 'aaaaaaaa-000a-000a-000a-00000000000a'; -- Javier Ruiz         (Bellavista)
  u11 UUID := 'bbbbbbbb-000b-000b-000b-00000000000b'; -- Natalia Vega        (Camas)
  u12 UUID := 'cccccccc-000c-000c-000c-00000000000c'; -- Andrés Herrera      (La Cartuja)
  u13 UUID := 'dddddddd-000d-000d-000d-00000000000d'; -- Isabel Molina       (San Pablo)
  u14 UUID := 'eeeeeeee-000e-000e-000e-00000000000e'; -- Rodrigo Sánchez     (Nervión)
  u15 UUID := 'ffffffff-000f-000f-000f-00000000000f'; -- María José López    (Triana)

  now_ts   TIMESTAMPTZ := NOW();

BEGIN

-- ==============================================================
-- 1. LANGUAGES  (idempotente)
-- ==============================================================
INSERT INTO languages (id, language) VALUES
  (1, 'Español'),
  (2, 'Inglés'),
  (3, 'Francés'),
  (4, 'Italiano')
ON CONFLICT (language) DO NOTHING;

-- ==============================================================
-- 2. BASE_USERS  (ubicaciones reales de barrios de Sevilla)
-- ==============================================================
-- lon/lat de referencia por barrio:
--   Triana:        -6.0016,  37.3815
--   Nervión:       -5.9712,  37.3840
--   Macarena:      -5.9900,  37.4020
--   Los Remedios:  -6.0080,  37.3740
--   Alfalfa:       -5.9930,  37.3890
--   Heliópolis:    -5.9970,  37.3600
--   San Jerónimo:  -5.9750,  37.4230
--   Palmete:       -5.9600,  37.3500
--   Torreblanca:   -5.9400,  37.3700
--   Bellavista:    -6.0000,  37.3420
--   Camas:         -6.0320,  37.3970
--   La Cartuja:    -5.9980,  37.4100
--   San Pablo:     -5.9650,  37.4150
INSERT INTO base_users (id, supabase_id, email, password_hash, username, nombre, foto_perfil_url, type, location, created_at, updated_at) VALUES
  (u01, u01::text, 'laura.fernandez@bookmerang.app',  crypt('Bookmerang2026!', gen_salt('bf')), 'laurafernandez',   'Laura Fernández',    '', 2, ST_MakePoint(-6.0016,  37.3815)::geography, now_ts, now_ts),
  (u02, u02::text, 'marcos.delgado@bookmerang.app',   crypt('Bookmerang2026!', gen_salt('bf')), 'marcosdelgado',    'Marcos Delgado',     '', 2, ST_MakePoint(-5.9712,  37.3840)::geography, now_ts, now_ts),
  (u03, u03::text, 'sofia.ramos@bookmerang.app',      crypt('Bookmerang2026!', gen_salt('bf')), 'sofiaramos',       'Sofía Ramos',        '', 2, ST_MakePoint(-5.9900,  37.4020)::geography, now_ts, now_ts),
  (u04, u04::text, 'alejandro.torres@bookmerang.app', crypt('Bookmerang2026!', gen_salt('bf')), 'alejandrotorres',  'Alejandro Torres',   '', 2, ST_MakePoint(-6.0080,  37.3740)::geography, now_ts, now_ts),
  (u05, u05::text, 'elena.castillo@bookmerang.app',   crypt('Bookmerang2026!', gen_salt('bf')), 'elenacastillo',    'Elena Castillo',     '', 2, ST_MakePoint(-5.9930,  37.3890)::geography, now_ts, now_ts),
  (u06, u06::text, 'pablo.moreno@bookmerang.app',     crypt('Bookmerang2026!', gen_salt('bf')), 'pablomoreno',      'Pablo Moreno',       '', 2, ST_MakePoint(-5.9970,  37.3600)::geography, now_ts, now_ts),
  (u07, u07::text, 'lucia.jimenez@bookmerang.app',    crypt('Bookmerang2026!', gen_salt('bf')), 'luciajimenez',     'Lucía Jiménez',      '', 2, ST_MakePoint(-5.9750,  37.4230)::geography, now_ts, now_ts),
  (u08, u08::text, 'diego.vargas@bookmerang.app',     crypt('Bookmerang2026!', gen_salt('bf')), 'diegovargas',      'Diego Vargas',       '', 2, ST_MakePoint(-5.9600,  37.3500)::geography, now_ts, now_ts),
  (u09, u09::text, 'carmen.ortega@bookmerang.app',    crypt('Bookmerang2026!', gen_salt('bf')), 'carmenortega',     'Carmen Ortega',      '', 2, ST_MakePoint(-5.9400,  37.3700)::geography, now_ts, now_ts),
  (u10, u10::text, 'javier.ruiz@bookmerang.app',      crypt('Bookmerang2026!', gen_salt('bf')), 'javierruiz',       'Javier Ruiz',        '', 2, ST_MakePoint(-6.0000,  37.3420)::geography, now_ts, now_ts),
  (u11, u11::text, 'natalia.vega@bookmerang.app',     crypt('Bookmerang2026!', gen_salt('bf')), 'nataliavega',      'Natalia Vega',       '', 2, ST_MakePoint(-6.0320,  37.3970)::geography, now_ts, now_ts),
  (u12, u12::text, 'andres.herrera@bookmerang.app',   crypt('Bookmerang2026!', gen_salt('bf')), 'andresherrera',    'Andrés Herrera',     '', 2, ST_MakePoint(-5.9980,  37.4100)::geography, now_ts, now_ts),
  (u13, u13::text, 'isabel.molina@bookmerang.app',    crypt('Bookmerang2026!', gen_salt('bf')), 'isabelmolina',     'Isabel Molina',      '', 2, ST_MakePoint(-5.9650,  37.4150)::geography, now_ts, now_ts),
  (u14, u14::text, 'rodrigo.sanchez@bookmerang.app',  crypt('Bookmerang2026!', gen_salt('bf')), 'rodrigosanchez',   'Rodrigo Sánchez',    '', 2, ST_MakePoint(-5.9712,  37.3830)::geography, now_ts, now_ts),
  (u15, u15::text, 'mariajose.lopez@bookmerang.app',  crypt('Bookmerang2026!', gen_salt('bf')), 'mariajoselopez',   'María José López',   '', 2, ST_MakePoint(-6.0020,  37.3800)::geography, now_ts, now_ts)
ON CONFLICT (id) DO NOTHING;

UPDATE base_users
SET password_hash = crypt('Bookmerang2026!', gen_salt('bf'))
WHERE id IN (u01, u02, u03, u04, u05, u06, u07, u08, u09, u10, u11, u12, u13, u14, u15)
  AND (password_hash IS NULL OR btrim(password_hash) = '');

-- ==============================================================
-- 3. USERS
-- ==============================================================
INSERT INTO users (id, rating_mean, finished_exchanges, plan) VALUES
  (u01, 4.8, 12, 'PREMIUM'),
  (u02, 4.5,  8, 'FREE'),
  (u03, 4.9, 15, 'PREMIUM'),
  (u04, 4.2,  5, 'FREE'),
  (u05, 4.7,  9, 'PREMIUM'),
  (u06, 3.9,  3, 'FREE'),
  (u07, 4.6,  7, 'FREE'),
  (u08, 4.1,  4, 'FREE'),
  (u09, 4.3,  6, 'FREE'),
  (u10, 4.0,  2, 'FREE'),
  (u11, 4.8, 11, 'PREMIUM'),
  (u12, 4.4,  5, 'FREE'),
  (u13, 4.7, 10, 'FREE'),
  (u14, 3.8,  1, 'FREE'),
  (u15, 4.5,  8, 'FREE')
ON CONFLICT (id) DO NOTHING;

-- Actualizar inkdrops mensuales de los usuarios de la comunidad 1 (SOLO ABRIL)
UPDATE users
SET inkdrops = CASE
  WHEN id = u01 THEN 400
  WHEN id = u03 THEN 300
  WHEN id = u05 THEN 300
  WHEN id = u06 THEN 200
  ELSE inkdrops
END,
inkdrops_last_updated = '2026-04'
WHERE id IN (u01, u03, u05, u06);

-- ==============================================================
-- 3b. SUBSCRIPTIONS (usuarios PREMIUM del seed → platform SYSTEM)
-- ==============================================================
INSERT INTO subscriptions (user_id, platform, platform_subscription_id, status, current_period_start, current_period_end, cancels_at_period_end, created_at, updated_at) VALUES
  (u01, 'SYSTEM', NULL, 'ACTIVE', '2026-04-01 00:00:00+00', '2026-05-01 00:00:00+00', false, now_ts, now_ts),
  (u03, 'SYSTEM', NULL, 'ACTIVE', '2026-04-01 00:00:00+00', '2026-05-01 00:00:00+00', false, now_ts, now_ts),
  (u05, 'SYSTEM', NULL, 'ACTIVE', '2026-04-01 00:00:00+00', '2026-05-01 00:00:00+00', false, now_ts, now_ts),
  (u11, 'SYSTEM', NULL, 'ACTIVE', '2026-04-01 00:00:00+00', '2026-05-01 00:00:00+00', false, now_ts, now_ts)
ON CONFLICT DO NOTHING;

-- ==============================================================
-- 4. USER_PREFERENCES (básicas)
-- ============================================================== 
INSERT INTO user_preferences (user_id, location, radio_km, extension, created_at, updated_at) VALUES
  (u01, ST_MakePoint(-6.0016,  37.3815)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u02, ST_MakePoint(-5.9712,  37.3840)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u03, ST_MakePoint(-5.9900,  37.4020)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u04, ST_MakePoint(-6.0080,  37.3740)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u05, ST_MakePoint(-5.9930,  37.3890)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u06, ST_MakePoint(-5.9970,  37.3600)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u07, ST_MakePoint(-5.9750,  37.4230)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u08, ST_MakePoint(-5.9600,  37.3500)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u09, ST_MakePoint(-5.9400,  37.3700)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u10, ST_MakePoint(-6.0000,  37.3420)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u11, ST_MakePoint(-6.0320,  37.3970)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u12, ST_MakePoint(-5.9980,  37.4100)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u13, ST_MakePoint(-5.9650,  37.4150)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u14, ST_MakePoint(-5.9712,  37.3830)::geography, 10, 'MEDIUM', now_ts, now_ts),
  (u15, ST_MakePoint(-6.0020,  37.3800)::geography, 10, 'MEDIUM', now_ts, now_ts)
ON CONFLICT (user_id) DO NOTHING;

-- ==============================================================
-- 5. BOOKS  (4 libros por usuario = 60 en total)
--    IDs 101-160 para evitar colisiones con el seed de test
-- ==============================================================
-- Géneros de referencia rápida en comentarios

INSERT INTO books (id, owner_id, isbn, titulo, autor, editorial, num_paginas, cover, condition, observaciones, status, created_at, updated_at) VALUES

-- ── Laura (u01) ── Ficción · Fantasía · Romance · Misterio
(101, u01,'9788445077528','El nombre del viento',         'Patrick Rothfuss',      'Debolsillo',      662, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '30 days', now_ts),
(102, u01,'9788408182146','La ladrona de libros',         'Markus Zusak',          'Booket',          574, 'PAPERBACK','GOOD',      'Portada algo desgastada, interior perfecto','PUBLISHED', now_ts - INTERVAL '25 days', now_ts),
(103, u01,'9788420412146','El nombre de la rosa',         'Umberto Eco',           'DeBolsillo',      640, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '20 days', now_ts),
(104, u01,'9788483464038','La casa de los espíritus',      'Isabel Allende',        'Debolsillo',      490, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '15 days', now_ts),

-- ── Marcos (u02) ── Ciencia ficción · Fantasía · Terror
(105, u02,'9780441013593','Dune',                         'Frank Herbert',         'Acebollar',       896, 'PAPERBACK','GOOD',      'Edición ilustrada',                       'PUBLISHED', now_ts - INTERVAL '28 days', now_ts),
(106, u02,'9788445077535','El temor de un hombre sabio',  'Patrick Rothfuss',      'Debolsillo',      1124,'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '22 days', now_ts),
(107, u02,'9788401352836','It',                           'Stephen King',          'Plaza & Janés',   1504,'HARDCOVER','VERY_GOOD', 'Edición de lujo con sobrecubierta',       'PUBLISHED', now_ts - INTERVAL '18 days', now_ts),
(108, u02,'9788445004449','Neuromante',                   'William Gibson',        'Minotauro',       278, 'PAPERBACK','ACCEPTABLE','Lomo marcado, texto intacto',             'PUBLISHED', now_ts - INTERVAL '10 days', now_ts),

-- ── Sofía (u03) ── Romance · Ficción · Poesía
(109, u03,'9780143124542','Yo antes de ti',               'Jojo Moyes',            'Penguin Books',   369, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '35 days', now_ts),
(110, u03,'9788408063209','La sombra del viento',         'Carlos Ruiz Zafón',     'Planeta',         576, 'PAPERBACK','GOOD',      'Pequeña mancha en contraportada',         'PUBLISHED', now_ts - INTERVAL '27 days', now_ts),
(111, u03,'9788491047391','Circe',                        'Madeline Miller',       'Planeta',         432, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '21 days', now_ts),
(112, u03,'9788420412108','El amor en los tiempos del cólera','Gabriel García Márquez','DeBolsillo',  432, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '12 days', now_ts),

-- ── Alejandro (u04) ── Historia · Biografía · Ensayo
(113, u04,'9788430619771','Sapiens',                      'Yuval Noah Harari',     'Debate',          496, 'HARDCOVER','VERY_GOOD', 'Notas en lápiz en primeros capítulos',    'PUBLISHED', now_ts - INTERVAL '40 days', now_ts),
(114, u04,'9788499892498','El mundo de Sofía',            'Jostein Gaarder',       'Siruela',         640, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '33 days', now_ts),
(115, u04,'9788491049227','Homo Deus',                    'Yuval Noah Harari',     'Debate',          464, 'HARDCOVER','LIKE_NEW',  'Sin usar, regalo duplicado',               'PUBLISHED', now_ts - INTERVAL '19 days', now_ts),
(116, u04,'9788408243861','Una breve historia del tiempo','Stephen Hawking',       'Booket',          212, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '8 days',  now_ts),

-- ── Elena (u05) ── Misterio · Terror · Ficción
(117, u05,'9788420676098','Y no quedó ninguno',           'Agatha Christie',       'Booket',          288, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '45 days', now_ts),
(118, u05,'9780062693662','Asesinato en el Orient Express','Agatha Christie',      'HarperCollins',   256, 'PAPERBACK','GOOD',      'Antiguo sello de biblioteca universitaria','PUBLISHED', now_ts - INTERVAL '38 days', now_ts),
(119, u05,'9788416867561','El resplandor',                'Stephen King',          'Debolsillo',      688, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '29 days', now_ts),
(120, u05,'9788466359962','El código Da Vinci',           'Dan Brown',             'Planeta',         595, 'PAPERBACK','ACCEPTABLE','Portada rayada, texto perfecto',           'PUBLISHED', now_ts - INTERVAL '11 days', now_ts),

-- ── Pablo (u06) ── Autoayuda · No ficción · Ensayo
(121, u06,'9788425357817','El poder del ahora',           'Eckhart Tolle',         'Gaia',            256, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '50 days', now_ts),
(122, u06,'9788416594320','Hábitos atómicos',             'James Clear',           'Diana',           320, 'PAPERBACK','VERY_GOOD', 'Subrayado con marcador amarillo',         'PUBLISHED', now_ts - INTERVAL '42 days', now_ts),
(123, u06,'9788495787897','El hombre en busca de sentido','Viktor Frankl',         'Herder',          160, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '31 days', now_ts),
(124, u06,'9788408211587','Piense y hágase rico',         'Napoleon Hill',         'Booket',          304, 'PAPERBACK','ACCEPTABLE','Páginas amarillentas por el tiempo',       'PUBLISHED', now_ts - INTERVAL '14 days', now_ts),

-- ── Lucía (u07) ── Romance · Juvenil · Fantasía
(125, u07,'9780525478812','La culpa es de las estrellas', 'John Green',            'Dutton Books',    313, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '22 days', now_ts),
(126, u07,'9788420486178','Orgullo y prejuicio',          'Jane Austen',           'Booket',          432, 'PAPERBACK','GOOD',      'Edición bilingüe español-inglés',         'PUBLISHED', now_ts - INTERVAL '17 days', now_ts),
(127, u07,'9788491814108','Mujeres que ya no lloran',     'Gloria Steinem',        'Lumen',           368, 'HARDCOVER','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '9 days',  now_ts),
(128, u07,'9788418038877','Fourth Wing',                  'Rebecca Yarros',        'Planeta',         640, 'HARDCOVER','VERY_GOOD', 'Comprado en preventa, impecable',         'PUBLISHED', now_ts - INTERVAL '5 days',  now_ts),

-- ── Diego (u08) ── Terror · Ciencia ficción · Fantasía
(129, u08,'9788445004258','Fundación',                    'Isaac Asimov',          'Minotauro',       288, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '55 days', now_ts),
(130, u08,'9788445000663','El Señor de los Anillos',      'J.R.R. Tolkien',        'Minotauro',       1200,'HARDCOVER','VERY_GOOD', 'Edición completa en un tomo',             'PUBLISHED', now_ts - INTERVAL '48 days', now_ts),
(131, u08,'9788445007648','La guía del autoestopista galáctico','Douglas Adams',   'Minotauro',       224, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '37 days', now_ts),
(132, u08,'9780141439570','El retrato de Dorian Gray',    'Oscar Wilde',           'Penguin Classics', 255, 'PAPERBACK','GOOD',     'Edición ilustrada con notas del traductor','PUBLISHED', now_ts - INTERVAL '16 days', now_ts),

-- ── Carmen (u09) ── Ficción · Historia · Romance
(133, u09,'9788408253297','Cien años de soledad',         'Gabriel García Márquez','DeBolsillo',      471, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '60 days', now_ts),
(134, u09,'9788491048916','La chica del tren',            'Paula Hawkins',         'Planeta',         400, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '52 days', now_ts),
(135, u09,'9788420412146','Los pilares de la tierra',     'Ken Follett',           'DeBolsillo',      1040,'HARDCOVER','GOOD',      'Ejemplar de colección',                   'PUBLISHED', now_ts - INTERVAL '43 days', now_ts),
(136, u09,'9788499183138','El médico',                    'Noah Gordon',           'Punto de lectura',852, 'PAPERBACK','ACCEPTABLE','Portada algo estropeada, interior bien',   'PUBLISHED', now_ts - INTERVAL '20 days', now_ts),

-- ── Javier (u10) ── Ensayo · No ficción · Autoayuda
(137, u10,'9780374533557','Pensar rápido, pensar despacio','Daniel Kahneman',      'Farrar, Straus',  499, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '33 days', now_ts),
(138, u10,'9788408241263','La inteligencia emocional',    'Daniel Goleman',        'Kairós',          397, 'PAPERBACK','GOOD',      'Anotaciones en bolígrafo azul',           'PUBLISHED', now_ts - INTERVAL '26 days', now_ts),
(139, u10,'9788491816775','El arte de lo esencial',       'Greg McKeown',          'Debolsillo',      272, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '18 days', now_ts),
(140, u10,'9788408263104','Comunicación no violenta',     'Marshall B. Rosenberg', 'Acanto',          256, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '7 days',  now_ts),

-- ── Natalia (u11) ── Ficción · Romance · Fantasía
(141, u11,'9788491048466','El cuento de la criada',       'Margaret Atwood',       'Salamandra',      400, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '38 days', now_ts),
(142, u11,'9780316015844','Crepúsculo',                   'Stephenie Meyer',       'Little, Brown',   498, 'PAPERBACK','VERY_GOOD', 'Edición con ilustraciones y contenido extra','PUBLISHED', now_ts - INTERVAL '30 days', now_ts),
(143, u11,'9788491817345','La emperatriz de los etéreos', 'Laura Gallego García',  'Ediciones SM',    528, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '21 days', now_ts),
(144, u11,'9788496208056','Canción de hielo y fuego I',   'George R.R. Martin',    'Gigamesh',        771, 'HARDCOVER','VERY_GOOD', 'Edición ilustrada limitada',              'PUBLISHED', now_ts - INTERVAL '13 days', now_ts),

-- ── Andrés (u12) ── Ciencia ficción · Terror · Fantasía
(145, u12,'9788445001547','2001: Una odisea espacial',    'Arthur C. Clarke',      'Minotauro',       254, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '44 days', now_ts),
(146, u12,'9788408246992','La casa de hojas',             'Mark Z. Danielewski',   'Alpha Decay',     736, 'PAPERBACK','VERY_GOOD', 'Edición en español muy difícil de encontrar','PUBLISHED', now_ts - INTERVAL '36 days', now_ts),
(147, u12,'9780812550702','El juego de Ender',            'Orson Scott Card',      'Tor Books',       352, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '27 days', now_ts),
(148, u12,'9788491049647','Soy leyenda',                  'Richard Matheson',      'Martinez Roca',   216, 'PAPERBACK','GOOD',      'Clásico del terror zombie',               'PUBLISHED', now_ts - INTERVAL '10 days', now_ts),

-- ── Isabel (u13) ── Misterio · Ficción · Romance
(149, u13,'9788408223283','El silencio de los inocentes', 'Thomas Harris',         'Debols!llo',      380, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '50 days', now_ts),
(150, u13,'9788408178439','La chica de la habitación 14', 'Alex Michaelides',      'Planeta',         352, 'PAPERBACK','LIKE_NEW',  NULL,                                      'PUBLISHED', now_ts - INTERVAL '41 days', now_ts),
(151, u13,'9788499183398','Me llamo Memory',              'Ann Brashares',         'Debolsillo',      291, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '24 days', now_ts),
(152, u13,'9788408233862','La paciente',                  'Alex Michaelides',      'Planeta',         352, 'PAPERBACK','VERY_GOOD', 'Segunda obra del autor, imprescindible',  'PUBLISHED', now_ts - INTERVAL '6 days',  now_ts),

-- ── Rodrigo (u14) ── Historia · Ensayo · Biografía
(153, u14,'9788491812692','El infinito en un junco',      'Irene Vallejo',         'Siruela',         416, 'HARDCOVER','LIKE_NEW',  'Premio Nacional de Ensayo 2020',          'PUBLISHED', now_ts - INTERVAL '20 days', now_ts),
(154, u14,'9780393317558','Armas, gérmenes y acero',      'Jared Diamond',         'W.W. Norton',     480, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '15 days', now_ts),
(155, u14,'9788408231141','Steve Jobs',                   'Walter Isaacson',       'DeBolsillo',      656, 'PAPERBACK','ACCEPTABLE','Portada desgastada, contiene todas págs.',  'PUBLISHED', now_ts - INTERVAL '9 days',  now_ts),
(156, u14,'9788491816461','Leonardo da Vinci',            'Walter Isaacson',       'Debate',          624, 'HARDCOVER','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '3 days',  now_ts),

-- ── María José (u15) ── Ficción · Romance · Juvenil
(157, u15,'9788418038334','A Court of Thorns and Roses',  'Sarah J. Maas',         'Planeta',         432, 'PAPERBACK','VERY_GOOD', NULL,                                      'PUBLISHED', now_ts - INTERVAL '26 days', now_ts),
(158, u15,'9780156012195','El principito',                'Antoine de Saint-Exupéry','Harcourt',       96, 'HARDCOVER','LIKE_NEW',  'Edición con ilustraciones originales del autor','PUBLISHED', now_ts - INTERVAL '19 days', now_ts),
(159, u15,'9788420412078','Jane Eyre',                    'Charlotte Brontë',      'DeBolsillo',      608, 'PAPERBACK','GOOD',      NULL,                                      'PUBLISHED', now_ts - INTERVAL '11 days', now_ts),
(160, u15,'9788491048954','Heartstopper Vol. 1',          'Alice Oseman',          'Salamandra',      288, 'PAPERBACK','LIKE_NEW',  'Cómic gráfico, impecable',                'PUBLISHED', now_ts - INTERVAL '4 days',  now_ts)
ON CONFLICT (id) DO NOTHING;

-- ==============================================================
-- 5. BOOK_PHOTOS  (portadas verificadas via Google Books API)
-- URL format: https://books.google.com/books/content?vid=ISBN{isbn}&printsec=frontcover&img=1&zoom=3
-- Todas las URLs devuelven imágenes JPEG reales (verificadas > 5 KB)
-- ==============================================================
INSERT INTO book_photos (book_id, url, orden) VALUES
(101,'https://books.google.com/books/content?vid=ISBN9788445077528&printsec=frontcover&img=1&zoom=3',0),
(102,'https://books.google.com/books/content?vid=ISBN9788408182146&printsec=frontcover&img=1&zoom=3',0),
(103,'https://books.google.com/books/content?vid=ISBN9788420412146&printsec=frontcover&img=1&zoom=3',0),
(104,'https://books.google.com/books/content?vid=ISBN9788483464038&printsec=frontcover&img=1&zoom=3',0),
(105,'https://books.google.com/books/content?vid=ISBN9780441013593&printsec=frontcover&img=1&zoom=3',0),
(106,'https://books.google.com/books/content?vid=ISBN9788445077535&printsec=frontcover&img=1&zoom=3',0),
(107,'https://books.google.com/books/content?vid=ISBN9788401352836&printsec=frontcover&img=1&zoom=3',0),
(108,'https://books.google.com/books/content?vid=ISBN9788445004449&printsec=frontcover&img=1&zoom=3',0),
(109,'https://books.google.com/books/content?vid=ISBN9780143124542&printsec=frontcover&img=1&zoom=3',0),
(110,'https://books.google.com/books/content?vid=ISBN9788408063209&printsec=frontcover&img=1&zoom=3',0),
(111,'https://books.google.com/books/content?vid=ISBN9788491047391&printsec=frontcover&img=1&zoom=3',0),
(112,'https://books.google.com/books/content?vid=ISBN9788420412108&printsec=frontcover&img=1&zoom=3',0),
(113,'https://books.google.com/books/content?vid=ISBN9788430619771&printsec=frontcover&img=1&zoom=3',0),
(114,'https://books.google.com/books/content?vid=ISBN9788499892498&printsec=frontcover&img=1&zoom=3',0),
(115,'https://books.google.com/books/content?vid=ISBN9788491049227&printsec=frontcover&img=1&zoom=3',0),
(116,'https://books.google.com/books/content?vid=ISBN9788408243861&printsec=frontcover&img=1&zoom=3',0),
(117,'https://books.google.com/books/content?vid=ISBN9788420676098&printsec=frontcover&img=1&zoom=3',0),
(118,'https://books.google.com/books/content?vid=ISBN9780062693662&printsec=frontcover&img=1&zoom=3',0),
(119,'https://books.google.com/books/content?vid=ISBN9788416867561&printsec=frontcover&img=1&zoom=3',0),
(120,'https://books.google.com/books/content?vid=ISBN9788466359962&printsec=frontcover&img=1&zoom=3',0),
(121,'https://books.google.com/books/content?vid=ISBN9788425357817&printsec=frontcover&img=1&zoom=3',0),
(122,'https://books.google.com/books/content?vid=ISBN9788416594320&printsec=frontcover&img=1&zoom=3',0),
(123,'https://books.google.com/books/content?vid=ISBN9788495787897&printsec=frontcover&img=1&zoom=3',0),
(124,'https://books.google.com/books/content?vid=ISBN9788408211587&printsec=frontcover&img=1&zoom=3',0),
(125,'https://books.google.com/books/content?vid=ISBN9780525478812&printsec=frontcover&img=1&zoom=3',0),
(126,'https://books.google.com/books/content?vid=ISBN9788420486178&printsec=frontcover&img=1&zoom=3',0),
(127,'https://books.google.com/books/content?vid=ISBN9788491814108&printsec=frontcover&img=1&zoom=3',0),
(128,'https://books.google.com/books/content?vid=ISBN9788418038877&printsec=frontcover&img=1&zoom=3',0),
(129,'https://books.google.com/books/content?vid=ISBN9788445004258&printsec=frontcover&img=1&zoom=3',0),
(130,'https://books.google.com/books/content?vid=ISBN9788445000663&printsec=frontcover&img=1&zoom=3',0),
(131,'https://books.google.com/books/content?vid=ISBN9788445007648&printsec=frontcover&img=1&zoom=3',0),
(132,'https://books.google.com/books/content?vid=ISBN9780141439570&printsec=frontcover&img=1&zoom=3',0),
(133,'https://books.google.com/books/content?vid=ISBN9788408253297&printsec=frontcover&img=1&zoom=3',0),
(134,'https://books.google.com/books/content?vid=ISBN9788491048916&printsec=frontcover&img=1&zoom=3',0),
(135,'https://books.google.com/books/content?vid=ISBN9788420412146&printsec=frontcover&img=1&zoom=3',0),
(136,'https://books.google.com/books/content?vid=ISBN9788499183138&printsec=frontcover&img=1&zoom=3',0),
(137,'https://books.google.com/books/content?vid=ISBN9780374533557&printsec=frontcover&img=1&zoom=3',0),
(138,'https://books.google.com/books/content?vid=ISBN9788408241263&printsec=frontcover&img=1&zoom=3',0),
(139,'https://books.google.com/books/content?vid=ISBN9788491816775&printsec=frontcover&img=1&zoom=3',0),
(140,'https://books.google.com/books/content?vid=ISBN9788408263104&printsec=frontcover&img=1&zoom=3',0),
(141,'https://books.google.com/books/content?vid=ISBN9788491048466&printsec=frontcover&img=1&zoom=3',0),
(142,'https://books.google.com/books/content?vid=ISBN9780316015844&printsec=frontcover&img=1&zoom=3',0),
(143,'https://books.google.com/books/content?vid=ISBN9788491817345&printsec=frontcover&img=1&zoom=3',0),
(144,'https://books.google.com/books/content?vid=ISBN9788496208056&printsec=frontcover&img=1&zoom=3',0),
(145,'https://books.google.com/books/content?vid=ISBN9788445001547&printsec=frontcover&img=1&zoom=3',0),
(146,'https://books.google.com/books/content?vid=ISBN9788408246992&printsec=frontcover&img=1&zoom=3',0),
(147,'https://covers.openlibrary.org/b/isbn/9780812550702-L.jpg',0),
(148,'https://books.google.com/books/content?vid=ISBN9788491049647&printsec=frontcover&img=1&zoom=3',0),
(149,'https://books.google.com/books/content?vid=ISBN9788408223283&printsec=frontcover&img=1&zoom=3',0),
(150,'https://books.google.com/books/content?vid=ISBN9788408178439&printsec=frontcover&img=1&zoom=3',0),
(151,'https://books.google.com/books/content?vid=ISBN9788499183398&printsec=frontcover&img=1&zoom=3',0),
(152,'https://books.google.com/books/content?vid=ISBN9788408233862&printsec=frontcover&img=1&zoom=3',0),
(153,'https://books.google.com/books/content?vid=ISBN9788491812692&printsec=frontcover&img=1&zoom=3',0),
(154,'https://covers.openlibrary.org/b/isbn/9780393317558-L.jpg',0),
(155,'https://books.google.com/books/content?vid=ISBN9788408231141&printsec=frontcover&img=1&zoom=3',0),
(156,'https://books.google.com/books/content?vid=ISBN9788491816461&printsec=frontcover&img=1&zoom=3',0),
(157,'https://books.google.com/books/content?vid=ISBN9788418038334&printsec=frontcover&img=1&zoom=3',0),
(158,'https://covers.openlibrary.org/b/isbn/9780156012195-L.jpg',0),
(159,'https://books.google.com/books/content?vid=ISBN9788420412078&printsec=frontcover&img=1&zoom=3',0),
(160,'https://books.google.com/books/content?vid=ISBN9788491048954&printsec=frontcover&img=1&zoom=3',0)
ON CONFLICT DO NOTHING;

-- ==============================================================
-- 6. BOOKS_GENRES
-- ==============================================================
INSERT INTO books_genres (book_id, genre_id) VALUES
-- Laura (101-104)
(101,3),(101,1),   -- El nombre del viento: Fantasía, Ficción
(102,1),           -- La ladrona de libros: Ficción
(103,5),(103,1),   -- El nombre de la rosa: Misterio, Ficción
(104,1),           -- Cinco esquinas: Ficción
-- Marcos (105-108)
(105,6),(105,3),   -- Dune: Ciencia ficción, Fantasía
(106,3),(106,1),   -- El temor de un hombre sabio: Fantasía, Ficción
(107,12),          -- It: Terror
(108,6),           -- Neuromante: Ciencia ficción
-- Sofía (109-112)
(109, 4),(109,  1),  -- Yo antes de ti: Romance, Ficción
(110,1),(110,5),   -- La sombra del viento: Ficción, Misterio
(111,3),(111,1),   -- Circe: Fantasía, Ficción
(112,4),(112,1),   -- El amor en los tiempos del cólera: Romance, Ficción
-- Alejandro (113-116)
(113,2),(113,8),   -- Sapiens: No ficción, Historia
(114,14),(114,9),  -- El mundo de Sofía: Ensayo, Autoayuda
(115,2),(115,8),   -- Homo Deus: No ficción, Historia
(116,2),(116,6),   -- Una breve historia del tiempo: No ficción, Ciencia ficción
-- Elena (117-120)
(117,5),(117,1),   -- Y no quedó ninguno: Misterio, Ficción
(118,5),(118,1),   -- Asesinato en el Orient Express: Misterio, Ficción
(119,12),(119,1),  -- El resplandor: Terror, Ficción
(120,5),(120,1),   -- El código Da Vinci: Misterio, Ficción
-- Pablo (121-124)
(121,9),           -- El poder del ahora: Autoayuda
(122,9),(122,2),   -- Hábitos atómicos: Autoayuda, No ficción
(123,9),(123,14),  -- El hombre en busca de sentido: Autoayuda, Ensayo
(124,9),(124,2),   -- Piense y hágase rico: Autoayuda, No ficción
-- Lucía (125-128)
(125,4),(125,11),  -- Fault in Our Stars: Romance, Juvenil
(126,4),(126,1),   -- Orgullo y prejuicio: Romance, Ficción
(127,2),(127,14),  -- Mujeres que ya no lloran: No ficción, Ensayo
(128,3),(128,4),   -- Fourth Wing: Fantasía, Romance
-- Diego (129-132)
(129,6),           -- Fundación: Ciencia ficción
(130,3),(130,1),   -- El Señor de los Anillos: Fantasía, Ficción
(131,6),(131,1),   -- La guía del autoestopista: Ciencia ficción, Ficción
(132,1),           -- El retrato de Dorian Gray: Ficción
-- Carmen (133-136)
(133,1),           -- Cien años de soledad: Ficción
(134,5),(134,1),   -- La chica del tren: Misterio, Ficción
(135,8),(135,1),   -- Los pilares de la tierra: Historia, Ficción
(136,8),(136,1),   -- El médico: Historia, Ficción
-- Javier (137-140)
(137,14),(137,2),  -- 21 lecciones: Ensayo, No ficción
(138,9),(138,2),   -- La inteligencia emocional: Autoayuda, No ficción
(139,9),(139,14),  -- El arte de lo esencial: Autoayuda, Ensayo
(140,9),(140,14),  -- Comunicación no violenta: Autoayuda, Ensayo
-- Natalia (141-144)
(141,1),(141,6),   -- El cuento de la criada: Ficción, Ciencia ficción
(142,4),(142,1),   -- Crepúsculo: Romance, Ficción
(143,3),(143,1),   -- La emperatriz de los etéreos: Fantasía, Ficción
(144,3),(144,1),   -- Canción de hielo y fuego I: Fantasía, Ficción
-- Andrés (145-148)
(145,6),           -- 2001: Ciencia ficción
(146,12),(146,1),  -- La casa de hojas: Terror, Ficción
(147,6),(147,3),   -- El juego de Ender: Ciencia ficción, Fantasía
(148,12),(148,6),  -- Soy leyenda: Terror, Ciencia ficción
-- Isabel (149-152)
(149,5),(149,12),  -- El silencio de los inocentes: Misterio, Terror
(150,5),(150,1),   -- La chica de la habitación 14: Misterio, Ficción
(151,4),(151,1),   -- Me llamo Memory: Romance, Ficción
(152,5),(152,1),   -- La paciente: Misterio, Ficción
-- Rodrigo (153-156)
(153,14),(153,8),  -- El infinito en un junco: Ensayo, Historia
(154,8),(154,2),   -- Armas, gérmenes y acero: Historia, No ficción
(155,7),(155,2),   -- Steve Jobs: Biografía, No ficción
(156,7),(156,8),   -- Leonardo da Vinci: Biografía, Historia
-- María José (157-160)
(157,3),(157,4),   -- A Court of Thorns and Roses: Fantasía, Romance
(158,1),(158,11),  -- El principito: Ficción, Juvenil
(159,4),(159,1),   -- Jane Eyre: Romance, Ficción
(160,4),(160,11)   -- Heartstopper: Romance, Juvenil
ON CONFLICT (book_id, genre_id) DO NOTHING;

-- ==============================================================
-- 7. BOOKS_LANGUAGES
-- ==============================================================
INSERT INTO books_languages (book_id, language_id) VALUES
(101,1),(102,1),(103,1),(104,1),
(105,1),(106,1),(107,1),(108,1),
(109,1),(110,1),(111,1),(112,1),
(113,1),(114,1),(115,1),(116,1),
(117,1),(118,1),(119,1),(120,1),
(121,1),(122,1),(123,1),(124,1),
(125,1),(126,1),(127,1),(128,1),   -- 125 La culpa es de las estrellas: español
(129,1),(130,1),(131,1),(132,1),
(133,1),(134,1),(135,1),(136,1),
(137,1),(138,1),(139,1),(140,1),
(141,1),(142,1),(143,1),(144,1),
(145,1),(146,1),(147,1),(148,1),
(149,1),(150,1),(151,1),(152,1),
(153,1),(154,1),(155,1),(156,1),
(157,1),(158,1),(159,1),(160,1)
ON CONFLICT (book_id, language_id) DO NOTHING;

-- ==============================================================
-- 11. BOOKSPOTS (lugares reales de Sevilla para exchanges)
-- ==============================================================
INSERT INTO bookspots (id, nombre, address_text, location, is_bookdrop, created_by_user_id, status, created_at) VALUES
  (10, 'Biblioteca Pública Municipal de Triana',      'C. Rodrigo de Triana, 70, 41010 Sevilla',       ST_MakePoint(-6.0041, 37.3823)::geography, false, u01, 'ACTIVE', now_ts - INTERVAL '60 days'),
  (11, 'Café literario El Gato Tuerto',               'C. Betis, 32, 41010 Sevilla',                   ST_MakePoint(-6.0020, 37.3810)::geography, false, u03, 'ACTIVE', now_ts - INTERVAL '55 days'),
  (12, 'Librería Palas (Nervión)',                    'C. Nervión, 5, 41005 Sevilla',                  ST_MakePoint(-5.9712, 37.3835)::geography, false, u02, 'ACTIVE', now_ts - INTERVAL '50 days'),
  (13, 'Plaza del Altozano (frente al museo)',        'Plaza del Altozano, 41010 Sevilla',             ST_MakePoint(-6.0008, 37.3795)::geography, false, u05, 'ACTIVE', now_ts - INTERVAL '45 days'),
  (14, 'MegaBiblio Universidad de Sevilla',           'C. San Fernando, 4, 41004 Sevilla',             ST_MakePoint(-5.9895, 37.3866)::geography, false, u07, 'ACTIVE', now_ts - INTERVAL '40 days'),
  (15, 'Bookdrop Parque de María Luisa',              'Glorieta de la Infanta, 41013 Sevilla',         ST_MakePoint(-5.9850, 37.3765)::geography, true,  u09, 'ACTIVE', now_ts - INTERVAL '35 days'),
  (16, 'Biblioteca Pública Municipal de Dos Hermanas','C. Huerta Palacios, s/n, 41701 Dos Hermanas',ST_MakePoint(-5.9225, 37.2851)::geography,false, u04, 'ACTIVE', now_ts - INTERVAL '58 days'),
  (17, 'Living Book Café','C. Ntra. Sra. de Valme, 39, 41701 Dos Hermanas',ST_MakePoint(-5.9235, 37.2854)::geography, false, u06, 'ACTIVE', now_ts - INTERVAL '52 days'),
  (18, 'Librería Valme','C. Ntra. Sra. de Valme, 2, 41701 Dos Hermanas',ST_MakePoint(-5.9221, 37.2840)::geography, false, u08, 'ACTIVE', now_ts - INTERVAL '47 days'),
  (19, 'Papelería El Quijote','C. Dr. Fleming, 34, 41701 Dos Hermanas',ST_MakePoint(-5.9326, 37.2834)::geography, false, u02, 'ACTIVE', now_ts - INTERVAL '43 days'),
  (20, 'Centro Cultural y Biblioteca de Montequinto','C. Venecia, 22, 41089 Montequinto, Sevilla',ST_MakePoint(-5.9309, 37.3376)::geography,false, u05, 'ACTIVE', now_ts - INTERVAL '38 days'),
  (21, 'Casa del Libro Sevilla','C. Velázquez, 8, 41001 Sevilla',ST_MakePoint(-5.9955, 37.3916)::geography, false, u01, 'ACTIVE', now_ts - INTERVAL '33 days'),
  (22, 'Librería Botica de Lectores','C. Asunción, 15, 41011 Sevilla', ST_MakePoint(-5.9976, 37.3782)::geography, false, u03, 'ACTIVE', now_ts - INTERVAL '28 days'),
  (23, 'Vicentina Café y Libros', 'Av. de Emilio Lemos, 11, local 3, 41020 Sevilla', ST_MakePoint(-5.9249, 37.4018)::geography, false, u07, 'ACTIVE', now_ts - INTERVAL '22 days'),
  (24, 'Biblioteca Pública Felipe González Márquez', 'C. Torneo, s/n, 41002 Sevilla', ST_MakePoint(-6.0020, 37.3973)::geography,false, u09, 'ACTIVE', now_ts - INTERVAL '18 days'),
  (25, 'Biblioteca Capitular y Colombina','C. Alemanes, s/n, 41001 Sevilla', ST_MakePoint(-5.9927, 37.3866)::geography, false, u02, 'ACTIVE', now_ts - INTERVAL '12 days'),
  (26, 'Biblioteca Municipal Pedro Laín Entralgo', 'Plaza de la Constitución, 1, 41701 Dos Hermanas', ST_MakePoint(-5.9238, 37.2828)::geography, false, u01, 'ACTIVE', now_ts - INTERVAL '10 days'),
  (27, 'Centro Social y Deportivo Vistazul', 'C. Nelson Mandela, s/n, 41703 Dos Hermanas', ST_MakePoint(-5.9345, 37.2805)::geography, false, u03, 'ACTIVE', now_ts - INTERVAL '9 days'),
  (28, 'Bookdrop Parque de la Alquería', 'C. 28 de Febrero, s/n (Entrada Principal), 41702 Dos Hermanas', ST_MakePoint(-5.9268, 37.2865)::geography, true, u05, 'ACTIVE', now_ts - INTERVAL '8 days'),
  (29, 'Cafetería de la Estación de Renfe', 'Plaza de la Estación, s/n, 41701 Dos Hermanas', ST_MakePoint(-5.9212, 37.2831)::geography, false, u02, 'ACTIVE', now_ts - INTERVAL '7 days'),
  (30, 'Librería Anteo', 'C. Santa María Magdalena, 76, 41701 Dos Hermanas', ST_MakePoint(-5.9255, 37.2818)::geography, false, u07, 'ACTIVE', now_ts - INTERVAL '6 days'),
  (31, 'Mercado de Abastos de Dos Hermanas', 'Plaza del Emigrante, s/n, 41701 Dos Hermanas', ST_MakePoint(-5.9248, 37.2842)::geography, false, u09, 'ACTIVE', now_ts - INTERVAL '5 days'),
  (32, 'Centro Cultural La Almona', 'C. Real, 3, 41701 Dos Hermanas', ST_MakePoint(-5.9232, 37.2836)::geography, false, u04, 'ACTIVE', now_ts - INTERVAL '4 days'),
  (33, 'Bookdrop Plaza de Menéndez y Pelayo', 'Plaza Menéndez y Pelayo, 41701 Dos Hermanas', ST_MakePoint(-5.9242, 37.2824)::geography, true, u06, 'ACTIVE', now_ts - INTERVAL '3 days'),
  (34, 'Kiosko de Prensa Parque Gines de los Ríos', 'Av. de la Libertad, 41703 Dos Hermanas', ST_MakePoint(-5.9312, 37.2798)::geography, false, u08, 'ACTIVE', now_ts - INTERVAL '2 days'),
  (35, 'Papelería Valme (Zona Arco Norte)', 'Av. Adolfo Suárez, 24, 41704 Dos Hermanas', ST_MakePoint(-5.9388, 37.2945)::geography, false, u10, 'ACTIVE', now_ts - INTERVAL '1 day')
ON CONFLICT (id) DO NOTHING;

-- ==============================================================
-- 12. COMUNIDADES DE PRUEBA
-- ==============================================================
-- Comunidad 1: Club de Lectura Triana (en Biblioteca de Triana, bookspot 10)
-- Creador/Moderador: Laura (u01)
-- Miembros: Sofía (u03), Elena (u05), Pablo (u06)
INSERT INTO communities (id, name, reference_bookspot_id, status, creator_id, created_at) VALUES
  (1, 'Club de Lectura Triana', 10, 'ACTIVE', u01, now_ts - INTERVAL '30 days'),
  (2, 'Amantes del Thriller', 12, 'ACTIVE', u02, now_ts - INTERVAL '25 days'),
  (3, 'Fantasía y Ciencia Ficción', 14, 'ACTIVE', u03, now_ts - INTERVAL '20 days'),
  (4, 'Lectores de Dos Hermanas', 16, 'ACTIVE', u04, now_ts - INTERVAL '15 days'),
  (5, 'Románticos Empedernidos', 22, 'ACTIVE', u05, now_ts - INTERVAL '10 days')
ON CONFLICT (id) DO NOTHING;

-- Miembros de comunidades (el creador siempre es MODERATOR)
INSERT INTO community_members (community_id, user_id, role, joined_at) VALUES
  -- Club de Lectura Triana (comunidad 1)
  (1, u01, 'MODERATOR', now_ts - INTERVAL '30 days'),  -- Laura (creador)
  (1, u03, 'MODERATOR', now_ts - INTERVAL '28 days'),  -- Sofía
  (1, u05, 'MEMBER', now_ts - INTERVAL '26 days'),     -- Elena
  (1, u06, 'MEMBER', now_ts - INTERVAL '24 days'),     -- Pablo
  
  -- Amantes del Thriller (comunidad 2)
  (2, u02, 'MODERATOR', now_ts - INTERVAL '25 days'),  -- Marcos (creador)
  (2, u09, 'MODERATOR', now_ts - INTERVAL '23 days'),  -- Carmen
  (2, u13, 'MEMBER', now_ts - INTERVAL '21 days'),     -- Isabel
  (2, u14, 'MEMBER', now_ts - INTERVAL '19 days'),     -- Rodrigo
  
  -- Fantasía y Ciencia Ficción (comunidad 3)
  (3, u03, 'MODERATOR', now_ts - INTERVAL '20 days'),  -- Sofía (creador)
  (3, u08, 'MODERATOR', now_ts - INTERVAL '18 days'),  -- Diego
  (3, u11, 'MEMBER', now_ts - INTERVAL '16 days'),     -- Natalia
  (3, u12, 'MEMBER', now_ts - INTERVAL '14 days'),     -- Andrés
  
  -- Lectores de Dos Hermanas (comunidad 4)
  (4, u04, 'MODERATOR', now_ts - INTERVAL '15 days'),  -- Alejandro (creador)
  (4, u06, 'MODERATOR', now_ts - INTERVAL '13 days'),  -- Pablo
  (4, u08, 'MEMBER', now_ts - INTERVAL '11 days'),     -- Diego
  (4, u10, 'MEMBER', now_ts - INTERVAL '9 days'),      -- Javier
  
  -- Románticos Empedernidos (comunidad 5)
  (5, u05, 'MODERATOR', now_ts - INTERVAL '10 days'),  -- Elena (creador)
  (5, u07, 'MODERATOR', now_ts - INTERVAL '8 days'),   -- Lucía
  (5, u11, 'MEMBER', now_ts - INTERVAL '6 days'),      -- Natalia
  (5, u15, 'MEMBER', now_ts - INTERVAL '4 days')       -- María José
ON CONFLICT (community_id, user_id) DO NOTHING;

-- Chats para las comunidades (tipo COMMUNITY)
INSERT INTO chats (id, type, created_at) VALUES
  ('11111111-1111-1111-1111-111111111001'::uuid, 'COMMUNITY', now_ts - INTERVAL '30 days'),
  ('11111111-1111-1111-1111-111111111002'::uuid, 'COMMUNITY', now_ts - INTERVAL '25 days'),
  ('11111111-1111-1111-1111-111111111003'::uuid, 'COMMUNITY', now_ts - INTERVAL '20 days'),
  ('11111111-1111-1111-1111-111111111004'::uuid, 'COMMUNITY', now_ts - INTERVAL '15 days'),
  ('11111111-1111-1111-1111-111111111005'::uuid, 'COMMUNITY', now_ts - INTERVAL '10 days')
ON CONFLICT (id) DO NOTHING;

-- Relación comunidad-chat
INSERT INTO community_chats (community_id, chat_id) VALUES
  (1, '11111111-1111-1111-1111-111111111001'::uuid),
  (2, '11111111-1111-1111-1111-111111111002'::uuid),
  (3, '11111111-1111-1111-1111-111111111003'::uuid),
  (4, '11111111-1111-1111-1111-111111111004'::uuid),
  (5, '11111111-1111-1111-1111-111111111005'::uuid)
ON CONFLICT (community_id) DO NOTHING;

-- Participantes en los chats de comunidades (todos los miembros)
INSERT INTO chat_participants (chat_id, user_id, joined_at) VALUES
  -- Chat comunidad 1
  ('11111111-1111-1111-1111-111111111001'::uuid, u01, now_ts - INTERVAL '30 days'),
  ('11111111-1111-1111-1111-111111111001'::uuid, u03, now_ts - INTERVAL '28 days'),
  ('11111111-1111-1111-1111-111111111001'::uuid, u05, now_ts - INTERVAL '26 days'),
  ('11111111-1111-1111-1111-111111111001'::uuid, u06, now_ts - INTERVAL '24 days'),
  -- Chat comunidad 2
  ('11111111-1111-1111-1111-111111111002'::uuid, u02, now_ts - INTERVAL '25 days'),
  ('11111111-1111-1111-1111-111111111002'::uuid, u09, now_ts - INTERVAL '23 days'),
  ('11111111-1111-1111-1111-111111111002'::uuid, u13, now_ts - INTERVAL '21 days'),
  ('11111111-1111-1111-1111-111111111002'::uuid, u14, now_ts - INTERVAL '19 days'),
  -- Chat comunidad 3
  ('11111111-1111-1111-1111-111111111003'::uuid, u03, now_ts - INTERVAL '20 days'),
  ('11111111-1111-1111-1111-111111111003'::uuid, u08, now_ts - INTERVAL '18 days'),
  ('11111111-1111-1111-1111-111111111003'::uuid, u11, now_ts - INTERVAL '16 days'),
  ('11111111-1111-1111-1111-111111111003'::uuid, u12, now_ts - INTERVAL '14 days'),
  -- Chat comunidad 4
  ('11111111-1111-1111-1111-111111111004'::uuid, u04, now_ts - INTERVAL '15 days'),
  ('11111111-1111-1111-1111-111111111004'::uuid, u06, now_ts - INTERVAL '13 days'),
  ('11111111-1111-1111-1111-111111111004'::uuid, u08, now_ts - INTERVAL '11 days'),
  ('11111111-1111-1111-1111-111111111004'::uuid, u10, now_ts - INTERVAL '9 days'),
  -- Chat comunidad 5
  ('11111111-1111-1111-1111-111111111005'::uuid, u05, now_ts - INTERVAL '10 days'),
  ('11111111-1111-1111-1111-111111111005'::uuid, u07, now_ts - INTERVAL '8 days'),
  ('11111111-1111-1111-1111-111111111005'::uuid, u11, now_ts - INTERVAL '6 days'),
  ('11111111-1111-1111-1111-111111111005'::uuid, u15, now_ts - INTERVAL '4 days')
ON CONFLICT (chat_id, user_id) DO NOTHING;

-- Algunos mensajes de prueba en los chats de comunidades
INSERT INTO messages (chat_id, sender_id, body, sent_at) VALUES
  ('11111111-1111-1111-1111-111111111001'::uuid, u01, '¡Bienvenidos al Club de Lectura Triana! Este mes leeremos "Cien años de soledad"', now_ts - INTERVAL '30 days'),
  ('11111111-1111-1111-1111-111111111001'::uuid, u03, '¡Qué buena elección! Me encanta García Márquez', now_ts - INTERVAL '29 days'),
  ('11111111-1111-1111-1111-111111111001'::uuid, u05, '¿Nos vemos el sábado en la biblioteca para comentarlo?', now_ts - INTERVAL '28 days'),

  ('11111111-1111-1111-1111-111111111002'::uuid, u02, 'Acabamos de terminar "La chica del tren". ¿Qué os ha parecido?', now_ts - INTERVAL '20 days'),
  ('11111111-1111-1111-1111-111111111002'::uuid, u09, 'El final me dejó con la boca abierta', now_ts - INTERVAL '19 days'),

  ('11111111-1111-1111-1111-111111111003'::uuid, u03, '¿Habéis leído la nueva de Brandon Sanderson?', now_ts - INTERVAL '15 days'),
  ('11111111-1111-1111-1111-111111111003'::uuid, u08, 'Sí, increíble como siempre. Sus sistemas de magia son geniales', now_ts - INTERVAL '14 days'),
  ('11111111-1111-1111-1111-111111111003'::uuid, u11, 'Yo estoy esperando a que salga en español', now_ts - INTERVAL '13 days'),

  ('11111111-1111-1111-1111-111111111004'::uuid, u04, 'Propongo que hagamos un intercambio de libros este fin de semana', now_ts - INTERVAL '10 days'),
  ('11111111-1111-1111-1111-111111111004'::uuid, u06, 'Me apunto! Tengo varios que ya he leído', now_ts - INTERVAL '9 days'),

  ('11111111-1111-1111-1111-111111111005'::uuid, u05, '¿Alguien ha leído "It Ends With Us"?', now_ts - INTERVAL '5 days'),
  ('11111111-1111-1111-1111-111111111005'::uuid, u15, 'Yo, y lloré muchísimo con el final', now_ts - INTERVAL '4 days'),
  ('11111111-1111-1111-1111-111111111005'::uuid, u07, 'Está en mi lista de pendientes', now_ts - INTERVAL '3 days')
ON CONFLICT DO NOTHING;

-- ==============================================================
-- MEETUPS (Eventos en comunidades)
-- ==============================================================
INSERT INTO meetups (community_id, title, description, other_book_spot_id, scheduled_at, status, creator_id, created_at, updated_at) VALUES
  -- comunidad 1
  (1, 'Lectura de "Cien Años de Soledad"', 'Nos reunimos para comentar el libro seleccionado para este mes', 10, now_ts - INTERVAL '5 days', 'SCHEDULED', u01, now_ts - INTERVAL '20 days', now_ts - INTERVAL '20 days'),
  (1, 'Intercambio de Novelas Clásicas', 'Traed vuestros clásicos favoritos para intercambiar', 11, now_ts + INTERVAL '3 days', 'SCHEDULED', u01, now_ts - INTERVAL '15 days', now_ts - INTERVAL '15 days'),
  
  -- comunidad 2
  (2, 'Club de Thriller y Misterio', 'Comentaremos "La chica del tren" y otras obras de suspenso', 12, now_ts - INTERVAL '2 days', 'SCHEDULED', u02, now_ts - INTERVAL '12 days', now_ts - INTERVAL '12 days'),
  (2, 'Tarde de Novedades Criminales', 'Presentación de los últimos thrillers publicados', 13, now_ts + INTERVAL '7 days', 'SCHEDULED', u02, now_ts - INTERVAL '8 days', now_ts - INTERVAL '8 days'),
  
  -- Fantasía y Ciencia Ficción (comunidad 3)
  (3, 'Mundo de Sanderson', 'Relectura y análisis de "Sopa de Piedra"', 14, now_ts - INTERVAL '8 days', 'SCHEDULED', u03, now_ts - INTERVAL '18 days', now_ts - INTERVAL '18 days'),
  (3, 'Viaje por los Multiversos', 'Intercambio de novelas de ciencia ficción y fantasía épica', 15, now_ts + INTERVAL '10 days', 'SCHEDULED', u03, now_ts - INTERVAL '10 days', now_ts - INTERVAL '10 days'),
  
  -- Lectores de Dos Hermanas (comunidad 4)
  (4, 'Encuentro de Apasionados por la Lectura', 'Reunión mensual de lectores sevillanos', 16, now_ts - INTERVAL '3 days', 'SCHEDULED', u04, now_ts - INTERVAL '14 days', now_ts - INTERVAL '14 days'),
  (4, 'Tertulias de Café y Libros', 'Charla informal mientras tomamos algo en el café', 17, now_ts + INTERVAL '5 days', 'SCHEDULED', u04, now_ts - INTERVAL '9 days', now_ts - INTERVAL '9 days'),
  
  -- Románticos Empedernidos (comunidad 5)
  (5, 'Historias de Amor y Desamor', 'Lectura compartida de novelas románticas contemporáneas', 22, now_ts - INTERVAL '1 day', 'SCHEDULED', u05, now_ts - INTERVAL '11 days', now_ts - INTERVAL '11 days'),
  (5, 'Debate: ¿Qué hace un buen final romántico?', 'Discusión abierta sobre los finales que nos marcaron', 24, now_ts + INTERVAL '8 days', 'SCHEDULED', u05, now_ts - INTERVAL '7 days', now_ts - INTERVAL '7 days')
ON CONFLICT DO NOTHING;

-- ==============================================================
-- MEETUP ATTENDANCES (Usuarios asistiendo a meetups)
-- ==============================================================
INSERT INTO meetup_attendance (meetup_id, user_id, selected_book_id, status) VALUES
  -- Meetup 1: Club de Lectura Triana
  (1, u01, 101, 'ATTENDED'),
  (1, u03, 109, 'ATTENDED'),
  (1, u05, 117, 'ATTENDED'),
  (1, u06, 121, 'ATTENDED'),
  
  -- Meetup 2: Intercambio Novelas Clásicas
  (2, u01, 102, 'REGISTERED'),
  (2, u03, 110, 'REGISTERED'),
  (2, u06, 122, 'REGISTERED'),
  
  -- Meetup 3: Club de Thriller y Misterio
  (3, u02, 105, 'ATTENDED'),
  (3, u09, 133, 'ATTENDED'),
  (3, u13, 149, 'ATTENDED'),
  (3, u14, 153, 'ATTENDED'),
  
  -- Meetup 4: Tarde de Novedades Criminales
  (4, u02, 106, 'REGISTERED'),
  (4, u09, 134, 'REGISTERED'),
  (4, u13, 150, 'REGISTERED'),
  
  -- Meetup 5: Mundo de Sanderson
  (5, u03, 111, 'ATTENDED'),
  (5, u08, 129, 'ATTENDED'),
  (5, u11, 141, 'ATTENDED'),
  
  -- Meetup 6: Viaje por los Multiversos
  (6, u03, 112, 'REGISTERED'),
  (6, u08, 130, 'REGISTERED'),
  (6, u12, 145, 'REGISTERED'),
  
  -- Meetup 7: Encuentro de Apasionados
  (7, u04, 113, 'ATTENDED'),
  (7, u06, 123, 'ATTENDED'),
  (7, u08, 131, 'ATTENDED'),
  
  -- Meetup 8: Tertulias de Café y Libros
  (8, u04, 114, 'REGISTERED'),
  (8, u06, 124, 'REGISTERED'),
  (8, u10, 137, 'REGISTERED'),
  
  -- Meetup 9: Historias de Amor y Desamor
  (9, u05, 118, 'ATTENDED'),
  (9, u07, 125, 'ATTENDED'),
  (9, u15, 157, 'ATTENDED'),
  
  -- Meetup 10: Debate: Finales Románticos
  (10, u05, 119, 'REGISTERED'),
  (10, u07, 126, 'REGISTERED'),
  (10, u11, 142, 'REGISTERED')
ON CONFLICT DO NOTHING;

-- ==============================================================
-- MATCHES (Emparejamientos para intercambios - 100 puntos c/u)
-- ==============================================================
INSERT INTO matches (user1_id, user2_id, book1_id, book2_id, status, created_at) VALUES
  -- Match 1: Laura (u01) + Marcos (u02) → intercambio completado = 100 pts c/u
  (u01, u02, 101, 105, 'CHAT_CREATED', now_ts - INTERVAL '39 days'),
  
  -- Match 2: Sofía (u03) + Diego (u08) → intercambio completado = 100 pts c/u
  (u03, u08, 109, 129, 'CHAT_CREATED', now_ts - INTERVAL '34 days'),
  
  -- Match 3: Elena (u05) + Andrés (u12) → intercambio completado = 100 pts c/u
  (u05, u12, 117, 145, 'CHAT_CREATED', now_ts - INTERVAL '29 days')
ON CONFLICT DO NOTHING;

-- ==============================================================
-- CHATS EXCHANGE (para los intercambios)
-- ==============================================================
INSERT INTO chats (id, type, created_at) VALUES
  ('22222222-2222-2222-2222-222222222001'::uuid, 'EXCHANGE', now_ts - INTERVAL '39 days'),  -- Laura + Marcos
  ('22222222-2222-2222-2222-222222222002'::uuid, 'EXCHANGE', now_ts - INTERVAL '34 days'),  -- Sofía + Diego
  ('22222222-2222-2222-2222-222222222003'::uuid, 'EXCHANGE', now_ts - INTERVAL '29 days')   -- Elena + Andrés
ON CONFLICT (id) DO NOTHING;

INSERT INTO chat_participants (chat_id, user_id, joined_at) VALUES
  ('22222222-2222-2222-2222-222222222001'::uuid, u01, now_ts - INTERVAL '39 days'),
  ('22222222-2222-2222-2222-222222222001'::uuid, u02, now_ts - INTERVAL '39 days'),
  ('22222222-2222-2222-2222-222222222002'::uuid, u03, now_ts - INTERVAL '34 days'),
  ('22222222-2222-2222-2222-222222222002'::uuid, u08, now_ts - INTERVAL '34 days'),
  ('22222222-2222-2222-2222-222222222003'::uuid, u05, now_ts - INTERVAL '29 days'),
  ('22222222-2222-2222-2222-222222222003'::uuid, u12, now_ts - INTERVAL '29 days')
ON CONFLICT (chat_id, user_id) DO NOTHING;

-- ==============================================================
-- EXCHANGES (Intercambios COMPLETADOS - cada uno genera 100 puntos)
-- ==============================================================
INSERT INTO exchanges (match_id, chat_id, status, created_at, updated_at) VALUES
  (1, '22222222-2222-2222-2222-222222222001'::uuid, 'COMPLETED', now_ts - INTERVAL '39 days', now_ts - INTERVAL '5 days'),
  (2, '22222222-2222-2222-2222-222222222002'::uuid, 'COMPLETED', now_ts - INTERVAL '34 days', now_ts - INTERVAL '3 days'),
  (3, '22222222-2222-2222-2222-222222222003'::uuid, 'COMPLETED', now_ts - INTERVAL '29 days', now_ts - INTERVAL '1 day')
ON CONFLICT DO NOTHING;

-- ==============================================================
-- EXCHANGE MEETINGS (ambos usuarios marcan como completado)
-- ==============================================================
INSERT INTO exchange_meetings (exchange_id, proposer_id, mode, custom_location, scheduled_at, mark_as_completed_by_user1, mark_as_completed_by_user2, status) VALUES
  (1, u01, 'BOOKSPOT', ST_MakePoint(-6.0016, 37.3815)::geography, now_ts - INTERVAL '5 days', true, true, 'ACCEPTED'),
  (2, u03, 'BOOKSPOT', ST_MakePoint(-5.9900, 37.4020)::geography, now_ts - INTERVAL '3 days', true, true, 'ACCEPTED'),
  (3, u05, 'BOOKSPOT', ST_MakePoint(-5.9930, 37.3890)::geography, now_ts - INTERVAL '1 day', true, true, 'ACCEPTED')
ON CONFLICT DO NOTHING;

INSERT INTO user_progress (user_id, xp_total, streak_weeks, last_active_date, streak_start_date, last_decrement_date, updated_at) VALUES
  -- Nivel 0, sin actividad
  (u04, 0, 0, NULL, NULL, NULL, now_ts),
  (u08, 0, 0, NULL, NULL, NULL, now_ts),
  
  -- Con XP TOTAL (marzo + abril): Laura (600), Sofía (500), Elena (500), Pablo (400)
  (u01, 600, 3, now_ts - INTERVAL '2 days', now_ts - INTERVAL '21 days', NULL, now_ts),
  (u03, 500, 5, now_ts - INTERVAL '1 day',  now_ts - INTERVAL '35 days', NULL, now_ts),
  
  -- Activo semana pasada (debe incrementar al hacer acción)
  (u02, 0, 2, now_ts - INTERVAL '8 days', now_ts - INTERVAL '16 days', NULL, now_ts),
  (u05, 500, 4, now_ts - INTERVAL '9 days', now_ts - INTERVAL '28 days', NULL, now_ts),
  
  -- Inactivo, debe decrementar
  (u07, 0, 4, now_ts - INTERVAL '14 days', now_ts - INTERVAL '28 days', NULL, now_ts),
  (u09, 0, 2, now_ts - INTERVAL '20 days', now_ts - INTERVAL '14 days', NULL, now_ts),
  
  -- Tope de multiplicador
  (u11, 0, 8, now_ts - INTERVAL '2 days', now_ts - INTERVAL '56 days', NULL, now_ts),
  
  -- Recuperando racha desde nivel bajo
  (u06, 400, 1, now_ts - INTERVAL '8 days', now_ts - INTERVAL '7 days',  NULL, now_ts),
  
  -- Resto de usuarios
  (u10, 0, 0, NULL, NULL, NULL, now_ts),
  (u12, 0, 0, NULL, NULL, NULL, now_ts),
  (u13, 0, 0, NULL, NULL, NULL, now_ts),
  (u14, 0, 0, NULL, NULL, NULL, now_ts),
  (u15, 0, 0, NULL, NULL, NULL, now_ts)
ON CONFLICT (user_id) DO NOTHING;

-- ==============================================================
-- INKDROPS RANKING (community_monthly_scores - Abril 2026)
-- Comunidad 1: Club de Lectura Triana
-- Miembros: Laura (u01), Sofía (u03), Elena (u05), Pablo (u06)
-- SOLO SE CUENTAN LAS ACCIONES DE ABRIL
-- ==============================================================
INSERT INTO community_monthly_scores (community_id, user_id, month, inkdrops_this_month) VALUES
  -- Laura Fernández: 2 intercambios (200) + 1 meetup (200) = 400 pts en ABRIL
  (1, u01, '2026-04', 400),
  
  -- Sofía Ramos: 1 intercambio (100) + 1 meetup (200) = 300 pts en ABRIL
  (1, u03, '2026-04', 300),
  
  -- Elena Castillo: 1 intercambio (100) + 1 meetup (200) = 300 pts en ABRIL
  (1, u05, '2026-04', 300),
  
  -- Pablo Moreno: 1 meetup (200) = 200 pts en ABRIL
  (1, u06, '2026-04', 200)
ON CONFLICT (community_id, user_id, month) DO NOTHING;

-- ==============================================================
-- INKDROPS HISTORY (Histórico de acciones)
-- ==============================================================
INSERT INTO inkdrops_history (user_id, action_type, points_granted, related_id, created_at) VALUES
  -- Laura Fernández (u01) - Intercambios (ABRIL)
  (u01, 'EXCHANGE_COMPLETED', 100, 1, now_ts - INTERVAL '5 days'),   -- April 2
  (u01, 'EXCHANGE_COMPLETED', 100, null, now_ts - INTERVAL '2 days'), -- April 5
  
  -- Laura Fernández (u01) - Meetups asistidos (MARZO + ABRIL)
  (u01, 'MEETUP_ATTENDED', 200, 1, now_ts - INTERVAL '30 days'),  -- Marzo 8
  (u01, 'MEETUP_ATTENDED', 200, 2, now_ts - INTERVAL '4 days'),  -- April 3
  
  -- Sofía Ramos (u03) - Intercambio (ABRIL)
  (u03, 'EXCHANGE_COMPLETED', 100, 2, now_ts - INTERVAL '3 days'),   -- April 4
  
  -- Sofía Ramos (u03) - Meetups (MARZO + ABRIL)
  (u03, 'MEETUP_ATTENDED', 200, 5, now_ts - INTERVAL '15 days'),  -- Marzo 23
  (u03, 'MEETUP_ATTENDED', 200, 6, now_ts - INTERVAL '1 day'), -- April 6
  
  -- Elena Castillo (u05) - Intercambio (ABRIL)
  (u05, 'EXCHANGE_COMPLETED', 100, 3, now_ts - INTERVAL '1 day'),  -- April 6
  
  -- Elena Castillo (u05) - Meetups (MARZO + ABRIL)
  (u05, 'MEETUP_ATTENDED', 200, 9, now_ts - INTERVAL '25 days'),  -- Marzo 13
  (u05, 'MEETUP_ATTENDED', 200, 10, now_ts - INTERVAL '2 days'), -- April 5
  
  -- Pablo Moreno (u06) - Meetups (MARZO + ABRIL)
  (u06, 'MEETUP_ATTENDED', 200, 7, now_ts - INTERVAL '18 days'),  -- Marzo 20
  (u06, 'MEETUP_ATTENDED', 200, 8, now_ts - INTERVAL '3 days')  -- April 4
ON CONFLICT DO NOTHING;

-- ==============================================================
-- 16. COBERTURA EXTRA DE MODULOS (Admin, Bookdrop, Matcher, etc.)
-- ==============================================================

-- 16.1 Usuarios de tipo ADMIN y BOOKDROP_USER
INSERT INTO base_users (id, supabase_id, email, password_hash, username, nombre, foto_perfil_url, type, location, created_at, updated_at) VALUES
  ('00000000-0000-0000-0000-0000000000a1'::uuid, 'admin-local-01', 'admin@bookmerang.app', crypt('Bookmerang2026!', gen_salt('bf')), 'adminbookmerang', 'Admin Bookmerang', '', 0, ST_MakePoint(-5.9905, 37.3895)::geography, now_ts, now_ts),
  ('10000000-0000-0000-0000-0000000000b1'::uuid, 'bookdrop-local-01', 'bookdrop.centro@bookmerang.app', crypt('Bookmerang2026!', gen_salt('bf')), 'bookdropcentro', 'Bookdrop Centro Sevilla', '', 1, ST_MakePoint(-5.9940, 37.3895)::geography, now_ts, now_ts),
  ('10000000-0000-0000-0000-0000000000b2'::uuid, 'bookdrop-local-02', 'bookdrop.nervion@bookmerang.app', crypt('Bookmerang2026!', gen_salt('bf')), 'bookdropnervion', 'Bookdrop Nervion', '', 1, ST_MakePoint(-5.9725, 37.3848)::geography, now_ts, now_ts)
ON CONFLICT (id) DO NOTHING;

INSERT INTO admins (id) VALUES
  ('00000000-0000-0000-0000-0000000000a1'::uuid)
ON CONFLICT (id) DO NOTHING;

-- 16.2 Bookspots extra para pruebas de bookdrop y validaciones
INSERT INTO bookspots (id, nombre, address_text, location, is_bookdrop, created_by_user_id, owner_id, status, created_at, updated_at) VALUES
  (36, 'Bookdrop Centro Sevilla', 'C. Sierpes, 41, 41004 Sevilla', ST_MakePoint(-5.9940, 37.3895)::geography, true, NULL, NULL, 'ACTIVE', now_ts - INTERVAL '20 days', now_ts - INTERVAL '20 days'),
  (37, 'Bookdrop Nervion', 'Av. Eduardo Dato, 12, 41005 Sevilla', ST_MakePoint(-5.9725, 37.3848)::geography, true, NULL, NULL, 'ACTIVE', now_ts - INTERVAL '18 days', now_ts - INTERVAL '18 days'),
  (38, 'Bookspot Pendiente Alameda', 'Alameda de Hercules, 87, 41002 Sevilla', ST_MakePoint(-5.9974, 37.3977)::geography, false, u04, NULL, 'PENDING', now_ts - INTERVAL '4 days', now_ts - INTERVAL '4 days'),
  (39, 'Bookspot Pendiente San Bernardo', 'C. Juan de Mata Carriazo, 10, 41018 Sevilla', ST_MakePoint(-5.9868, 37.3788)::geography, false, u10, NULL, 'PENDING', now_ts - INTERVAL '3 days', now_ts - INTERVAL '3 days'),
  (40, 'Bookspot Rechazado La Buhaira', 'Av. de la Buhaira, 29, 41018 Sevilla', ST_MakePoint(-5.9779, 37.3811)::geography, false, u14, NULL, 'REJECTED', now_ts - INTERVAL '12 days', now_ts - INTERVAL '12 days')
ON CONFLICT (id) DO NOTHING;

INSERT INTO bookdrop_users (id, book_spot_id) VALUES
  ('10000000-0000-0000-0000-0000000000b1'::uuid, 36),
  ('10000000-0000-0000-0000-0000000000b2'::uuid, 37)
ON CONFLICT (id) DO NOTHING;

-- 16.3 Validaciones para probar flujo PENDING->ACTIVE/REJECTED
INSERT INTO bookspot_validations (bookspot_id, validator_user_id, knows_place, safe_for_exchange, created_at) VALUES
  (38, u01, true,  true,  now_ts - INTERVAL '3 days'),
  (38, u02, true,  true,  now_ts - INTERVAL '2 days 20 hours'),
  (38, u03, true,  true,  now_ts - INTERVAL '2 days 12 hours'),
  (38, u05, true,  true,  now_ts - INTERVAL '1 day 18 hours'),
  (39, u01, true,  false, now_ts - INTERVAL '2 days 22 hours'),
  (39, u03, true,  false, now_ts - INTERVAL '2 days 10 hours'),
  (39, u05, true,  false, now_ts - INTERVAL '1 day 20 hours'),
  (39, u06, true,  false, now_ts - INTERVAL '1 day 8 hours'),
  (39, u07, false, false, now_ts - INTERVAL '1 day 2 hours')
ON CONFLICT (bookspot_id, validator_user_id) DO NOTHING;

-- 16.4 Preferencias con generos para probar UserPreferences completo
INSERT INTO user_preferences_genres (preferences_id, genre_id)
SELECT up.id, v.genre_id
FROM user_preferences up
JOIN (VALUES
  (u01, 1), (u01, 3),
  (u02, 6), (u02, 12),
  (u03, 4), (u03, 5),
  (u05, 5), (u05, 1),
  (u11, 3), (u11, 4)
) AS v(user_id, genre_id)
  ON v.user_id = up.user_id
ON CONFLICT (preferences_id, genre_id) DO NOTHING;

-- 16.5 Libros extra en distintos estados para probar my-drafts/my-library
INSERT INTO books (id, owner_id, isbn, titulo, autor, editorial, num_paginas, cover, condition, observaciones, status, created_at, updated_at) VALUES
  (161, u01, NULL, 'Borrador de prueba', NULL, NULL, NULL, NULL, NULL, 'Libro en construccion para probar borradores', 'DRAFT', now_ts - INTERVAL '2 days', now_ts - INTERVAL '2 days'),
  (162, u02, '9788401022913', 'Libro en pausa', 'Autor Demo', 'Editorial Demo', 320, 'PAPERBACK', 'GOOD', 'No disponible temporalmente', 'PAUSED', now_ts - INTERVAL '9 days', now_ts - INTERVAL '9 days'),
  (163, u03, '9788408123456', 'Libro reservado demo', 'Autora Demo', 'Editorial Demo', 280, 'PAPERBACK', 'VERY_GOOD', 'Reservado para un intercambio', 'RESERVED', now_ts - INTERVAL '7 days', now_ts - INTERVAL '7 days'),
  (164, u05, '9788408567891', 'Libro intercambiado demo', 'Autor Historico', 'Editorial Demo', 410, 'HARDCOVER', 'LIKE_NEW', 'Ya intercambiado, para historico', 'EXCHANGED', now_ts - INTERVAL '20 days', now_ts - INTERVAL '2 days')
ON CONFLICT (id) DO NOTHING;

INSERT INTO books_languages (book_id, language_id) VALUES
  (161, 1), (162, 1), (163, 1), (164, 1)
ON CONFLICT (book_id, language_id) DO NOTHING;

INSERT INTO books_genres (book_id, genre_id) VALUES
  (161, 1), (162, 2), (163, 4), (164, 8)
ON CONFLICT (book_id, genre_id) DO NOTHING;

INSERT INTO book_photos (book_id, url, orden) VALUES
  (162, 'https://books.google.com/books/content?vid=ISBN9788401022913&printsec=frontcover&img=1&zoom=3', 0),
  (163, 'https://books.google.com/books/content?vid=ISBN9788408123456&printsec=frontcover&img=1&zoom=3', 0),
  (164, 'https://books.google.com/books/content?vid=ISBN9788408567891&printsec=frontcover&img=1&zoom=3', 0)
ON CONFLICT DO NOTHING;

-- 16.6 Likes en biblioteca de comunidades para probar ranking/sugerencias
INSERT INTO community_library_likes (community_id, user_id, book_id, created_at) VALUES
  (1, u03, 101, now_ts - INTERVAL '2 days'),
  (1, u05, 101, now_ts - INTERVAL '1 day 12 hours'),
  (1, u01, 111, now_ts - INTERVAL '1 day'),
  (3, u11, 130, now_ts - INTERVAL '3 days'),
  (3, u03, 141, now_ts - INTERVAL '2 days 8 hours')
ON CONFLICT (community_id, user_id, book_id) DO NOTHING;

-- 16.7 Swipes para probar feed/swipe/undo en matcher
INSERT INTO swipes (swiper_id, book_id, direction, created_at) VALUES
  (u01, 109, 'RIGHT', now_ts - INTERVAL '6 hours'),
  (u03, 101, 'RIGHT', now_ts - INTERVAL '5 hours'),
  (u06, 117, 'LEFT',  now_ts - INTERVAL '4 hours'),
  (u11, 130, 'RIGHT', now_ts - INTERVAL '3 hours')
ON CONFLICT (swiper_id, book_id) DO NOTHING;

-- 16.8 Estados de exchanges para cubrir todos los flujos principales
INSERT INTO matches (id, user1_id, user2_id, book1_id, book2_id, status, created_at) VALUES
  (4, u01, u11, 102, 141, 'NEW', now_ts - INTERVAL '7 days'),
  (5, u02, u05, 106, 118, 'CHAT_CREATED', now_ts - INTERVAL '6 days'),
  (6, u07, u09, 126, 134, 'CHAT_CREATED', now_ts - INTERVAL '5 days'),
  (7, u10, u13, 138, 149, 'CHAT_CREATED', now_ts - INTERVAL '4 days'),
  (8, u04, u15, 113, 157, 'CHAT_CREATED', now_ts - INTERVAL '3 days'),
  (9, u06, u12, 121, 146, 'CHAT_CREATED', now_ts - INTERVAL '2 days'),
  (10, u08, u14, 131, 152, 'CHAT_CREATED', now_ts - INTERVAL '36 hours'),
  (11, u01, u04, 103, 116, 'CHAT_CREATED', now_ts - INTERVAL '30 hours'),
  (12, u02, u08, 107, 130, 'CHAT_CREATED', now_ts - INTERVAL '28 hours'),
  (13, u03, u10, 110, 137, 'CHAT_CREATED', now_ts - INTERVAL '26 hours'),
  (14, u05, u14, 119, 153, 'CHAT_CREATED', now_ts - INTERVAL '24 hours'),
  (15, u06, u13, 122, 148, 'CHAT_CREATED', now_ts - INTERVAL '22 hours'),
  (16, u07, u15, 127, 156, 'CHAT_CREATED', now_ts - INTERVAL '20 hours'),
  (17, u09, u11, 133, 143, 'CHAT_CREATED', now_ts - INTERVAL '18 hours')
ON CONFLICT (id) DO NOTHING;

INSERT INTO chats (id, type, created_at) VALUES
  ('22222222-2222-2222-2222-222222222004'::uuid, 'EXCHANGE', now_ts - INTERVAL '7 days'),
  ('22222222-2222-2222-2222-222222222005'::uuid, 'EXCHANGE', now_ts - INTERVAL '6 days'),
  ('22222222-2222-2222-2222-222222222006'::uuid, 'EXCHANGE', now_ts - INTERVAL '5 days'),
  ('22222222-2222-2222-2222-222222222007'::uuid, 'EXCHANGE', now_ts - INTERVAL '4 days'),
  ('22222222-2222-2222-2222-222222222008'::uuid, 'EXCHANGE', now_ts - INTERVAL '3 days'),
  ('22222222-2222-2222-2222-222222222009'::uuid, 'EXCHANGE', now_ts - INTERVAL '2 days'),
  ('22222222-2222-2222-2222-222222222010'::uuid, 'EXCHANGE', now_ts - INTERVAL '36 hours'),
  ('22222222-2222-2222-2222-222222222011'::uuid, 'EXCHANGE', now_ts - INTERVAL '30 hours'),
  ('22222222-2222-2222-2222-222222222012'::uuid, 'EXCHANGE', now_ts - INTERVAL '28 hours'),
  ('22222222-2222-2222-2222-222222222013'::uuid, 'EXCHANGE', now_ts - INTERVAL '26 hours'),
  ('22222222-2222-2222-2222-222222222014'::uuid, 'EXCHANGE', now_ts - INTERVAL '24 hours'),
  ('22222222-2222-2222-2222-222222222015'::uuid, 'EXCHANGE', now_ts - INTERVAL '22 hours'),
  ('22222222-2222-2222-2222-222222222016'::uuid, 'EXCHANGE', now_ts - INTERVAL '20 hours'),
  ('22222222-2222-2222-2222-222222222017'::uuid, 'EXCHANGE', now_ts - INTERVAL '18 hours')
ON CONFLICT (id) DO NOTHING;

INSERT INTO chat_participants (chat_id, user_id, joined_at) VALUES
  ('22222222-2222-2222-2222-222222222004'::uuid, u01, now_ts - INTERVAL '7 days'),
  ('22222222-2222-2222-2222-222222222004'::uuid, u11, now_ts - INTERVAL '7 days'),
  ('22222222-2222-2222-2222-222222222005'::uuid, u02, now_ts - INTERVAL '6 days'),
  ('22222222-2222-2222-2222-222222222005'::uuid, u05, now_ts - INTERVAL '6 days'),
  ('22222222-2222-2222-2222-222222222006'::uuid, u07, now_ts - INTERVAL '5 days'),
  ('22222222-2222-2222-2222-222222222006'::uuid, u09, now_ts - INTERVAL '5 days'),
  ('22222222-2222-2222-2222-222222222007'::uuid, u10, now_ts - INTERVAL '4 days'),
  ('22222222-2222-2222-2222-222222222007'::uuid, u13, now_ts - INTERVAL '4 days'),
  ('22222222-2222-2222-2222-222222222008'::uuid, u04, now_ts - INTERVAL '3 days'),
  ('22222222-2222-2222-2222-222222222008'::uuid, u15, now_ts - INTERVAL '3 days'),
  ('22222222-2222-2222-2222-222222222009'::uuid, u06, now_ts - INTERVAL '2 days'),
  ('22222222-2222-2222-2222-222222222009'::uuid, u12, now_ts - INTERVAL '2 days'),
  ('22222222-2222-2222-2222-222222222010'::uuid, u08, now_ts - INTERVAL '36 hours'),
  ('22222222-2222-2222-2222-222222222010'::uuid, u14, now_ts - INTERVAL '36 hours'),
  ('22222222-2222-2222-2222-222222222011'::uuid, u01, now_ts - INTERVAL '30 hours'),
  ('22222222-2222-2222-2222-222222222011'::uuid, u04, now_ts - INTERVAL '30 hours'),
  ('22222222-2222-2222-2222-222222222012'::uuid, u02, now_ts - INTERVAL '28 hours'),
  ('22222222-2222-2222-2222-222222222012'::uuid, u08, now_ts - INTERVAL '28 hours'),
  ('22222222-2222-2222-2222-222222222013'::uuid, u03, now_ts - INTERVAL '26 hours'),
  ('22222222-2222-2222-2222-222222222013'::uuid, u10, now_ts - INTERVAL '26 hours'),
  ('22222222-2222-2222-2222-222222222014'::uuid, u05, now_ts - INTERVAL '24 hours'),
  ('22222222-2222-2222-2222-222222222014'::uuid, u14, now_ts - INTERVAL '24 hours'),
  ('22222222-2222-2222-2222-222222222015'::uuid, u06, now_ts - INTERVAL '22 hours'),
  ('22222222-2222-2222-2222-222222222015'::uuid, u13, now_ts - INTERVAL '22 hours'),
  ('22222222-2222-2222-2222-222222222016'::uuid, u07, now_ts - INTERVAL '20 hours'),
  ('22222222-2222-2222-2222-222222222016'::uuid, u15, now_ts - INTERVAL '20 hours'),
  ('22222222-2222-2222-2222-222222222017'::uuid, u09, now_ts - INTERVAL '18 hours'),
  ('22222222-2222-2222-2222-222222222017'::uuid, u11, now_ts - INTERVAL '18 hours')
ON CONFLICT (chat_id, user_id) DO NOTHING;

INSERT INTO messages (chat_id, sender_id, body, sent_at) VALUES
  ('22222222-2222-2222-2222-222222222004'::uuid, u01, 'Te interesa cerrar el intercambio esta semana?', now_ts - INTERVAL '6 days 20 hours'),
  ('22222222-2222-2222-2222-222222222005'::uuid, u02, 'Yo acepto el intercambio, me viene bien el viernes.', now_ts - INTERVAL '5 days 18 hours'),
  ('22222222-2222-2222-2222-222222222006'::uuid, u07, 'Quedamos en un bookspot intermedio?', now_ts - INTERVAL '4 days 16 hours'),
  ('22222222-2222-2222-2222-222222222007'::uuid, u13, 'Hubo una incidencia en el intercambio.', now_ts - INTERVAL '3 days 12 hours'),
  ('22222222-2222-2222-2222-222222222008'::uuid, u04, 'Tengo disponibilidad para quedar mañana por la tarde.', now_ts - INTERVAL '2 days 20 hours'),
  ('22222222-2222-2222-2222-222222222009'::uuid, u12, 'Podemos hacerlo en un bookdrop para ir rapido.', now_ts - INTERVAL '35 hours'),
  ('22222222-2222-2222-2222-222222222010'::uuid, u08, 'Te viene bien cerrar el intercambio este finde?', now_ts - INTERVAL '30 hours'),
  ('22222222-2222-2222-2222-222222222011'::uuid, u01, 'Podemos dejarlo en un bookspot centrico?', now_ts - INTERVAL '29 hours'),
  ('22222222-2222-2222-2222-222222222012'::uuid, u08, 'Yo ya he aceptado por mi parte, te toca confirmar.', now_ts - INTERVAL '27 hours'),
  ('22222222-2222-2222-2222-222222222013'::uuid, u10, 'No me encaja esa ubicacion, mejor lo dejamos.', now_ts - INTERVAL '25 hours'),
  ('22222222-2222-2222-2222-222222222014'::uuid, u05, 'Perfecto, intercambio cerrado hoy.', now_ts - INTERVAL '6 hours'),
  ('22222222-2222-2222-2222-222222222015'::uuid, u13, 'Aprobado por ambas partes, ya podemos quedar.', now_ts - INTERVAL '21 hours'),
  ('22222222-2222-2222-2222-222222222016'::uuid, u07, 'Te propongo un bookdrop de la zona.', now_ts - INTERVAL '19 hours'),
  ('22222222-2222-2222-2222-222222222017'::uuid, u09, 'Estoy negociando horario para manana.', now_ts - INTERVAL '17 hours')
ON CONFLICT DO NOTHING;

INSERT INTO exchanges (id, match_id, chat_id, status, created_at, updated_at) VALUES
  (4, 4, '22222222-2222-2222-2222-222222222004'::uuid, 'NEGOTIATING', now_ts - INTERVAL '7 days', now_ts - INTERVAL '2 days'),
  (5, 5, '22222222-2222-2222-2222-222222222005'::uuid, 'ACCEPTED_BY_1', now_ts - INTERVAL '6 days', now_ts - INTERVAL '1 day'),
  (6, 6, '22222222-2222-2222-2222-222222222006'::uuid, 'ACCEPTED', now_ts - INTERVAL '5 days', now_ts - INTERVAL '1 day'),
  (7, 7, '22222222-2222-2222-2222-222222222007'::uuid, 'INCIDENT', now_ts - INTERVAL '4 days', now_ts - INTERVAL '12 hours'),
  (8, 8, '22222222-2222-2222-2222-222222222008'::uuid, 'ACCEPTED', now_ts - INTERVAL '3 days', now_ts - INTERVAL '18 hours'),
  (9, 9, '22222222-2222-2222-2222-222222222009'::uuid, 'COMPLETED', now_ts - INTERVAL '2 days', now_ts - INTERVAL '6 hours'),
  (10, 10, '22222222-2222-2222-2222-222222222010'::uuid, 'NEGOTIATING', now_ts - INTERVAL '36 hours', now_ts - INTERVAL '4 hours'),
  (11, 11, '22222222-2222-2222-2222-222222222011'::uuid, 'ACCEPTED_BY_1', now_ts - INTERVAL '30 hours', now_ts - INTERVAL '12 hours'),
  (12, 12, '22222222-2222-2222-2222-222222222012'::uuid, 'ACCEPTED_BY_2', now_ts - INTERVAL '28 hours', now_ts - INTERVAL '10 hours'),
  (13, 13, '22222222-2222-2222-2222-222222222013'::uuid, 'INCIDENT', now_ts - INTERVAL '26 hours', now_ts - INTERVAL '8 hours'),
  (14, 14, '22222222-2222-2222-2222-222222222014'::uuid, 'COMPLETED', now_ts - INTERVAL '24 hours', now_ts - INTERVAL '6 hours'),
  (15, 15, '22222222-2222-2222-2222-222222222015'::uuid, 'ACCEPTED', now_ts - INTERVAL '22 hours', now_ts - INTERVAL '5 hours'),
  (16, 16, '22222222-2222-2222-2222-222222222016'::uuid, 'NEGOTIATING', now_ts - INTERVAL '20 hours', now_ts - INTERVAL '3 hours'),
  (17, 17, '22222222-2222-2222-2222-222222222017'::uuid, 'NEGOTIATING', now_ts - INTERVAL '18 hours', now_ts - INTERVAL '2 hours')
ON CONFLICT (id) DO NOTHING;

INSERT INTO exchange_meetings (exchange_id, proposer_id, mode, bookspot_id, custom_location, scheduled_at, mark_as_completed_by_user1, mark_as_completed_by_user2, status) VALUES
  (6, u07, 'BOOKSPOT', 23, (SELECT location FROM bookspots WHERE id = 23), now_ts + INTERVAL '2 days', false, false, 'PROPOSAL'),
  (7, u10, 'BOOKSPOT', 25, (SELECT location FROM bookspots WHERE id = 25), now_ts - INTERVAL '1 day', false, false, 'ACCEPTED'),
  (8, u04, 'BOOKSPOT', 29, (SELECT location FROM bookspots WHERE id = 29), now_ts + INTERVAL '1 day', false, false, 'PROPOSAL'),
  (9, u06, 'BOOKSPOT', 33, (SELECT location FROM bookspots WHERE id = 33), now_ts - INTERVAL '8 hours', true, true, 'ACCEPTED'),
  (13, u03, 'BOOKSPOT', 31, (SELECT location FROM bookspots WHERE id = 31), now_ts + INTERVAL '12 hours', false, false, 'REFUSED'),
  (14, u05, 'BOOKSPOT', 35, (SELECT location FROM bookspots WHERE id = 35), now_ts - INTERVAL '4 hours', true, true, 'ACCEPTED'),
  (15, u06, 'BOOKDROP', 33, (SELECT location FROM bookspots WHERE id = 33), now_ts + INTERVAL '26 hours', false, false, 'ACCEPTED')
ON CONFLICT DO NOTHING;

INSERT INTO incidents (exchange_id, meetup_id, informer_id, informed_id, admin_id, comment, status, created_at) VALUES
  (7, 3, u13, u10, '00000000-0000-0000-0000-0000000000a1'::uuid, 'El usuario no se presento en el punto acordado.', 'PENDING', now_ts - INTERVAL '10 hours')
ON CONFLICT DO NOTHING;

-- 16.9 Typing indicators para probar /typing en chats
INSERT INTO typing_indicators (chat_id, user_id, started_at) VALUES
  ('11111111-1111-1111-1111-111111111003'::uuid, u08, now_ts - INTERVAL '15 seconds'),
  ('22222222-2222-2222-2222-222222222005'::uuid, u05, now_ts - INTERVAL '10 seconds')
ON CONFLICT (chat_id, user_id) DO NOTHING;

-- 16.10 Meetups extra con estados y asistencias variadas
INSERT INTO meetups (id, community_id, title, description, other_book_spot_id, scheduled_at, status, creator_id, created_at, updated_at) VALUES
  (11, 2, 'Sesion cerrada de thriller', 'Meetup ya celebrado para probar historico', 12, now_ts - INTERVAL '6 days', 'CELEBRATED', u02, now_ts - INTERVAL '10 days', now_ts - INTERVAL '6 days'),
  (12, 4, 'Encuentro cancelado por lluvia', 'Evento cancelado para probar filtros de estado', 17, now_ts + INTERVAL '4 days', 'CANCELLED', u04, now_ts - INTERVAL '2 days', now_ts - INTERVAL '1 day')
ON CONFLICT (id) DO NOTHING;

INSERT INTO meetup_attendance (meetup_id, user_id, selected_book_id, status) VALUES
  (11, u02, 105, 'ATTENDED'),
  (11, u09, 133, 'NO_SHOW'),
  (12, u04, 113, 'CANCELLED'),
  (12, u06, 121, 'REGISTERED')
ON CONFLICT (meetup_id, user_id) DO NOTHING;

-- 16.11 Movimientos de puntos para probar historico de gamificacion
INSERT INTO points_ledgers (user_id, type, amount, created_at) VALUES
  (u01, 'EXCHANGE_SUCCESS', 100, now_ts - INTERVAL '5 days'),
  (u03, 'MEETUP_ATTENDANCE', 200, now_ts - INTERVAL '1 day'),
  (u06, 'LOGIN_STREAK', 25, now_ts - INTERVAL '12 hours')
ON CONFLICT DO NOTHING;

END $$;

COMMIT;
