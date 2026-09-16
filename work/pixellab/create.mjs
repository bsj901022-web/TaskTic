// Submits create_character for slugs in new-plan.json that are not yet in jobs.json.
import fs from 'node:fs/promises';
import {Client} from '@modelcontextprotocol/sdk/client/index.js';
import {StreamableHTTPClientTransport} from '@modelcontextprotocol/sdk/client/streamableHttp.js';
const token=(await fs.readFile(process.env.PIXELLAB_TOKEN_FILE,'utf8')).trim();
const read=async f=>JSON.parse((await fs.readFile(f,'utf8')).replace(/^\uFEFF/,''));
const plan=await read('new-plan.json'), jobs=await read('jobs.json');
const client=new Client({name:'taskbar-tails-create',version:'0.5.0'});
const txt=r=>(r.content??[]).filter(c=>c.type==='text').map(c=>c.text).join('\n');
try{
 await client.connect(new StreamableHTTPClientTransport(new URL('https://api.pixellab.ai/mcp'),{requestInit:{headers:{Authorization:'Bearer '+token}}}));
 for(const p of plan){
  if(jobs.some(j=>j.slug===p.slug)){console.log(p.slug+': already created');continue;}
  let r;for(let attempt=0;attempt<40;attempt++){r=await client.callTool({name:'create_character',arguments:{description:p.description,name:p.name,body_type:'humanoid',mode:'v3',n_directions:8,size:64,view:'side',outline:'single color outline',detail:'low detail'}},undefined,{timeout:90000});if(r.isError&&/job slots|429|concurrent/i.test(txt(r))){console.log(p.slug+': slots full, retrying in 40s');await new Promise(s=>setTimeout(s,40000));continue;}break;}
  await fs.writeFile('create-'+p.slug+'.json',JSON.stringify(r));
  const body=txt(r);
  if(r.isError){console.log(p.slug+': ERROR '+body.slice(0,200));continue;}
  const id=body.match(/^id: (\S+)/m)?.[1];
  if(!id){console.log(p.slug+': no id in response');continue;}
  jobs.push({slug:p.slug,id});await fs.writeFile('jobs.json',JSON.stringify(jobs,null,2));
  console.log(p.slug+': queued '+id);
 }
}catch(e){console.error('FAIL: '+String(e.message).replaceAll(token,'[REDACTED]'));process.exitCode=1;}finally{await client.close();}
