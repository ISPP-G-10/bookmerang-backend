-- Allow subscriptions for any base user type (USER or BOOKDROP_USER)
ALTER TABLE public.subscriptions
    DROP CONSTRAINT IF EXISTS subscriptions_user_id_fkey;

ALTER TABLE public.subscriptions
    ADD CONSTRAINT subscriptions_user_id_fkey
    FOREIGN KEY (user_id) REFERENCES public.base_users(id) ON DELETE CASCADE;
