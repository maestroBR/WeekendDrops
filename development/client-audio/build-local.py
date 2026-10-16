#!/usr/bin/env python3
"""Build the integrated client using a local SPT install and the original mod archive."""
from pathlib import Path
import argparse, os, subprocess, shutil, zipfile
root = Path(__file__).resolve().parent
repo = root.parent
parser = argparse.ArgumentParser()
parser.add_argument('--game-root', default=os.environ.get('WEEKEND_GAME_ROOT'))
parser.add_argument('--dotnet', default=os.environ.get('DOTNET', 'dotnet'))
parser.add_argument('--original-archive', type=Path)
parser.add_argument('--base', type=Path, help='Use an already layout-patched compatible 1.0.7 UI')
args = parser.parse_args()
if not args.game_root:
    parser.error('Set WEEKEND_GAME_ROOT or pass --game-root')
game = Path(args.game_root).resolve()
os.environ['WEEKEND_GAME_ROOT'] = str(game)
dotnet = args.dotnet
base = root / '.local/WeekendDropsUI.base.dll'
base.parent.mkdir(exist_ok=True)
obj = root / 'obj/native'
obj.mkdir(parents=True, exist_ok=True)
layout = repo / 'client-layout'
subprocess.run([dotnet, 'build', str(layout / 'Layout.csproj'), '-c', 'Release', '-p:GameRoot=' + str(game)], check=True)
if args.base:
    if base.exists():
        raise RuntimeError('A base is already saved; review before selecting another input.')
    shutil.copy2(args.base, base)
if not base.exists():
    if not args.original_archive:
        parser.error('First build requires --original-archive with the original WeekendDrops 1.0.7 ZIP')
    with zipfile.ZipFile(args.original_archive) as archive:
        entry = next(n for n in archive.namelist() if n.replace('\\', '/').endswith('/WeekendDrops/WeekendDropsUI.dll'))
        original = base.parent / 'WeekendDropsUI.original.dll'
        original.write_bytes(archive.read(entry))
    subprocess.run([dotnet, 'run', '--project', str(layout / 'patch/Patch.csproj'), '--', str(original), str(base), str(layout / 'bin/Release/netstandard2.1/ProjectBrainiac.WeekendTextLayout.dll'), str(obj / 'layout-report.json')], check=True)
output = root / 'bin/Release/WeekendDropsUI.dll'
sdk_line = subprocess.check_output([dotnet, '--list-sdks'], text=True).splitlines()[-1]
version, location = sdk_line.split(' ', 1)
sdk = Path(location.strip('[]')) / version
managed = game / 'EscapeFromTarkov_Data/Managed'
refs = [managed / n for n in ['mscorlib.dll', 'System.dll', 'System.Core.dll', 'netstandard.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'Assembly-CSharp.dll', 'Comfort.dll', 'Sirenix.Serialization.dll']] + [game / 'BepInEx/core/BepInEx.dll']
sources = [root / 'NativeAudio.cs'] + [repo / 'client-preview' / n for n in ['KitTooltip.cs', 'LootSampling.cs', 'LootSamplingRuntime.cs']]
command = [dotnet, str(sdk / 'Roslyn/bincore/csc.dll'), '-nologo', '-target:library', '-nostdlib+', '-optimize+', '-debug-', '-out:' + str(obj / 'WeekendDrops.AudioBuild.dll')] + ['-r:' + str(p) for p in refs] + [str(p) for p in sources]
subprocess.run(command, check=True)
subprocess.run([dotnet, 'run', '--project', str(root / 'tools/Integrate.csproj'), '-c', 'Release', '--', str(base), str(obj / 'WeekendDrops.AudioBuild.dll'), str(output), str(root / 'obj/integration-report.json'), str(managed / 'Assembly-CSharp.dll')], check=True)
print('Client output: ' + str(output))
