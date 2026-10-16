#!/usr/bin/env python3
"""Build and stage a clean distribution; never writes to the installed game."""
from pathlib import Path
import argparse, os, subprocess, shutil, zipfile, hashlib, json
root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--game-root', default=os.environ.get('WEEKEND_GAME_ROOT'))
parser.add_argument('--original-archive', type=Path)
parser.add_argument('--dotnet', default=os.environ.get('DOTNET', 'dotnet'))
args = parser.parse_args()
if not args.game_root:
    parser.error('Set WEEKEND_GAME_ROOT or --game-root')
game = Path(args.game_root).resolve()
os.environ['WEEKEND_GAME_ROOT'] = str(game)
version = '1.0.8'
def run(*command):
    subprocess.run(list(command), cwd=root, check=True)
run(args.dotnet, 'build', 'WeekendDrops.csproj', '-c', 'Release', '-p:SptRoot=' + str(game / 'SPT_Runtime'))
client = ['python3', 'client-audio/build-local.py', '--game-root', str(game), '--dotnet', args.dotnet]
if args.original_archive:
    client += ['--original-archive', str(args.original_archive.resolve())]
run(*client)
run(args.dotnet, 'build', 'client-issues/Client.csproj', '-c', 'Release', '-p:GameRoot=' + str(game))
stage = root / 'artifacts' / ('WeekendDrops-Continued-' + version)
if stage.exists():
    shutil.rmtree(stage)
server = stage / 'SPT_Runtime/user/mods/WeekendDrops'
ui = stage / 'BepInEx/plugins/WeekendDrops'
server.mkdir(parents=True)
ui.mkdir(parents=True)
def copy(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / source, target)
copy('bin/Release/WeekendDrops.dll', server / 'WeekendDrops.dll')
shutil.copytree(root / 'config', server / 'config')
shutil.copytree(root / 'db', server / 'db')
copy('client-audio/bin/Release/WeekendDropsUI.dll', ui / 'WeekendDropsUI.dll')
copy('client-layout/bin/Release/netstandard2.1/ProjectBrainiac.WeekendTextLayout.dll', ui / 'ProjectBrainiac.WeekendTextLayout.dll')
copy('assets/icon.png', ui / 'icon.png')
shutil.copytree(root / 'localization/locales', ui / 'locales')
helper = stage / 'BepInEx/plugins/WeekendDrops.LocalizationFixes'
copy('client-issues/bin/Release/netstandard2.1/WeekendDrops.LocalizationFixes.dll', helper / 'WeekendDrops.LocalizationFixes.dll')
shutil.copytree(root / 'client-issues/locales', helper / 'locales')
for name in ['README.md', 'CHANGELOG.md', 'LICENSE']:
    copy(name, stage / name)
manifest = {str(p.relative_to(stage)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(stage.rglob('*')) if p.is_file()}
(stage / 'manifest.sha256.json').write_text(json.dumps(manifest, indent=2) + '\n')
archive = stage.parent / (stage.name + '.zip')
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
    for path in sorted(stage.rglob('*')):
        if path.is_file():
            z.write(path, path.relative_to(stage))
(archive.parent / 'SHA256SUMS.txt').write_text(hashlib.sha256(archive.read_bytes()).hexdigest() + '  ' + archive.name + '\n')
print('Release archive: ' + str(archive))
