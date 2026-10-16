"""Repair ordinary strings still matching English after the review pass."""
import translate_local as t
import json,re
P=t.ROOT;records=[]
for lang in t.LANGS:
 path=P/'locales'/f'{lang}.json';data=json.loads(path.read_text());manual=json.loads((P/'manual-overrides.json').read_text()).get(lang,{})
 rows=[]
 for k,source in t.SOURCE.items():
  if not isinstance(source,str)or data.get(k)!=source or k in manual or len(source)<3 or k.startswith('ui.map.'):continue
  stripped=re.sub(r'\{[^{}]+\}|<[^>]+>|\d+|[^\w\s]',' ',source)
  words=re.findall(r'[A-Za-z]+',stripped)
  if len(words)<2 and not(k.startswith('ui.') and re.fullmatch(r'[A-Za-z ]{3,40}',source)):continue
  if all(w in ['GP','XP','TC','Fika','Weekend','Drops','TarCoin','PMC','SCAV','Scav','BepInEx','Unity','REF','SMG','DMR','LMG','PMC','PMCs','Scavs','CQB','CQC','WEEKENDDROPS','WEEKEND','DROPS','WEEKEND DROPS','OK']for w in words):continue
  rows.append({'key':k,'text':source})
 for start in range(0,len(rows),8):
  chunk=rows[start:start+8];values=t.request(lang,chunk,fix=True)
  for row,value in zip(chunk,values):
   source=row['text'];key=row['key'];value=re.sub(r'^(?:challenge|ui|shop|market|collection|contract|modifier)\.[a-zA-Z0-9_.]+:\s*','',value)
   if value!=source and not t.errors(source,value):data[key]=value;records.append({'language':lang,'key':key,'source':source,'target':value})
 path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
 print(lang,'remaining-English candidates',len(rows),'processed',flush=True)
(P/'remaining-english-repairs.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n')
