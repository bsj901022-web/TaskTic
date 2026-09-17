-- Taskbar Tails v0.6.8 upgrade / Supabase
-- Run once in the SQL Editor of a project that already ran setup.sql (and upgrade-v05.sql).
--
-- What changes and why:
-- * Until v0.6.7 every character event went through the tt_send_event RPC, which calls realtime.send(): that INSERTS a row
--   into realtime.messages for every event. With a snapshot every 2-3 s per client this filled the database.
-- * From v0.6.8 the app sends events as Realtime broadcasts over the websocket and reads the roster from Realtime presence.
--   Nothing is written to the database for that. Private channels check an INSERT policy on realtime.messages before
--   accepting a broadcast or presence update, so this file adds that policy (room members only).
-- * realtime.messages holds nothing the app ever reads back (bubbles and motions are shown once and forgotten), so the
--   existing rows are purged and a pg_cron job every 10 minutes keeps the table small while older clients are still around.
-- tt_send_event stays so clients on v0.6.7 or older keep working.
begin;

-- Members may broadcast and track presence on their own room's channel.
grant insert on realtime.messages to authenticated;
do $$ begin
 if not exists(select 1 from pg_policies where schemaname='realtime' and tablename='messages' and policyname='tt_room_broadcast') then
  create policy tt_room_broadcast on realtime.messages for insert to authenticated
   with check (realtime.messages.extension in ('broadcast','presence') and public.tt_can_listen(realtime.topic()));
 end if;
end $$;

-- Purge of transient realtime rows every 10 minutes (needs the pg_cron extension: Database -> Extensions -> pg_cron, or the line below).
create extension if not exists pg_cron;
do $$ begin
 if exists(select 1 from pg_extension where extname='pg_cron') then
  perform cron.unschedule(jobid) from cron.job where jobname='tt_purge_realtime_messages';
  perform cron.schedule('tt_purge_realtime_messages','*/10 * * * *',
   $job$ delete from realtime.messages where inserted_at < now() - interval '10 minutes' $job$);
 end if;
end $$;

commit;

-- One-time cleanup of what accumulated so far. Safe: the app never reads old messages.
delete from realtime.messages where inserted_at < now() - interval '10 minutes';

select 'Taskbar Tails v0.6.8: websocket broadcasts + presence enabled, realtime.messages purged every 10 minutes' as result;
