"""Local, resumable localization draft; no GitHub or live-game writes."""
from pathlib import Path
from collections import Counter
import json,re,urllib.request,time,sys
ROOT=Path(__file__).resolve().parents[1]
AUDIT=ROOT.parents[2]/'outputs/weekend-localization-20261014'
SOURCE=json.loads((ROOT/'locales/en.json').read_text())
LANGS={'ge':('Deutsch','German','stash=Versteck; extract=überleben und entkommen; claim a reward=abholen; reroll a challenge=Aufgabe wechseln; challenge=Herausforderung'), 'ru':('Русский','Russian','stash=схрон; PMC=ЧВК; Scav=Дикий; extract=эвакуироваться; claim reward=получить; reroll challenge=заменить задание; found in raid=найдено в рейде'), 'fr':('Français','French','stash=réserve; extract=réussir à sortir du raid; claim reward=récupérer; reroll challenge=remplacer le défi'), 'ch':('简体中文','Simplified Chinese','stash=仓库; extract=撤离; claim reward=领取; reroll challenge=更换挑战; PMC=PMC; Scav=Scav; found in raid=战局中找到'), 'es':('Español','Spanish (Spain)','stash=alijo; extract=extraerse con vida; claim reward=recoger; reroll challenge=cambiar desafío'), 'cz':('Čeština','Czech','stash=skrýš; extract=evakuovat se; claim reward=vyzvednout; reroll challenge=vyměnit výzvu'), 'it':('Italiano','Italian','stash=scorta; extract=estrarre con successo dal raid; claim reward=ritirare; reroll challenge=cambiare sfida'), 'po':('Português (Brasil)','Brazilian Portuguese','stash=depósito; hideout=esconderijo; extract=extrair com vida; claim reward=resgatar; reroll challenge=trocar desafio; found in raid=encontrado em raid'), 'hu':('Magyar','Hungarian','stash=raktár; extract=kimenekülni a bevetésből; claim reward=jutalom átvétele; reroll challenge=kihívás cseréje'), 'ro':('Română','Romanian','stash=depozit; extract=extracție cu succes din raid; claim reward=ridică recompensa; reroll challenge=schimbă provocarea'), 'sk':('Slovenčina','Slovak','stash=sklad; extract=evakuovať sa; claim reward=vyzdvihnúť; reroll challenge=vymeniť výzvu'), 'tu':('Türkçe','Turkish','stash=depo; extract=baskından sağ çıkıp tahliye olmak; claim reward=ödülü al; reroll challenge=hedefi değiştir'), 'jp':('日本語','Japanese','stash=保管庫; extract=脱出する; claim reward=受け取る; reroll challenge=チャレンジを変更; found in raid=レイド内で発見'), 'kr':('한국어','Korean','stash=보관함; extract=탈출; claim reward=보상 수령; reroll challenge=도전 변경; found in raid=레이드에서 발견'), 'pl':('Polski','Polish','stash=schowek; extract=ewakuować się; claim reward=odebrać; reroll challenge=zmienić wyzwanie'), 'es-mx':('Español (Latinoamérica)','neutral Latin American Spanish','stash=alijo; extract=salir con vida del asalto; claim reward=recibir; reroll challenge=cambiar desafío. Avoid Spain-specific forms such as vosotros.')}
FMT=re.compile(r'\{[^{}\n]+\}')
TAGS=re.compile(r'<[^>]+>')
ATOMS=re.compile(r'<[^>]+>|\{[^{}\n]+\}|\d+(?:\.\d+)?')
CURRENCIES=re.compile(r'(?<![A-Za-z])(?:GP|TC|XP)(?![A-Za-z])')
SYMBOLS='✓✕↻＋◂▸→☾○₽€$%×+'
def canonical(text):
 numbers=[]
 def sub(m):
  if m[0].startswith(('<','{')):return m[0]
  n=len(numbers);numbers.append(m[0]);return '{N'+str(n)+'}'
 return CURRENCIES.sub(lambda m:'{C'+{'GP':'0','TC':'1','XP':'2'}[m[0]]+'}',ATOMS.sub(sub,text)),numbers

def errors(source,target):
 result=[]
 if not isinstance(target,str)or not target.strip():return ['blank']
 if Counter(FMT.findall(source))!=Counter(FMT.findall(target)):result.append('placeholders')
 if TAGS.findall(source)!=TAGS.findall(target):result.append('markup')
 if Counter(CURRENCIES.findall(source))!=Counter(CURRENCIES.findall(target)):result.append('currencies')
 if any(source.count(s)!=target.count(s)for s in SYMBOLS):result.append('symbols')
 if source.count('\n')!=target.count('\n'):result.append('newlines')
 return result

