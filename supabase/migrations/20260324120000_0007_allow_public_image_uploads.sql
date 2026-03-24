-- La app sube imágenes directamente desde el frontend usando el JWT propio del backend,
-- no una sesión gestionada por Supabase Auth. En Storage esas peticiones llegan como
-- rol anon, así que la policy anterior (solo authenticated) bloqueaba la inserción.
DROP POLICY IF EXISTS "images_insert_authenticated" ON storage.objects;
DROP POLICY IF EXISTS "images_insert_public" ON storage.objects;

CREATE POLICY "images_insert_public"
ON storage.objects FOR INSERT
TO public
WITH CHECK (bucket_id = 'images');
