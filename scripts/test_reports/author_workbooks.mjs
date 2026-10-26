// Author cell content/formulas with Artifact Tool; the Python adapter retains template OOXML.
import fs from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import {createHash} from 'node:crypto';
const [specFile, outDir, modulePath] = process.argv.slice(2);
const {Workbook,SpreadsheetFile}=await import(pathToFileURL(modulePath).href);
const specs=JSON.parse(await fs.readFile(specFile,'utf8'));
await fs.mkdir(outDir,{recursive:true});
function column(n){let s='';while(n){const k=(n-1)%26;s=String.fromCharCode(65+k)+s;n=Math.floor((n-1)/26);}return s;}
for(const spec of specs){
  const destination=path.join(outDir,spec.filename);
  const cacheFile=destination+'.cache.json';
  const specHash=createHash('sha256').update(JSON.stringify(spec)).digest('hex');
  try {
    const cache=JSON.parse(await fs.readFile(cacheFile,'utf8'));
    const outputHash=createHash('sha256').update(await fs.readFile(destination)).digest('hex');
    if(cache.specHash===specHash&&cache.outputHash===outputHash){console.log('Reused verified authoring payload: '+spec.filename);continue;}
  } catch(error) {if(error.code!=='ENOENT')throw error;}
  const wb=Workbook.create();
  for(const def of spec.sheets)wb.worksheets.add(def.name);
  for(const def of spec.sheets){
    const sh=wb.worksheets.getItem(def.name);
    let maxRow=1,maxCol=1;
    for(const addr of [...Object.keys(def.values),...Object.keys(def.formulas)]){
      const [,letters,row]=addr.match(/^([A-Z]+)(\d+)$/);let n=0;for(const c of letters)n=n*26+c.charCodeAt(0)-64;
      maxRow=Math.max(maxRow,Number(row));maxCol=Math.max(maxCol,n);
    }
    const data=Array.from({length:maxRow},()=>Array(maxCol).fill(null));
    for(const [addr,value] of Object.entries(def.values)){
      const [,letters,row]=addr.match(/^([A-Z]+)(\d+)$/);let n=0;for(const c of letters)n=n*26+c.charCodeAt(0)-64;
      data[Number(row)-1][n-1]=typeof value==='string'&&value.startsWith('=')?"'"+value:value;
    }
    sh.getRange(`A1:${column(maxCol)}${maxRow}`).values=data;
    for(const [addr,formula] of Object.entries(def.formulas))sh.getRange(addr).formulas=[[formula]];
  }
  wb.recalculate();
  const result=await SpreadsheetFile.exportXlsx(wb);await result.save(destination);
  await fs.writeFile(cacheFile,JSON.stringify({specHash,outputHash:createHash('sha256').update(await fs.readFile(destination)).digest('hex')}));
  console.log(`Authored ${spec.filename}: ${spec.sheets.length} template sheets`);
}