def _request(lang,entries,fix=False):
 system=f'''You are a professional game localizer. Translate every provided text into {LANGS[lang][1]} for WeekendDrops, an Escape from Tarkov SPT/Fika mod.
Context: weekly and daily combat challenges, rewards, a virtual GP shop, collectible donations, the night market, and bounty contracts. "Extract" means successfully leave a raid alive, not extracting data. "Claim" means collect an earned reward, never assert a legal right. "Reroll" means replace a challenge. "Crew" refers to a hostile NPC squad. "Stock" in shop means inventory availability, not a weapon stock. Body-part kill objectives refer to fatal damage to the named body part. "Found in raid" is an item status. "Kill" is combat elimination. Never invent extra kills, quests or mechanics.
Terminology: {LANGS[lang][2]}.
Preserve GP, TC, XP, Fika, Weekend Drops, TarCoin, weapon model names, every {{0}}/{{N0}}/{{C0}} format placeholder (C0=GP, C1=TC, C2=XP), all Unity <color>/<b> tags EXACTLY IN ORDER, all symbols ✓✕↻＋◂▸→☾○ and newline breaks. Translate the words inside tags. Use concise natural in-game wording, grammatical plurals and cases. Keep ALL CAPS for buttons/headings when the alphabet allows. An input label may be short: use the key for context. Do not expand abbreviations or add explanations.
Return ONLY JSON: {{"translations":{{"0":"translated first text","1":"translated second text"}}}}. Keep every numeric input key exactly. Translate all entries, not a summary.'''
 system+=' Context keys in input order (do not include keys in output): '+', '.join(e['key'] for e in entries)
 if fix:
  system+=' The previous attempt violated a format requirement. Tokens MUST match these exact counts for each corresponding entry: '+json.dumps([{"formats":dict(Counter(FMT.findall(e['text']))),"currencies":dict(Counter(CURRENCIES.findall(e['text']))),"newlines":e['text'].count('\n')}for e in entries])+'. Keep XP as XP (do not replace it with a local word for experience); keep GP as GP in EVERY mention. Do not omit repetitions even if the wording could be shortened.'
 data={'messages':[{'role':'system','content':system},{'role':'user','content':json.dumps({str(i):e['text'] for i,e in enumerate(entries)},ensure_ascii=False)}],'temperature':0.15 if fix else 0.0,'max_tokens':min(4500,max(900,int(sum(len(e['text'])for e in entries)*0.9)+700)),'frequency_penalty':0.12,'response_format':{'type':'json_schema','json_schema':{'name':'translations','strict':True,'schema':{'type':'object','properties':{'translations':{'type':'object','properties':{str(i):{'type':'string'}for i in range(len(entries))},'required':[str(i)for i in range(len(entries))],'additionalProperties':False}},'required':['translations'],'additionalProperties':False}}}}
 req=urllib.request.Request('http://127.0.0.1:18476/v1/chat/completions',data=json.dumps(data).encode(),headers={'Content-Type':'application/json'})
 result=json.load(urllib.request.urlopen(req,timeout=240))
 parsed=json.loads(result['choices'][0]['message']['content']);values=parsed['translations'];values=[values[str(i)]for i in range(len(entries))]if isinstance(values,dict)else values;values=[v.get('text',v.get('translation','')) if isinstance(v,dict)else v for v in values]
 if len(values)!=len(entries):raise ValueError(f'Expected {len(entries)} translations; got {len(values)}')
 return values


def request(lang,entries,fix=False):
 for attempt in range(2):
  try:return _request(lang,entries,fix)
  except (ValueError,KeyError,TimeoutError) as exc:
   print(lang,'request retry',type(exc).__name__,flush=True)
   if attempt==1:
    if len(entries)==1:raise
    mid=len(entries)//2
    return request(lang,entries[:mid],fix)+request(lang,entries[mid:],fix)


