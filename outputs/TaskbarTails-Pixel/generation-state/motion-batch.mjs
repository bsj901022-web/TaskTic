import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {Client} from '@modelcontextprotocol/sdk/client/index.js';
import {StreamableHTTPClientTransport} from '@modelcontextprotocol/sdk/client/streamableHttp.js';
let token='';for await(const data of process.stdin)token+=data;token=token.trim();
const root=path.dirname(fileURLToPath(import.meta.url));
const read=async file=>JSON.parse((await fs.readFile(path.join(root,file),'utf8')).replace(/^\uFEFF/,''));
const save=async(file,data)=>fs.writeFile(path.join(root,file),JSON.stringify(data,null,2));
const jobs=await read('jobs.json'), actions=await read('actions.json');
await fs.mkdir(path.join(root,'motion-requests'),{recursive:true});
const client=new Client({name:'taskbar-tails-motions',version:'0.4.0'});
const txt=r=>(r.content??[]).filter(c=>c.type==='text').map(c=>c.text).join('\n');
const done=new Set();
try{
 await client.connect(new StreamableHTTPClientTransport(new URL('https://api.pixellab.ai/mcp'),{requestInit:{headers:{Authorization:'Bearer '+token}}}));
 for(let cycle=0;cycle<160;cycle++){
  let saturated=false;
  for(const job of jobs){
   if(done.has(job.slug))continue;
   const result=await client.callTool({name:'get_character',arguments:{character_id:job.id,include_preview:false}},undefined,{timeout:60000});
   await save('status-'+job.slug+'.json',result);const body=txt(result);
   if(!body.includes('status: completed')){console.log(job.slug+': '+(body.match(/^status: .+$/m)?.[0]??body.slice(0,150)));continue;}
   const recipes=[{key:'smooth-walk',directions:['east','west'],prompt:'A smooth relaxed walking loop in place with short alternating steps and gentle natural weight transfer, subtle head bob, consistent speed and side-facing direction, tiny arm or paw swing, seamless first to last transition'},...actions[job.slug].map(a=>({...a,directions:['east']}))];
   let completed=0;
   for(const recipe of recipes){
    const ready=body.includes('  '+recipe.key+' — '+recipe.directions.length+' dir');
    const record='motion-requests/'+job.slug+'-'+recipe.key+'.json';
    if(ready){completed++;continue;}
    if(saturated) continue;
    let prior=null;try{prior=await read(record)}catch{}
    if(prior&&!prior.isError)continue;
    await save(record,{attemptStarted:true,at:new Date().toISOString()});
    const answer=await client.callTool({name:'animate_character',arguments:{character_id:job.id,mode:'v3',action_description:recipe.prompt,animation_name:recipe.key,directions:recipe.directions,frame_count:16,keep_first_frame:false}},undefined,{timeout:60000});
    await save(record,answer);
    if(answer.isError){console.log(job.slug+'/'+recipe.key+': '+txt(answer)); if(/slots|concurrent|429/i.test(txt(answer))){saturated=true;break;} if(/quota|credit|balance|limit/i.test(txt(answer)))throw new Error('Generation service limit reached; submitted jobs are preserved.');}
    else console.log(job.slug+'/'+recipe.key+': 16-frame generation queued');
   }
   console.log(job.slug+': '+completed+'/'+recipes.length+' motion groups ready');
   if(completed===recipes.length)done.add(job.slug);
  }
  await save('motion-batch-result.json',{completed:[...done],expected:jobs.length});
  if(done.size===jobs.length){console.log('ALL MOTIONS COMPLETE');break;}
  await new Promise(r=>setTimeout(r,45000));
 }
}catch(e){console.error(String(e.message).replaceAll(token,'[REDACTED]'));process.exitCode=1;}finally{await client.close();}

