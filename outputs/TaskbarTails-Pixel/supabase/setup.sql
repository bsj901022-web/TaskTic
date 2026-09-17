-- Taskbar Tails v0.6.8 / Supabase setup (fresh install). Projects that already ran an older setup.sql run upgrade-v05.sql
-- and upgrade-v08.sql instead. Run the entire file once in the project's SQL Editor as postgres, then turn on
-- Authentication -> Sign In / Providers -> Anonymous Sign-Ins.
-- Only tt_* tables/functions and two realtime.messages policies are created. No existing application tables are touched.
--
-- Traffic model: rooms and memberships live in two small tables. Character events are Realtime broadcasts over the
-- websocket and the roster is Realtime presence, so nothing is written to the database while people play.
begin;

create table if not exists public.tt_rooms (
 id uuid primary key default gen_random_uuid(),
 name text not null check (char_length(name) between 1 and 40),
 owner_id uuid not null references auth.users(id) on delete cascade,
 invite_code text not null unique default upper(substr(replace(gen_random_uuid()::text,'-',''),1,10)),
 created_at timestamptz not null default now()
);
create table if not exists public.tt_members (
 room_id uuid not null references public.tt_rooms(id) on delete cascade,
 user_id uuid not null references auth.users(id) on delete cascade,
 pet_name text not null default '친구' check (char_length(pet_name) between 1 and 12),
 species text not null default 'cat' check (species ~ '^[a-z]{2,20}$'),
 last_seen timestamptz not null default now(),
 joined_at timestamptz not null default now(),
 primary key(room_id,user_id)
);
create index if not exists tt_members_user_idx on public.tt_members(user_id,room_id);
alter table public.tt_rooms enable row level security;
alter table public.tt_members enable row level security;

create or replace function public.tt_is_member(p_room uuid)
returns boolean language sql stable security definer set search_path = '' as $$
 select exists(select 1 from public.tt_members where room_id=p_room and user_id=auth.uid());
$$;
revoke all on function public.tt_is_member(uuid) from public,anon;
grant execute on function public.tt_is_member(uuid) to authenticated;

-- Direct writes are not exposed. Validated RPC functions perform writes.
revoke all on public.tt_rooms,public.tt_members from anon,authenticated;
grant select on public.tt_rooms,public.tt_members to authenticated;
do $$ begin
 if not exists(select 1 from pg_policies where schemaname='public' and tablename='tt_rooms' and policyname='tt_member_rooms') then
  create policy tt_member_rooms on public.tt_rooms for select to authenticated using(public.tt_is_member(id));
 end if;
 if not exists(select 1 from pg_policies where schemaname='public' and tablename='tt_members' and policyname='tt_member_roster') then
  create policy tt_member_roster on public.tt_members for select to authenticated using(public.tt_is_member(room_id));
 end if;
end $$;

create or replace function public.tt_create_room(p_name text,p_pet_name text,p_species text)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare r public.tt_rooms; u uuid:=auth.uid();
begin
 if u is null then raise exception 'Authentication required'; end if;
 if (select count(*) from public.tt_rooms where owner_id=u)>=5 then raise exception 'You can own at most 5 rooms'; end if;
 if char_length(trim(p_name)) not between 1 and 40 or char_length(trim(p_pet_name)) not between 1 and 12 then raise exception 'Invalid name'; end if;
 insert into public.tt_rooms(name,owner_id) values(trim(p_name),u) returning * into r;
 insert into public.tt_members(room_id,user_id,pet_name,species) values(r.id,u,trim(p_pet_name),p_species);
 return jsonb_build_object('room_id',r.id,'name',r.name,'invite_code',r.invite_code);
end; $$;

create or replace function public.tt_join_room(p_code text,p_pet_name text,p_species text)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare r public.tt_rooms; u uuid:=auth.uid();
begin
 if u is null then raise exception 'Authentication required'; end if;
 if char_length(trim(p_pet_name)) not between 1 and 12 then raise exception 'Invalid pet name'; end if;
 select * into r from public.tt_rooms where invite_code=upper(trim(p_code)) for update;
 if not found then raise exception 'Invitation code not found'; end if;
 if (select count(*) from public.tt_members where room_id=r.id)>=12 and not public.tt_is_member(r.id) then raise exception 'Room is full (12 members)'; end if;
 insert into public.tt_members(room_id,user_id,pet_name,species) values(r.id,u,trim(p_pet_name),p_species)
 on conflict(room_id,user_id) do update set pet_name=excluded.pet_name,species=excluded.species,last_seen=now();
 return jsonb_build_object('room_id',r.id,'name',r.name,'invite_code',r.invite_code);
