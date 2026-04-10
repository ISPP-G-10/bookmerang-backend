-- Create enum type for inkdrops actions
CREATE TYPE public.inkdrops_action_type AS ENUM (
    'EXCHANGE_COMPLETED',
    'MEETUP_ATTENDED'
);

-- Create inkdrops_history table
CREATE TABLE public.inkdrops_history (
    id SERIAL PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    action_type public.inkdrops_action_type NOT NULL,
    points_granted INT NOT NULL,
    related_id INT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create index on user_id for faster queries
CREATE INDEX idx_inkdrops_history_user_id ON public.inkdrops_history(user_id);

-- Create index on created_at for chronological queries
CREATE INDEX idx_inkdrops_history_created_at ON public.inkdrops_history(created_at DESC);
