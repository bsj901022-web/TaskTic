import fs from 'node:fs/promises';
import path from 'node:path';
const jobs = JSON.parse((await fs.readFile(new URL('./jobs.json',import.meta.url),'utf8')).replace(/^\uFEFF/,''));
for(const job of jobs) {
 const status = JSON.parse((await fs.readFile(new URL('./status-'+job.slug+'.json',import.meta.url),'utf8')).replace(/^\uFEFF/,''));
 const text = status.content.map(c=>c.text??'').join('\n');
 if(!text.includes('status: completed')) { console.log(job.slug+': not ready'); continue; }
 const folder = path.resolve('outputs/PixelPets',job.slug);
 const appFolder = path.resolve('work/TaskbarTails/assets',job.slug);
 await fs.mkdir(folder,{recursive:true}); await fs.mkdir(appFolder,{recursive:true});
 const files = [];
 for (const line of text.split('\n')) {
  const idle = line.match(/^  ([a-z-]+): (https:\/\/\S+)$/);
  if(idle) files.push({name:'idle-'+idle[1]+'.png',url:idle[2]});
  const walk = line.match(/^    (east|west): (https:\/\/.+)$/);
  if(walk) walk[2].split(', ').forEach((url,i)=>files.push({name:'walk-'+walk[1]+'-'+String(i).padStart(2,'0')+'.png',url}));
 }
 for(let start=0;start<files.length;start+=4) await Promise.all(files.slice(start,start+4).map(async f=>{
  const target = path.join(folder,f.name);
  try { await fs.access(target); } catch { const r=await fetch(f.url); if(!r.ok) throw new Error('HTTP '+r.status); await fs.writeFile(target,Buffer.from(await r.arrayBuffer())); }
  await fs.copyFile(target,path.join(appFolder,f.name));
 }));
 await fs.writeFile(path.join(folder,'manifest.json'),JSON.stringify({provider:'PixelLab',characterId:job.id,canvas:{width:64,height:64},animationFps:9,files:files.map(f=>f.name)},null,2));
 console.log(job.slug+': '+files.length+' frames saved');
}
