"""Heuristic language detection for long texts; findings require editorial review."""
from pathlib import Path
import json,re
from langid.langid import LanguageIdentifier,model
P=Path(__file__).resolve().parents[1]
MAP={'en':'en','ge':'de','jp':'ja','ru':'ru','fr':'fr','kr':'ko','ch':'zh','es':'es','pl':'pl','cz':'cs','it':'it','po':'pt','hu':'hu','ro':'ro','sk':'sk','tu':'tr','es-mx':'es'}
id=LanguageIdentifier.from_modelstring(model,norm_probs=True);id.set_languages(sorted(set(MAP.values())))
findings=[];checked={}
for code,expected in MAP.items():
 path=P/'locales'/f'{code}.json'
 if not path.exists():continue
 data=json.loads(path.read_text());checked[code]=0
 for k,v in data.items():
  if not isinstance(v,str):continue
  clean=re.sub(r'<[^>]+>|\{[^{}]+\}','',v)
  if len(clean)<80 or len(re.findall(r'[^\W\d_]',clean))<45:continue
  checked[code]+=1;detected,confidence=id.classify(clean)
  if detected!=expected and confidence>.90:findings.append({'language':code,'key':k,'detected':detected,'confidence':float(confidence),'text':v})
(P/'language-audit.json').write_text(json.dumps({'heuristic_only':True,'checked_long_texts':checked,'findings':findings},ensure_ascii=False,indent=2)+'\n')
print('Long-text language checks:',sum(checked.values()),'findings:',len(findings))
