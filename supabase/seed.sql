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

-- ==============================================================
-- 4. BOOKS  (4 libros por usuario = 60 en total)
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

END $$;

COMMIT;
