-- Create subscription_status enum type
CREATE TYPE public.subscription_status AS ENUM (
    'ACTIVE',
    'EXPIRED',
    'CANCELLED',
    'GRACE_PERIOD',
    'REVOKED'
);

-- Create subscription_platform enum type
CREATE TYPE public.subscription_platform AS ENUM (
    'STRIPE',
    'SYSTEM'
);

-- Create subscriptions table
CREATE TABLE public.subscriptions (
    id SERIAL PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    platform subscription_platform NOT NULL,
    platform_subscription_id TEXT,
    original_transaction_id TEXT,
    status subscription_status NOT NULL DEFAULT 'ACTIVE',
    current_period_start TIMESTAMP WITH TIME ZONE NOT NULL,
    current_period_end TIMESTAMP WITH TIME ZONE NOT NULL,
    cancels_at_period_end BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create subscription_receipts table
CREATE TABLE public.subscription_receipts (
    id SERIAL PRIMARY KEY,
    subscription_id INTEGER NOT NULL REFERENCES public.subscriptions(id) ON DELETE CASCADE,
    platform subscription_platform NOT NULL,
    receipt_data TEXT,
    verified_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create indices for performance
CREATE INDEX idx_subscriptions_user_id_status ON public.subscriptions(user_id, status);
CREATE INDEX idx_subscriptions_current_period_end ON public.subscriptions(current_period_end);
CREATE INDEX idx_subscription_receipts_subscription_id ON public.subscription_receipts(subscription_id);
