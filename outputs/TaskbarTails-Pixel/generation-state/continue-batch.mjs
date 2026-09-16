import fs from 'node:fs/promises';
import path from 'node:path';
import {Client} from '@modelcontextprotocol/sdk/client/index.js';
import {StreamableHTTPClientTransport} from '@modelcontextprotocol/sdk/client/streamableHttp.js';
let token=''; for await (const data of process.stdin) token+=data; token=token.trim();
const root=path.dirname(new URL(import.meta.url).pathname.replace(/^\/(\w:)/,'$1'));
const read=async file=>JSON.parse((await fs.readFile(path.join(root,file),'utf8')).replace(/^\uFEFF/,''));
const save=async(file,data)=>fs.writeFile(path.join(root,file),JSON.stringify(data,null,2));
const jobs=await read('jobs.json');
const client=new Client({name:'taskbar-tails-asset-batch',version:'0.3.0'});
const text=r=>(r.content??[]).filter(c=>c.type==='text').map(c=>c.text).join('\n');
const complete=new Set();
try {
 await client.connect(new StreamableHTTPClientTransport(new URL('https://api.pixellab.ai/mcp'),{requestInit:{headers:{Authorization:'Bearer '+token}}}));
 for(let cycle=0;cycle<30;cycle++) {
  for(const job of jobs) {
   if(complete.has(job.slug)) continue;
   let result;
   try { result=await client.callTool({name:'get_character',arguments:{character_id:job.id,include_preview:false}},undefined,{timeout:60000}); }
   catch(e) { console.log(job.slug+': status request failed; will check again'); continue; }
   await save('status-'+job.slug+'.json',result);
   const body=text(result);
   if(/taskbar-walk — 2 dir/.test(body)) { complete.add(job.slug); console.log(job.slug+': READY (8 views, east/west 8 frames)'); continue; }
   console.log(job.slug+': '+(body.match(/^status: .+$/m)?.[0]??'status unavailable'));
   if(!body.includes('status: completed')) continue;
   let prior=null; try { prior=await read('animate-'+job.slug+'.json'); } catch {}
   if(prior && !prior.isError) continue;
   await save('animate-'+job.slug+'.json',{attemptStarted:true,at:new Date().toISOString()});
   const animation=await client.callTool({name:'animate_character',arguments:{character_id:job.id,mode:'v3',action_description:'A gentle cheerful walking cycle in place, short alternating steps on two hind paws, tiny front paws swinging, subtle soft head bob, steady pace, stays facing the same direction throughout, consistent silhouette, seamless loop',animation_name:'taskbar-walk',directions:['east','west'],frame_count:8,keep_first_frame:false}},undefined,{timeout:60000});
   await save('animate-'+job.slug+'.json',animation);
   console.log(job.slug+': '+(animation.isError?text(animation):'walking animation queued'));
  }
  if(complete.size===jobs.length) { console.log('BATCH COMPLETE'); break; }
  await new Promise(r=>setTimeout(r,45000));
 }
 await save('batch-result.json',{completed:[...complete],expected:jobs.length});
} catch(e) { console.error(String(e.message).replaceAll(token,'[REDACTED]')); process.exitCode=1; }
finally { await client.close(); }
