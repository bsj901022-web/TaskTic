// Usage: node pl.mjs <tool> '<json-args>' [outfile]   (token read from PIXELLAB_TOKEN_FILE)
import fs from 'node:fs/promises';
import {Client} from '@modelcontextprotocol/sdk/client/index.js';
import {StreamableHTTPClientTransport} from '@modelcontextprotocol/sdk/client/streamableHttp.js';
const token=(await fs.readFile(process.env.PIXELLAB_TOKEN_FILE,'utf8')).trim();
const [tool,argsJson,outfile]=process.argv.slice(2);
const client=new Client({name:'taskbar-tails-cli',version:'0.5.0'});
try{
 await client.connect(new StreamableHTTPClientTransport(new URL('https://api.pixellab.ai/mcp'),{requestInit:{headers:{Authorization:'Bearer '+token}}}));
 const result=await client.callTool({name:tool,arguments:argsJson?JSON.parse(argsJson):{}},undefined,{timeout:90000});
 if(outfile)await fs.writeFile(outfile,JSON.stringify(result,null,1));
 const text=(result.content??[]).filter(c=>c.type==='text').map(c=>c.text).join('\n');
 console.log((result.isError?'[ERROR] ':'')+text.replace(/https:\/\/\S+/g,'<URL>'));
 if(result.isError)process.exitCode=2;
}catch(e){console.error('FAIL: '+String(e.message).replaceAll(token,'[REDACTED]'));process.exitCode=1;}finally{await client.close();}
