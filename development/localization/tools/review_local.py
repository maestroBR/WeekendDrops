"""Second local contextual editing pass, with a resumable audit trail."""
import translate_local as t
import json,time,urllib.request,re
from collections import Counter

def review(lang,rows):
 schema={'type':'object','properties':{'translations':{'type':'object','properties':{str(i):{'type':'string'} for i in range(len(rows))},'required':[],'additionalProperties':False}},'required':['translations'],'additionalProperties':False}
 prompt=f'''Edit these existing {t.LANGS[lang][1]} translations for the WeekendDrops Tarkov game mod. Compare each with the English original. Correct mistranslations, unnatural grammar, wrong game terminology and inconsistent meaning. Keep correct translations unchanged. If the translation still matches English, translate ordinary labels and sentences; only preserve proper names, gun models and technical abbreviations. Context: extracts are surviving and leaving a raid; reroll replaces a challenge; claims collect rewards; stash stores player items; bounty crews are hostile NPC squads; a headshot is a shot to the head, NOT a projectile called a head bullet; beating a record means improving it. Glossary: {t.LANGS[lang][2]}. Preserve ALL format placeholders, literal numbers, GP/TC/XP, Unity tags in exact order, special symbols and newline counts. Preserve the original meaning and quantities, don't invent mechanics. Keep short labels concise and capitalized headings where appropriate. The resulting strings MUST be entirely in the requested target language, except proper names, game acronyms and protected tokens. Do not turn translated verbs or item categories into English. Return JSON translations dictionary containing ONLY entries that require corrections. Omit unchanged translations entirely. For each correction, map its numeric input key to the corrected string, not an explanation. An empty dictionary is allowed if all entries are correct.'''
 data={'messages':[{'role':'system','content':prompt},{'role':'user','content':json.dumps({str(i):{'key':k,'english':s,'translation':v}for i,(k,s,v)in enumerate(rows)},ensure_ascii=False)}],'temperature':0,'max_tokens':6000,'response_format':{'type':'json_schema','json_schema':{'name':'review','strict':True,'schema':schema}}}
 req=urllib.request.Request('http://127.0.0.1:18476/v1/chat/completions',data=json.dumps(data).encode(),headers={'Content-Type':'application/json'})
 response=json.load(urllib.request.urlopen(req,timeout=240));return json.loads(response['choices'][0]['message']['content'])['translations']

def run(lang):
 path=t.ROOT/'locales'/f'{lang}.json'
 while not path.exists():time.sleep(10)
 data=json.loads(path.read_text());cachefile=t.AUDIT/f'review-{lang}.json';cache=json.loads(cachefile.read_text())if cachefile.exists()else{}
 groups={};unique={}
 manual=json.loads((t.ROOT/'manual-overrides.json').read_text()).get(lang,{})
 for k,s in t.SOURCE.items():
  if not isinstance(s,str):continue
  if k in manual:data[k]=manual[k];continue
  cs,ns=t.canonical(s);cv,nv=t.canonical(data[k]);identity=json.dumps([cs,cv],ensure_ascii=False)
  if ns!=nv:identity=json.dumps([s,data[k]],ensure_ascii=False);cs=s;cv=data[k];ns=[]
  groups[k]=(identity,ns);unique.setdefault(identity,(k,cs,cv))
 pending=[(i,row)for i,row in unique.items()if i not in cache];chunks=[];chunk=[];size=0
 for pair in pending:
  n=len(pair[1][1])+len(pair[1][2])
  if chunk and(len(chunk)>=32 or size+n>6000):chunks.append(chunk);chunk=[];size=0
  chunk.append(pair);size+=n
 if chunk:chunks.append(chunk)
 rejected=[]
 for pos,chunk in enumerate(chunks):
  values=review(lang,[row for _,row in chunk])
  for index,(identity,(k,s,v))in enumerate(chunk):
   new=values.get(str(index),v)
   if re.search(r'\{C[0-2]\}',s):new=t.CURRENCIES.sub(lambda m:'{C'+{'GP':'0','TC':'1','XP':'2'}[m[0]]+'}',new)
   bad=t.errors(s,new)
   if bad:rejected.append({'key':k,'errors':bad,'proposal':new});new=v
   cache[identity]={'key':k,'source':s,'before':v,'after':new,'rejected_errors':bad}
  cachefile.write_text(json.dumps(cache,ensure_ascii=False,indent=2)+'\n');print(lang,'review',pos+1,'/',len(chunks),flush=True)
 for k,(identity,ns)in groups.items():
  value=cache[identity]['after']
  for i,n in enumerate(ns):value=value.replace('{N'+str(i)+'}',n)
  for i,c in enumerate(['GP','TC','XP']):value=value.replace('{C'+str(i)+'}',c)
  if not t.errors(t.SOURCE[k],value):data[k]=value
 path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
 print(lang,'review complete',len(groups),flush=True)
if __name__=='__main__':
 for lang in t.LANGS:run(lang)
