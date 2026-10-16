"""Validate the full English key set and runtime-sensitive text in 17 game locales."""
from pathlib import Path
from collections import Counter
import json,re,sys,hashlib
ROOT=Path(__file__).resolve().parents[1]
LANGUAGES=['en','ge','jp','ru','fr','kr','ch','es','pl','cz','it','po','hu','ro','sk','tu','es-mx']
FMT=re.compile(r'\{[^{}\n]+\}');TAGS=re.compile(r'<[^>]+>');DIGITS=re.compile(r'\d+(?:\.\d+)?');CURRENCY=re.compile(r'(?<![A-Za-z])(?:GP|TC|XP)(?![A-Za-z])')
SYMBOLS='✓✕↻＋◂▸→☾○₽€$%×+'
def duplicates(pairs):
 d={}
 for k,v in pairs:
  if k in d:raise ValueError('Duplicate key: '+k)
  d[k]=v
 return d
source=json.loads((ROOT/'locales/en.json').read_text(),object_pairs_hook=duplicates);keys={k for k,v in source.items()if isinstance(v,str)}
def counts(pattern,text):return Counter(pattern.findall(text))
def digits(text):
 text=TAGS.sub('',FMT.sub('',text));text=re.sub(r'(?<![\d,])([0-9]+),([0-9]{1,2})(?![\d,])',r'\1.\2',text)
 return counts(DIGITS,text)
report={'source_keys':len(keys),'languages':{},'gameplay_validated':False,'native_speaker_review':False}
for code in LANGUAGES:
 path=ROOT/'locales'/f'{code}.json';issues=[];warnings=[]
 if not path.exists():report['languages'][code]={'missing_file':True};continue
 target=json.loads(path.read_text(),object_pairs_hook=duplicates)
 if set(target)-{'_meta'}!=keys:issues.append({'key':'*','problem':'coverage','missing':sorted(keys-set(target)),'extra':sorted(set(target)-keys-{'_meta'})})
 for key in keys:
  value=target.get(key);original=source[key]
  if not isinstance(value,str)or not value.strip():issues.append({'key':key,'problem':'blank or non-string'});continue
  for name,bad in [('placeholders',counts(FMT,original)!=counts(FMT,value)),('format braces',original.count('{')!=value.count('{')or original.count('}')!=value.count('}')),('markup',TAGS.findall(original)!=TAGS.findall(value)),('numbers',digits(original)!=digits(value)),('currency',counts(CURRENCY,original)!=counts(CURRENCY,value)),('symbols',any(original.count(c)!=value.count(c)for c in SYMBOLS)),('newlines',original.count('\n')!=value.count('\n'))]:
   if bad:issues.append({'key':key,'problem':name,'source':original,'target':value})
  if re.search(r'\b(?:challenge|ui|shop|market|collection|contract|modifier)\.[a-zA-Z0-9_.]+',value):issues.append({'key':key,'problem':'context key leaked into text'})
  if re.search(r'\{[NC]\d+\}',value):issues.append({'key':key,'problem':'temporary translation token'})
  if code!='en'and len(original)<40 and len(value)>max(50,len(original)*1.7):warnings.append({'key':key,'problem':'layout length','source_chars':len(original),'target_chars':len(value)})
  if code in ['ch','jp','kr','ru']and len(original)>45:
   alphabet={'ch':r'[\u3400-\u9fff]','jp':r'[\u3040-\u30ff\u3400-\u9fff]','kr':r'[\uac00-\ud7af]','ru':r'[\u0400-\u04ff]'}[code]
   if not re.search(alphabet,value):warnings.append({'key':key,'problem':'expected script absent'})
 font=target.get('_meta',{}).get('font','')
 if code in ['ch','jp','kr']and not font:issues.append({'key':'_meta.font','problem':'CJK font missing'})
 report['languages'][code]={'strings':len(set(target)&keys),'errors':issues,'warnings':warnings,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
for code,result in report['languages'].items():print(code,'MISSING'if result.get('missing_file')else f"{result['strings']} strings / {len(result['errors'])} errors / {len(result['warnings'])} review warnings")
if any(v.get('missing_file')or v.get('errors')for v in report['languages'].values()):sys.exit(1)
