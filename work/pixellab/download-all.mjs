// Downloads idle rotations, the 16-frame smooth-walk (east/west) and every special action (east)
// for each character in jobs.json, using the most recent status-<slug>.json snapshot.
// Animation groups are separated by name so special actions never overwrite walk frames.
// Output: outputs/PixelPets/<slug>/ and work/TaskbarTails/assets/<slug>/
//   idle-<dir>.png, walk-<dir>-NN.png, action-<key>-east-NN.png, manifest.json
import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const projectRoot=path.resolve(here,'..','..');
const read=async f=>JSON.parse((await fs.readFile(path.join(here,f),'utf8')).replace(/^﻿/,''));
const jobs=await read('jobs.json'), actions=await read('actions.json');
const only=process.argv.slice(2);
const summary=[];
for(const job of jobs){
 if(only.length&&!only.includes(job.slug))continue;
 let status;try{status=await read('status-'+job.slug+'.json');}catch{console.log(job.slug+': no status file');continue;}
 const text=(status.content??[]).map(c=>c.text??'').join('\n');
 if(!text.includes('status: completed')){console.log(job.slug+': character not completed');continue;}
 const files=[];const missing=[];
 // idle rotations: "  south: https://..."
 const rotBlock=text.split('\nrotations:')[1]?.split('\n\n')[0]??'';
 for(const m of rotBlock.matchAll(/^  ([a-z-]+): (https:\/\/\S+)$/gm))files.push({name:'idle-'+m[1]+'.png',url:m[2]});
 // animation groups: header line "  <name> — N dir (...), 16f ..." followed by "    <dir>: url url ..."
 const animBlock=text.split(/\nanimations[^\n]*\n/)[1]?.split(/\n(?:pending jobs|download:)/)[0]??'';
 const groups={};let current=null;
 for(const line of animBlock.split('\n')){
  const head=line.match(/^  ([A-Za-z0-9_-]+) — (\d+) dir .*?(\d+)f /);
  if(head){current={name:head[1],frames:+head[3],dirs:{}};(groups[head[1]]??=[]).push(current);continue;}
  const dir=line.match(/^    ([a-z-]+): (https:\/\/.+)$/);
  if(dir&&current)current.dirs[dir[1]]=dir[2].trim().split(/[ ,]+/).filter(u=>u.startsWith('https://'));
 }
 const pick=name=>(groups[name]??[]).filter(g=>g.frames===16).sort((a,b)=>Object.keys(b.dirs).length-Object.keys(a.dirs).length)[0];
 const walk=pick('smooth-walk');
 for(const d of ['east','west']){
  const urls=walk?.dirs[d];
  if(!urls||urls.length!==16){missing.push('walk-'+d);continue;}
  urls.forEach((url,i)=>files.push({name:`walk-${d}-${String(i).padStart(2,'0')}.png`,url}));
 }
 for(const a of actions[job.slug]??[]){
  const g=pick(a.key);const urls=g?.dirs.east;
  if(!urls||urls.length!==16){missing.push('action-'+a.key);continue;}
  urls.forEach((url,i)=>files.push({name:`action-${a.key}-east-${String(i).padStart(2,'0')}.png`,url}));
 }
 const folder=path.join(projectRoot,'outputs','PixelPets',job.slug), appFolder=path.join(projectRoot,'work','TaskbarTails','assets',job.slug);
 await fs.mkdir(folder,{recursive:true});await fs.mkdir(appFolder,{recursive:true});
 // remove stale frames of groups we are replacing so 8-frame leftovers never mix with 16-frame sets
 for(const dir of [folder,appFolder])for(const f of await fs.readdir(dir))if(/^(walk|action)-.*\.png$/.test(f)&&files.some(x=>x.name.split('-').slice(0,-1).join('-')===f.split('-').slice(0,-1).join('-')))await fs.rm(path.join(dir,f));
 let downloaded=0;
 for(let start=0;start<files.length;start+=6)await Promise.all(files.slice(start,start+6).map(async f=>{
  const target=path.join(folder,f.name);
  let ok=false;for(let attempt=0;attempt<3&&!ok;attempt++){try{const r=await fetch(f.url);if(!r.ok)throw new Error('HTTP '+r.status);await fs.writeFile(target,Buffer.from(await r.arrayBuffer()));ok=true;downloaded++;}catch(e){if(attempt===2)throw new Error(job.slug+'/'+f.name+': '+e.message);await new Promise(r=>setTimeout(r,800));}}
  await fs.copyFile(target,path.join(appFolder,f.name));
 }));
 const manifest={provider:'PixelLab',characterId:job.id,canvas:{width:64,height:64},walkFps:12,actionFps:9,files:files.map(f=>f.name),missing};
 await fs.writeFile(path.join(folder,'manifest.json'),JSON.stringify(manifest,null,2));
 summary.push(`${job.slug}: ${files.length} files (${downloaded} downloaded)`+(missing.length?' MISSING '+missing.join(','):''));
 console.log(summary.at(-1));
}