def run(lang):
 cachefile=AUDIT/f'cache-{lang}.json';cache=json.loads(cachefile.read_text())if cachefile.exists()else{}
 existing={}
 manual=json.loads((ROOT/'manual-overrides.json').read_text()).get(lang,{})
 if lang in ['po','ru']:
  p=AUDIT/'original-locales'/f'{lang}.json'
  if p.exists():existing=json.loads(p.read_text())
 unique={};reused=0;result={};groups={}
 for key,text in SOURCE.items():
  if not isinstance(text,str):continue
  if key in manual and not errors(text,manual[key]):result[key]=manual[key];reused+=1;continue
  value=existing.get(key)
  if value and value!=text and not errors(text,value):result[key]=value;reused+=1;continue
  canon,numbers=canonical(text);identity=canon;unique.setdefault(identity,{'key':key,'text':canon});groups[key]=(identity,numbers)
 needed=[(identity,entry)for identity,entry in unique.items()if identity not in cache]
 print(lang,'translate patterns',len(needed),'reuse',reused,flush=True)
 chunks=[];chunk=[];size=0
 for pair in needed:
  n=len(pair[1]['text'])
  if chunk and (len(chunk)>=24 or size+n>4300):chunks.append(chunk);chunk=[];size=0
  chunk.append(pair);size+=n
 if chunk:chunks.append(chunk)
 for pos,chunk in enumerate(chunks):
  values=request(lang,[e for _,e in chunk])
  for (identity,entry),value in zip(chunk,values):
   if re.search(r'\{C[0-2]\}',entry['text']):value=CURRENCIES.sub(lambda m:'{C'+{'GP':'0','TC':'1','XP':'2'}[m[0]]+'}',value)
   failures=errors(entry['text'],value)
   for retry in range(5):
    if not failures:break
    value=request(lang,[entry],fix=True)[0]
    if re.search(r'\{C[0-2]\}',entry['text']):value=CURRENCIES.sub(lambda m:'{C'+{'GP':'0','TC':'1','XP':'2'}[m[0]]+'}',value)
    failures=errors(entry['text'],value)
   if failures:
    parts=re.split(r'(\{[^{}\n]+\}|<[^>]+>|\n)',entry['text'])
    fragments=[{'key':entry['key']+'.fragment','text':part.strip()}for part in parts if part.strip() and not FMT.fullmatch(part) and not TAGS.fullmatch(part) and any(c.isalpha()for c in part)]
    translated=request(lang,fragments,fix=True) if fragments else []
    pos=0;joined=[]
    for part in parts:
     if part.strip() and not FMT.fullmatch(part) and not TAGS.fullmatch(part) and any(c.isalpha()for c in part):
      valuePart=translated[pos];pos+=1
      joined.append((' 'if part.startswith(' ')else '')+valuePart+(' 'if part.endswith(' ')else ''))
     else:joined.append(part)
    value=''.join(joined)
    if re.search(r'\{C[0-2]\}',entry['text']):value=CURRENCIES.sub(lambda m:'{C'+{'GP':'0','TC':'1','XP':'2'}[m[0]]+'}',value)
    failures=errors(entry['text'],value)
    fallback=AUDIT/'fragment-review.json';records=json.loads(fallback.read_text())if fallback.exists()else[];records.append({'language':lang,'key':entry['key'],'source':entry['text'],'translation':value,'errors':failures});fallback.write_text(json.dumps(records,ensure_ascii=False,indent=2))
    if failures:raise ValueError(f'{lang} {entry["key"]}: {failures}; {value}')
   cache[identity]=value
  cachefile.write_text(json.dumps(cache,ensure_ascii=False,indent=2))
  print(lang,f'batch {pos+1}/{len(chunks)}',flush=True)
 for key,(identity,numbers)in groups.items():
  value=cache[identity]
  for index,n in enumerate(numbers):value=value.replace('{N'+str(index)+'}',n)
  for index,currency in enumerate(['GP','TC','XP']):value=value.replace('{C'+str(index)+'}',currency)
  if lang=='hu':value=re.sub(r'(?<![A-Za-z])(GP|TC|XP)(?=[a-záéíóöőúüű])',r'\1-',value)
  if errors(SOURCE[key],value):raise ValueError((lang,key,errors(SOURCE[key],value)))
  result[key]=value
 fonts={'ch':'Microsoft YaHei, Noto Sans CJK SC, SimHei','jp':'Yu Gothic, Noto Sans CJK JP, Meiryo, MS Gothic','kr':'Malgun Gothic, Noto Sans CJK KR, NanumGothic'}
 data={'_meta':{'name':LANGS[lang][0],'credit':existing.get('_meta',{}).get('credit',''),'font':fonts.get(lang,'')}}
 data.update({key:result[key]for key,text in SOURCE.items()if isinstance(text,str)})
 (ROOT/'locales'/f'{lang}.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
 print(lang,'complete',len(result),flush=True)
if __name__=='__main__':
 for lang in sys.argv[1:]or LANGS:run(lang)
