import json,re
from pathlib import Path
p=Path(__file__).resolve().parents[1];s=json.loads((p/'locales/en.json').read_text());m=json.loads((p/'manual-overrides.json').read_text());d=m['ge']
weapons={'an assault rifle':'einem Sturmgewehr','an assault carbine':'einem Sturmkarabiner','a grenade':'einer Granate','grenades':'Granaten','a machine gun':'einem Maschinengewehr','a marksman rifle':'einem Präzisionsgewehr','a pistol':'einer Pistole','a shotgun':'einer Schrotflinte','an SMG':'einer Maschinenpistole','a bolt-action rifle':'einem Repetiergewehr','a suppressed weapon':'einer schallgedämpften Waffe','headshots':'Kopfschüssen','arm damage':'Armschaden','leg damage':'Beinschaden','stomach damage':'Bauchschaden','an optic mounted':'montierter Zieloptik'}
maps={'Customs':'Zollgelände','Factory':'Fabrik','Ground Zero':'Ground Zero','Interchange':'Autobahnkreuz','The Lab':'Labor','the Labyrinth':'Labyrinth','Lighthouse':'Leuchtturm','Reserve':'Reserve','Shoreline':'Küste','Streets of Tarkov':'Straßen von Tarkov','Streets':'Straßen von Tarkov','Woods':'Wälder'}
for k,v in s.items():
 if not isinstance(v,str)or not k.startswith('challenge.'):continue
 new=None
 a=re.fullmatch(r'Kill (an enemy|\d+ enemies) with (.+)',v)
 if a and a[2]in weapons:new=('Einen Feind'if a[1]=='an enemy'else a[1].replace('enemies','Feinde'))+' mit '+weapons[a[2]]+' töten'
 a=re.fullmatch(r'Kill (an enemy|\d+ enemies) from (\d+)m or further( in a single raid)?',v)
 if a:new=('Einen Feind'if a[1]=='an enemy'else a[1].replace('enemies','Feinde'))+f' aus mindestens {a[2]}m Entfernung'+(' in einem einzigen Raid'if a[3]else '')+' töten'
 a=re.fullmatch(r'(?:Land (\d+) headshot kills in a single raid|Get (\d+) headshot kills)',v)
 if a:new=f'{a[1]or a[2]} Feinde'+(' in einem einzigen Raid'if a[1]else '')+' mit Kopfschüssen töten'
 a=re.fullmatch(r'Kill (\d+) enemies on iron sights',v)
 if a:new=f'{a[1]} Feinde mit offener Visierung töten'
 a=re.fullmatch(r'Kill (\d+) (enemies|PMCs|Scavs) in a single (raid|Scav run)',v)
 if a:new=f'{a[1]} '+{'enemies':'Feinde','PMCs':'PMCs','Scavs':'Scavs'}[a[2]]+' in einem einzigen '+('Scav-Raid'if a[3]=='Scav run'else 'Raid')+' töten'
 a=re.fullmatch(r'Get (a|\d+) grenade kills?',v)
 if a:new=('Einen Feind'if a[1]=='a'else a[1]+' Feinde')+' mit Granaten töten'
 a=re.fullmatch(r'(Get a melee kill|Get (\d+) melee kills|Bring a knife to a gunfight: get (\d+) melee kills)',v)
 if a:new=('Mit einem Messer ins Feuergefecht: 'if a[3]else '')+(f'{a[2]or a[3]} Feinde'if a[2]or a[3]else 'Einen Feind')+' im Nahkampf töten'
 a=re.fullmatch(r'Extract from (.+?)( as a Scav)?(?: (\d+) times)?',v)
 if a and a[1]in maps:new=('Als Scav 'if a[2]else '')+(a[3]+' Mal 'if a[3]else '')+f'auf der Karte {maps[a[1]]} lebend extrahieren'
 a=re.fullmatch(r'Extract as a Scav (\d+) times',v)
 if a:new=f'{a[1]} Mal als Scav lebend extrahieren'
 a=re.fullmatch(r'Extract successfully (\d+) times',v)
 if a:new=f'{a[1]} Mal lebend extrahieren'
 a=re.fullmatch(r'Extract with (₽[\d,]+) of loot in one raid',v)
 if a:new=f'In einem einzigen Raid mit Beute im Wert von {a[1]} lebend extrahieren'
 a=re.fullmatch(r'Haul out (₽[\d,]+) of loot (today|this weekend)',v)
 if a:new=('Heute'if a[2]=='today'else 'Dieses Wochenende')+f' Beute im Wert von {a[1]} aus Raids mitbringen'
 a=re.fullmatch(r'(Kill|Eliminate) (\d+) bosses',v)
 if a:new=f'{a[2]} Bosse eliminieren'
 a=re.fullmatch(r'Kill (a PMC|a cultist|a Raider|a Rogue)',v)
 if a:new={'a PMC':'Einen PMC töten','a cultist':'Einen Kultisten töten','a Raider':'Einen Raider töten','a Rogue':'Einen Rogue töten'}[a[1]]
 a=re.fullmatch(r'Kill (\d+) Rogues',v)
 if a:new=f'{a[1]} Rogues töten'
 a=re.fullmatch(r'Get (\d+) kills across Scav raids',v)
 if a:new=f'In Scav-Raids insgesamt {a[1]} Feinde töten'
 a=re.fullmatch(r'Complete (\d+) Scav raids',v)
 if a:new=f'{a[1]} Scav-Raids abschließen'
 a=re.fullmatch(r'Survive (\d+) minutes total( in-raid)? \(resets on death\)',v)
 if a:new=f'In Raids insgesamt {a[1]} Minuten überleben (wird bei Tod zurückgesetzt)'
 if v=='Extract successfully from any map':new='Auf einer beliebigen Karte lebend extrahieren'
 if v=='Extract from a raid alive':new='Einen Raid durch eine erfolgreiche Extraktion überleben'
 if v=='Kill any boss':new='Einen beliebigen Boss töten'
 if v=='Kill the cultist Priest':new='Den Kultistenpriester töten'
 if v=='Hunt down and kill Knight of the Goons':new='Knight von den Goons aufspüren und töten'
 if new:d[k]=new
extras={'ui.deposit.title':'MÜNZEN EINZAHLEN','ui.deposit.done':'GP EINGEZAHLT','ui.deposit.gpCoins':'GP-Münzen','ui.deposit.stashHeader':'MÜNZEN  ·  IM VERSTECK','ui.depositN':'＋ {0} EINZAHLEN','ui.clearObjective':'Ziel erfüllen','shop.shop_weapon_case.desc':'Sichere Aufbewahrung für Gewehre und Ausrüstung','ui.collection.donating':'SPENDEN...'}
for k,v in extras.items():
 if k in s:d[k]=v
(p/'manual-overrides.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n');print('German reviewed overrides',len(d))
