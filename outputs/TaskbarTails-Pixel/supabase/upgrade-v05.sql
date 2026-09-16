-- Taskbar Tails v0.5 upgrade / Supabase
-- Run once in the SQL Editor of a project that already ran setup.sql (v0.4).
-- Opens the species list so newly added characters (duck, bear, frog, panda, ghost, mushroom, dragon, cactus, ...)
-- are accepted by tt_create_room / tt_join_room / tt_touch_room. Nothing else changes.
begin;
alter table public.tt_members drop constraint if exists tt_members_species_check;
alter table public.tt_members add constraint tt_members_species_check check (species ~ '^[a-z]{2,20}$');
commit;
select 'Taskbar Tails v0.5: species list is now open-ended' as result;
