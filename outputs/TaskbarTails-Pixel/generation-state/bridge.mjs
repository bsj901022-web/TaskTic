import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StreamableHTTPClientTransport } from '@modelcontextprotocol/sdk/client/streamableHttp.js';
let input = '';
for await (const chunk of process.stdin) input += chunk;
const request = JSON.parse(input);
const client = new Client({name:'taskbar-tails-prototype',version:'0.1.0'});
try {
  const transport = new StreamableHTTPClientTransport(new URL('https://api.pixellab.ai/mcp'), {
    requestInit: {headers:{Authorization: 'Bearer ' + request.token}}
  });
  await client.connect(transport);
  const result = request.method === 'list' ? await client.listTools() : await client.callTool({name:request.name,arguments:request.arguments ?? {}}, undefined, {timeout:60000});
  process.stdout.write(JSON.stringify(result));
} catch (error) {
  process.stdout.write(JSON.stringify({error:String(error.message).replaceAll(request.token,'[REDACTED]')}));
  process.exitCode = 1;
} finally { await client.close(); }
