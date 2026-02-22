create index if not exists ix_base_users_location on base_users using gist (location);
create index if not exists ix_user_preferences_location on user_preferences using gist (location);
create index if not exists ix_bookspots_location on bookspots using gist (location);