// Render the actual template-based outputs, including the figures embedded in Word.
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {spawnSync} from 'node:child_process';
const [out,preview,modulePath,jobIndex]=process.argv.slice(2);
const {SpreadsheetFile,FileBlob}=await import(pathToFileURL(modulePath).href);
await fs.mkdir(preview,{recursive:true});
const jobs=[
 ['Report5.1_Unit_Test.xlsx',[['Cover','A1:F16','unit-cover'],['Test Cases','B1:F20','unit-list'],['Test Statistics','A1:I22','unit-statistics']]],
 ['Report5.2_Integration_Test.xlsx',[['Cover','A1:F16','integration-cover'],['Test Statistics','B1:H22','integration-statistics']]],
 ['Report5.2.2_Integration_Test_API_Test.xlsx',[['Cover','A1:F16','api-cover'],['Test Statistics','B1:H22','api-statistics']]],
 ['Report5.3_System_Test.xlsx',[['Cover','A1:F16','system-cover'],['Test Statistics','B1:H23','system-statistics'],['WorkFlow1','A1:F13','system-case']]],
 ['Report5.4_Acceptance_Test.xlsx',[['Cover','A1:F16','uat-cover'],['Statistics','A1:E22','uat-statistics'],['Customers Feedbacks','A1:F4','uat-feedback']]],
];
if(jobIndex===undefined){
 for(let i=0;i<jobs.length;i++){
   // Artifact Tool cannot render all of these retained legacy Office structures.
   // Use native Excel for those books; it also verifies they open without repair.
   const r=spawnSync('C:/Windows/System32/WindowsPowerShell/v1.0/powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(path.dirname(process.argv[1]),'render_excel.ps1'),'-ReportDirectory',out,'-PreviewDirectory',preview,'-JobIndex',String(i)],{stdio:'inherit'});
   if(r.status!==0)process.exit(r.status??1);
 }
 process.exit(0);
}
for(const [file,areas] of [jobs[Number(jobIndex)]]){
 const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path.join(out,file)));
 for(const [sheetName,range,name] of areas){
   const img=await wb.render({sheetName,range,scale:1,format:'png'});
   await fs.writeFile(path.join(preview,name+'.png'),new Uint8Array(await img.arrayBuffer()));
 }
 if(file.includes('Unit')){
   const sheets=JSON.parse(await fs.readFile(path.join(path.dirname(preview),'authoring.json'),'utf8'))[0].sheets;
   const s=sheets[3];
   const img=await wb.render({sheetName:s.name,range:'A1:P30',scale:1,format:'png'});
   await fs.writeFile(path.join(preview,'unit-case.png'),new Uint8Array(await img.arrayBuffer()));
 }
 if(file==='Report5.2_Integration_Test.xlsx'){
   const specs=JSON.parse(await fs.readFile(path.join(path.dirname(preview),'authoring.json'),'utf8'));
   const s=specs[1].sheets[3];
   const img=await wb.render({sheetName:s.name,range:'A1:F14',scale:1,format:'png'});
   await fs.writeFile(path.join(preview,'integration-case.png'),new Uint8Array(await img.arrayBuffer()));
 }
 console.log('Rendered '+file);
}
