"""Mirror the correction plugin's keys from each complete locale, locally."""
from pathlib import Path
import json
P=Path(__file__).resolve().parents[1];destination=P.parent/'client-issues/locales'
keys=json.loads((destination/'en.json').read_text())
for f in (P/'locales').glob('*.json'):
 data=json.loads(f.read_text());subset={k:data[k]for k in keys}
 (destination/f.name).write_text(json.dumps(subset,ensure_ascii=False,indent=2)+'\n')
print('Correction plugin locale files:',len(list(destination.glob('*.json'))),'keys:',len(keys))
