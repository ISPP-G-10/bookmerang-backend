INSERT INTO "genres" ("name") VALUES
  ('Ficción'),
  ('No ficción'),
  ('Fantasía'),
  ('Romance'),
  ('Misterio'),
  ('Ciencia ficción'),
  ('Biografía'),
  ('Historia'),
  ('Autoayuda'),
  ('Infantil'),
  ('Juvenil'),
  ('Terror'),
  ('Poesía'),
  ('Ensayo')
ON CONFLICT ("name") DO NOTHING;