from pathlib import Path
import json,re
p=Path(__file__).resolve().parents[1];m=json.loads((p/'manual-overrides.json').read_text());s=json.loads((p/'locales/en.json').read_text())
keys=['an assault rifle','an assault carbine','a machine gun','a marksman rifle','a pistol','a shotgun','an SMG','a bolt-action rifle','a suppressed weapon','an optic mounted']
data={
'fr':('Éliminer {n} ennemis {weapon}',['avec un fusil d’assaut','avec une carabine d’assaut','avec une mitrailleuse','avec un fusil de précision semi-automatique','avec un pistolet','avec un fusil de chasse','avec un pistolet-mitrailleur','avec un fusil à verrou','avec une arme équipée d’un silencieux','avec une optique montée']),
'es':('Mata a {n} enemigos {weapon}',['con un fusil de asalto','con una carabina de asalto','con una ametralladora','con un fusil de tirador designado','con una pistola','con una escopeta','con un subfusil','con un fusil de cerrojo','con un arma silenciada','con una mira óptica montada']),
'es-mx':('Elimina a {n} enemigos {weapon}',['con un rifle de asalto','con una carabina de asalto','con una ametralladora','con un rifle de tirador designado','con una pistola','con una escopeta','con un subfusil','con un rifle de cerrojo','con un arma con silenciador','con una mira óptica instalada']),
'po':('Elimine {n} inimigos {weapon}',['com um fuzil de assalto','com uma carabina de assalto','com uma metralhadora','com um fuzil de atirador designado','com uma pistola','com uma escopeta','com uma submetralhadora','com um fuzil de ferrolho','com uma arma equipada com silenciador','com uma mira óptica instalada']),
'ru':('Убейте {n} противников {weapon}',['из штурмовой винтовки','из штурмового карабина','из пулемёта','из марксманской винтовки','из пистолета','из дробовика','из пистолета-пулемёта','из винтовки с продольно-скользящим затвором','из оружия с глушителем','из оружия с установленным оптическим прицелом']),
'ch':('使用{weapon}击杀{n}名敌人',['突击步枪','突击卡宾枪','机枪','精确射手步枪','手枪','霰弹枪','冲锋枪','栓动步枪','装有消音器的武器','装有光学瞄具的武器']),
'jp':('{weapon}で敵を{n}人倒す',['アサルトライフル','アサルトカービン','機関銃','マークスマンライフル','ハンドガン','ショットガン','サブマシンガン','ボルトアクションライフル','サプレッサー付きの武器','光学サイトを装着した武器']),
'kr':('{weapon} 적 {n}명 처치',['돌격소총으로','돌격 카빈으로','기관총으로','지정사수소총으로','권총으로','산탄총으로','기관단총으로','볼트액션 소총으로','소음기를 장착한 무기로','광학 조준기를 장착한 무기로']),
'cz':('Zabijte {n} nepřátel {weapon}',['útočnou puškou','útočnou karabinou','kulometem','puškou pro přesnou střelbu','pistolí','brokovnicí','samopalem','opakovací puškou','zbraní s tlumičem','zbraní s namontovanou optikou']),
'it':('Uccidi {n} nemici {weapon}',['con un fucile d’assalto','con una carabina d’assalto','con una mitragliatrice','con un fucile da tiratore scelto','con una pistola','con un fucile a canna liscia','con una pistola mitragliatrice','con un fucile a otturatore manuale','con un’arma silenziata','con un’ottica montata']),
'pl':('Zabij {n} przeciwników {weapon}',['karabinem szturmowym','karabinkiem szturmowym','karabinem maszynowym','karabinem wyborowym','pistoletem','strzelbą','pistoletem maszynowym','karabinem powtarzalnym','bronią z tłumikiem','bronią z zamontowanym celownikiem optycznym']),
'hu':('Ölj meg {n} ellenfelet {weapon}',['gépkarabéllyal','rohamkarabéllyal','géppuskával','félautomata mesterlövészpuskával','pisztollyal','sörétes puskával','géppisztollyal','forgó-tolózáras puskával','hangtompítós fegyverrel','felszerelt optikai irányzékkal']),
'ro':('Elimină {n} inamici {weapon}',['cu o pușcă de asalt','cu o carabină de asalt','cu o mitralieră','cu o pușcă de precizie','cu un pistol','cu o pușcă cu alice','cu un pistol-mitralieră','cu o pușcă cu închizător manual','cu o armă cu amortizor','cu o armă cu vizor optic montat']),
'sk':('Zabite {n} nepriateľov {weapon}',['útočnou puškou','útočnou karabínou','guľometom','puškou na presnú streľbu','pištoľou','brokovnicou','samopalom','opakovacou puškou','zbraňou s tlmičom','zbraňou s namontovanou optikou']),
'tu':('{weapon} {n} düşman öldür',['taarruz tüfeğiyle','taarruz karabinasıyla','makineli tüfekle','keskin nişancı destek tüfeğiyle','tabancayla','av tüfeğiyle','hafif makineli tüfekle','sürgülü tüfekle','susturuculu silahla','optik nişangâh takılı silahla'])}
for code,(template,weapons)in data.items():
 for k,v in s.items():
  if not isinstance(v,str)or not k.startswith('challenge.'):continue
  a=re.fullmatch(r'Kill (\d+) enemies with (.+)',v)
  if a and a[2]in keys:
   text=template.format(n=a[1],weapon=weapons[keys.index(a[2])])
   if code=='cz'and int(a[1])in[2,3,4]:text=text.replace('nepřátel','nepřátele')
   if code=='ro'and int(a[1])>=20:text=text.replace(' inamici',' de inamici')
   if code=='tu':text=text[0].upper()+text[1:]
   m[code][k]=text
(p/'manual-overrides.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n');print('Overrides',sum(len(d)for d in m.values()))
