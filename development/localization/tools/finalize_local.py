"""Apply editorial overrides and produce local coverage/review artifacts."""
from pathlib import Path
import json,re,hashlib,zipfile
import translate_local as t
P=t.ROOT;manual=json.loads((P/'manual-overrides.json').read_text());edits=[]
for code in ['en',*t.LANGS]:
 path=P/'locales'/f'{code}.json';data=json.loads(path.read_text())
 if code!='en':
  reviewfile=t.AUDIT/f'review-{code}.json'
  if reviewfile.exists():
   reviewed=json.loads(reviewfile.read_text());regressions={x['source']:x['before'] for x in reviewed.values() if x['after']==x['source'] and x['before']!=x['source']}
   for key,source in t.SOURCE.items():
    if not isinstance(source,str)or data.get(key)!=source:continue
    canon,nums=t.canonical(source)
    if canon not in regressions:continue
    restored=regressions[canon]
    for i,n in enumerate(nums):restored=restored.replace('{N'+str(i)+'}',n)
    for i,c in enumerate(['GP','TC','XP']):restored=restored.replace('{C'+str(i)+'}',c)
    if not t.errors(source,restored):data[key]=restored;edits.append({'language':code,'key':key,'before':source,'after':restored,'kind':'rejected English regression'})
  for k,v in manual.get(code,{}).items():
   if k not in t.SOURCE:raise ValueError((code,k,'unknown editorial key'))
   if t.errors(t.SOURCE[k],v):raise ValueError((code,k,t.errors(t.SOURCE[k],v)))
   if data[k]!=v:edits.append({'language':code,'key':k,'before':data[k],'after':v,'kind':'editorial override'});data[k]=v
  for k,v in list(data.items()):
   if not isinstance(v,str):continue
   clean=re.sub(r'^(?:challenge|ui|shop|market|collection|contract|modifier)\.[a-zA-Z0-9_.]+:\s*','',v)
   if code=='jp' and 'Scav' in t.SOURCE.get(k,''):clean=clean.replace('スカーバーラン','Scavレイド').replace('スカーバー','Scav')
   if code=='hu':clean=re.sub(r'(?<![A-Za-z])(GP|TC|XP)(?=[a-záéíóöőúüű])',r'\1-',clean)
   if clean!=v:edits.append({'language':code,'key':k,'before':v,'after':clean,'kind':'context-prefix or currency suffix repair'});data[k]=clean
 path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
previous=P/'editorial-review.json'
if previous.exists():edits=json.loads(previous.read_text()).get('edits',[])+edits
(P/'editorial-review.json').write_text(json.dumps({'edits':edits,'manual_entries':{c:len(v)for c,v in manual.items()}},ensure_ascii=False,indent=2)+'\n')
print('Editorial changes applied',len(edits))
