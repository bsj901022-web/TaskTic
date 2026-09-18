-- Taskbar Tails v0.6.8 follow-up / Supabase: empty rooms are removed and the 5-room limit counts only rooms you are still in.
-- Run once in the SQL Editor of a project that already ran setup.sql (any version). Safe to run again.
--
-- Before: tt_leave_room deleted only the membership row, so a room stayed in tt_rooms forever with its owner_id, and
-- tt_create_room counted those ghosts toward "You can own at most 5 rooms".
-- After: the last member leaving deletes the room; the limit counts rooms the owner is currently a member of; rooms nobody
-- has been seen in for 60 days are removed daily by pg_cron.
begin;

create or replace function public.tt_leave_room(p_room uuid)
returns void language plpgsql security definer set search_path = '' as $$
begin
 delete from public.tt_members where room_id=p_room and user_id=auth.uid();
 -- Nobody left: the room and its invite code disappear.
 delete from public.tt_rooms r where r.id=p_room and not exists(select 1 from public.tt_members m where m.room_id=r.id);
end; $$;

create or replace function public.tt_create_room(p_name text,p_pet_name text,p_species text)
returns jsonb language plpgsql security definer set search_path = '' as $$
declare r public.tt_rooms; u uuid:=auth.uid();
begin
 if u is null then raise exception 'Authentication required'; end if;
 -- Only rooms this user still belongs to count toward the limit.
 if (select count(*) from public.tt_rooms x where x.owner_id=u and exists(select 1 from public.tt_members m where m.room_id=x.id and m.user_id=u))>=5 then
  raise exception 'You can own at most 5 rooms'; end if;
 if char_length(trim(p_name)) not between 1 and 40 or char_length(trim(p_pet_name)) not between 1 and 12 then raise exception 'Invalid name'; end if;
 insert into public.tt_rooms(name,owner_id) values(trim(p_name),u) returning * into r;
 insert into public.tt_members(room_id,user_id,pet_name,species) values(r.id,u,trim(p_pet_name),p_species);
 return jsonb_build_object('room_id',r.id,'name',r.name,'invite_code',r.invite_code);
end; $$;

-- Rooms with no members at all (left over from before this change).
delete from public.tt_rooms r where not exists(select 1 from public.tt_members m where m.room_id=r.id);

-- Daily: rooms where nobody has been seen for 60 days.
do $$ begin
 if exists(select 1 from pg_extension where extname='pg_cron') then
  perform cron.unschedule(jobid) from cron.job where jobname='tt_purge_idle_rooms';
  perform cron.schedule('tt_purge_idle_rooms','40 4 * * *',
   $job$ delete from public.tt_rooms r where not exists(select 1 from public.tt_members m where m.room_id=r.id and m.last_seen > now() - interval '60 days') $job$);
 end if;
end $$;

commit;

select (select count(*) from public.tt_rooms) as rooms_now, 'empty rooms removed; leaving as the last member now deletes the room' as result;
