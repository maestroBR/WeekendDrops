import json,re
from pathlib import Path
p=Path(__file__).resolve().parents[1];s=json.loads((p/'locales/en.json').read_text());m=json.loads((p/'manual-overrides.json').read_text())
formats={
'fr':('Sortir vivant de {map}{times}{scav}',' {n} fois',' en tant que Scav'),
'es':('Extráete con vida de {map}{times}{scav}',' {n} veces',' como Scav'),
'es-mx':('Sal con vida de {map}{times}{scav}',' {n} veces',' como Scav'),
'po':('Extraia com vida de {map}{times}{scav}',' {n} vezes',' como Scav'),
'ru':('Эвакуируйтесь с карты «{map}»{times}{scav}',' {n} раз',' за Дикого'),
'ch':('{scav}从{map}成功撤离{times}','{n}次','以Scav身份'),
'jp':('{scav}{map}から{times}生還する','{n}回','Scavとして'),
'kr':('{map}에서{scav} {times}생존 탈출','{n}회 ',' Scav로'),
'cz':('Evakuujte se z mapy {map}{times}{scav}',' {n}krát',' jako Scav'),
'it':('Estrai con successo da {map}{times}{scav}',' {n} volte',' come Scav'),
'pl':('Ewakuuj się z mapy {map}{times}{scav}',' {n} razy',' jako Scav'),
'hu':('Juss ki élve a(z) {map} pályáról{times}{scav}',' {n} alkalommal',' Scavként'),
'ro':('Extrage-te cu succes de pe harta {map}{times}{scav}',' de {n} ori',' ca Scav'),
'sk':('Evakuujte sa z mapy {map}{times}{scav}',' {n}-krát',' ako Scav'),
'tu':('{map} haritasından{scav}{times} sağ çık',' {n} kez',' Scav olarak')}
mapkeys={'Customs':'customs','Factory':'factory','Ground Zero':'groundzero','Interchange':'interchange','The Lab':'lab','Lighthouse':'lighthouse','Reserve':'reserve','Shoreline':'shoreline','Streets of Tarkov':'streets','Streets':'streets','Woods':'woods','the Labyrinth':'labyrinth'}
for code,(form,times,scav)in formats.items():
 for k,v in s.items():
  if not isinstance(v,str)or not k.startswith('challenge.'):continue
  match=re.fullmatch(r'Extract from (.+?)( as a Scav)?(?: (\d+) times)?',v)
  if match and match[1]in mapkeys:
   name=m[code].get('ui.map.'+mapkeys[match[1]],'Labyrinth')
   if name=='Labyrinth':name={'ru':'Лабиринт','ch':'迷宫','jp':'ラビリンス','kr':'미궁','es':'Laberinto','es-mx':'Laberinto','po':'Labirinto','fr':'Labyrinthe','cz':'Labyrint','it':'Labirinto','pl':'Labirynt','hu':'Labirintus','ro':'Labirint','sk':'Labyrint','tu':'Labirent'}[code]
   m[code][k]=form.format(map=name,times=times.format(n=match[3])if match[3]else '',scav=scav if match[2]else '')
 # Correct accusative plural in Czech and the numeral preposition in Romanian.
 for k,v in list(m[code].items()):
  source=s.get(k,'');n=re.search(r'\d+',source)
  if code=='cz'and n and int(n[0])in[2,3,4]:m[code][k]=v.replace(f'{n[0]} nepřátel',f'{n[0]} nepřátele')
  if code=='ro'and n and int(n[0])>=20:m[code][k]=v.replace(f'{n[0]} inamici',f'{n[0]} de inamici')
(p/'manual-overrides.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n');print('Overrides:',sum(len(d)for d in m.values()))
