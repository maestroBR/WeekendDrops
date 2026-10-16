"""Use translated UI labels for lingering English tutorial button references."""
from pathlib import Path
import json
P=Path(__file__).resolve().parents[1];records=[]
deposit={'ge':'EINZAHLEN','fr':'DÉPOSER','es':'DEPOSITAR','es-mx':'DEPOSITAR','po':'DEPOSITAR','ru':'ВНЕСТИ','ch':'存入','jp':'入金','kr':'입금','cz':'VLOŽIT','it':'DEPOSITA','pl':'WPŁAĆ','hu':'BEFIZETÉS','ro':'DEPUNE','sk':'VLOŽIŤ','tu':'YATIR'}
double={'ge':('Doppelte Belohnung','Doppelte XP'),'fr':('Récompenses doublées','XP doublés'),'es':('Recompensas dobles','Doble XP'),'es-mx':('Recompensas dobles','Doble XP'),'po':('Recompensas dobradas','XP dobrado'),'ru':('Двойная награда','Двойной XP'),'ch':('双倍奖励','双倍XP'),'jp':('報酬倍増','XP倍増'),'kr':('보상 두 배','XP 두 배'),'cz':('Dvojnásobná odměna','Dvojnásobné XP'),'it':('Ricompense doppie','XP doppi'),'pl':('Podwójna wypłata','Podwójne XP'),'hu':('Dupla jutalom','Dupla XP'),'ro':('Recompense duble','XP dublu'),'sk':('Dvojnásobná odmena','Dvojnásobné XP'),'tu':('Çifte ödül','Çifte XP')}
for code in deposit:
 f=P/'locales'/f'{code}.json';d=json.loads(f.read_text());labels={'Weekly':d['ui.tab.weekly'],'Daily':d['ui.tab.daily'],'Shop':d['ui.tab.shop'],'Contracts':d['ui.tab.contracts'],'CLAIM':d['ui.claim'],'Reroll':d['ui.reroll.confirmBtn'],'DEPOSIT':deposit[code],'Double Pay':double[code][0],'Double XP':double[code][1]}
 for key,value in list(d.items()):
  if not isinstance(value,str):continue
  new=value
  for a,b in labels.items():new=new.replace('<b>'+a+'</b>','<b>'+b+'</b>')
  if new!=value:records.append({'language':code,'key':key,'before':value,'after':new});d[key]=new
 f.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n')
(P/'tutorial-label-review.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n');print('Tutorial label corrections:',len(records))
