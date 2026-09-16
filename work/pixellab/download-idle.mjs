import fs from 'node:fs/promises';
import path from 'node:path';
for (const slug of ['cat','rabbit','dog']) {
 const status = JSON.parse((await fs.readFile(new URL('./status-'+slug+'.json',import.meta.url),'utf8')).replace(/^\uFEFF/,''));
 const text = status.content.map(c=>c.text??'').join('\n');
 if (!text.includes('status: completed')) continue;
 const folder = path.resolve('work/TaskbarTails/assets',slug);
 await fs.mkdir(folder,{recursive:true});
 for (const direction of ['south','east','west']) {
  const url = text.match(new RegExp('^  '+direction+': (https://\\S+)','m'))?.[1];
  if (!url) continue;
  const response = await fetch(url); if(!response.ok) throw new Error('Download HTTP '+response.status);
  await fs.writeFile(path.join(folder,'idle-'+direction+'.png'),Buffer.from(await response.arrayBuffer()));
 }
 console.log(slug+' sprites saved');
}
