-- ============================================================
-- PDA Analytics — Дополнительные индексы для PostgreSQL
-- Выполнить ПОСЛЕ EF Core миграции InitialCreate
-- ============================================================

-- Full-Text Search GIN index по тексту сообщений (русский + английский)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Trigram-индекс для LIKE '%текст%' поиска по сообщениям
CREATE INDEX IF NOT EXISTS ix_messages_message_trgm 
    ON messages_denormalized 
    USING gin (message gin_trgm_ops);

-- Trigram-индекс для поиска по логину PDA
CREATE INDEX IF NOT EXISTS ix_pda_accounts_login_trgm 
    ON pda_accounts 
    USING gin (login gin_trgm_ops);

-- Trigram-индекс для поиска по никнейму игрока
CREATE INDEX IF NOT EXISTS ix_players_nickname_trgm 
    ON players 
    USING gin (nickname gin_trgm_ops);

-- Trigram-индекс для поиска по SteamID (partial match)
CREATE INDEX IF NOT EXISTS ix_players_steam_id_trgm 
    ON players 
    USING gin (steam_id gin_trgm_ops);

-- Составной индекс для быстрого Omni-Search по sender/receiver login
CREATE INDEX IF NOT EXISTS ix_messages_sender_login 
    ON messages_denormalized (sender_login);

CREATE INDEX IF NOT EXISTS ix_messages_receiver_login 
    ON messages_denormalized (receiver_login);