end; $$;

-- Called about once a minute per connected member (last_seen keeps older clients' rosters accurate).
create or replace function public.tt_touch_room(p_room uuid,p_pet_name text,p_species text)
returns void language plpgsql security definer set search_path = '' as $$
begin
 if not public.tt_is_member(p_room) then raise exception 'Room membership required'; end if;
 update public.tt_members set pet_name=trim(p_pet_name),species=p_species,last_seen=now() where room_id=p_room and user_id=auth.uid();
end; $$;

create or replace function public.tt_leave_room(p_room uuid)
returns void language plpgsql security definer set search_path = '' as $$
begin
 delete from public.tt_members where room_id=p_room and user_id=auth.uid();
end; $$;

-- Kept for clients on v0.6.7 or older, which send events through this RPC (each call writes one realtime.messages row).
-- v0.6.8+ clients broadcast over the websocket instead and never call it.
create or replace function public.tt_send_event(p_room uuid,p_event jsonb)
returns void language plpgsql security definer set search_path = '' as $$
declare clean jsonb;
begin
 if not public.tt_is_member(p_room) then raise exception 'Room membership required'; end if;
 if jsonb_typeof(p_event)<>'object' or octet_length(p_event::text)>4096 then raise exception 'Invalid event'; end if;
 if char_length(coalesce(p_event->>'Message',''))>80 then raise exception 'Message too long'; end if;
 clean := p_event || jsonb_build_object('UserId',auth.uid()::text);
 perform realtime.send(clean,'pet','tt:'||p_room::text,true);
end; $$;

-- Realtime authorization: members may listen to, broadcast on and track presence in their own room's private channel.
create or replace function public.tt_can_listen(p_topic text)
returns boolean language sql stable security definer set search_path = '' as $$
 select exists(select 1 from public.tt_members m where 'tt:'||m.room_id::text=p_topic and m.user_id=auth.uid());
$$;
revoke all on function public.tt_can_listen(text) from public,anon;
grant execute on function public.tt_can_listen(text) to authenticated;

grant select, insert on realtime.messages to authenticated;
do $$ begin
 if not exists(select 1 from pg_policies where schemaname='realtime' and tablename='messages' and policyname='tt_room_subscriptions') then
  create policy tt_room_subscriptions on realtime.messages for select to authenticated using(public.tt_can_listen(realtime.topic()));
 end if;
 if not exists(select 1 from pg_policies where schemaname='realtime' and tablename='messages' and policyname='tt_room_broadcast') then
  create policy tt_room_broadcast on realtime.messages for insert to authenticated
   with check (realtime.messages.extension in ('broadcast','presence') and public.tt_can_listen(realtime.topic()));
 end if;
end $$;

revoke all on function public.tt_create_room(text,text,text),public.tt_join_room(text,text,text),public.tt_touch_room(uuid,text,text),public.tt_leave_room(uuid),public.tt_send_event(uuid,jsonb) from public,anon;
grant execute on function public.tt_create_room(text,text,text),public.tt_join_room(text,text,text),public.tt_touch_room(uuid,text,text),public.tt_leave_room(uuid),public.tt_send_event(uuid,jsonb) to authenticated;

-- realtime.messages holds nothing the app reads back; purge hourly so rows from older clients never pile up.
create extension if not exists pg_cron;
do $$ begin
 if exists(select 1 from pg_extension where extname='pg_cron') then
  perform cron.unschedule(jobid) from cron.job where jobname='tt_purge_realtime_messages';
  perform cron.schedule('tt_purge_realtime_messages','17 * * * *',
   $job$ delete from realtime.messages where inserted_at < now() - interval '1 hour' $job$);
 end if;
end $$;
commit;

select 'Taskbar Tails v0.6.8: tables, room functions, realtime policies and hourly purge are ready' as result;
