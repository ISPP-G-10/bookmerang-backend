-- Bucket para fotos de libros usadas por el flujo "Subir libro".
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES
  (
    'images',
    'images',
    true,
    52428800,
    ARRAY['image/jpeg', 'image/jpg', 'image/png', 'image/webp', 'image/heic', 'image/heif']
  )
ON CONFLICT (id) DO UPDATE SET
  public = EXCLUDED.public,
  file_size_limit = EXCLUDED.file_size_limit,
  allowed_mime_types = EXCLUDED.allowed_mime_types;

DROP POLICY IF EXISTS "images_select_public" ON storage.objects;
CREATE POLICY "images_select_public"
ON storage.objects FOR SELECT
TO public
USING (bucket_id = 'images');

DROP POLICY IF EXISTS "images_insert_authenticated" ON storage.objects;
CREATE POLICY "images_insert_authenticated"
ON storage.objects FOR INSERT
TO authenticated
WITH CHECK (bucket_id = 'images');
