using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;

if(args.Length!=5)throw new ArgumentException("Expected base UI DLL, compiled audio module, output UI DLL, report path and installed game assembly.");
using var input=AssemblyDefinition.ReadAssembly(args[0]);
using var audio=AssemblyDefinition.ReadAssembly(args[1]);
var module=input.MainModule;
if(module.Types.Any(t=>t.FullName=="WeekendDropsUI.Services.NativeAudio"))throw new InvalidOperationException("Use the saved base UI DLL; audio is already integrated in this input.");
var source=audio.MainModule.GetType("WeekendDropsUI.Services.NativeAudio");
var target=new TypeDefinition(source.Namespace,source.Name,source.Attributes,module.ImportReference(source.BaseType));module.Types.Add(target);
var fields=source.Fields.ToDictionary(f=>f.FullName,f=>new FieldDefinition(f.Name,f.Attributes,module.ImportReference(f.FieldType)));
foreach(var f in fields.Values)target.Fields.Add(f);
var methods=new Dictionary<string,MethodDefinition>();
foreach(var m in source.Methods){
 var clone=new MethodDefinition(m.Name,m.Attributes,module.ImportReference(m.ReturnType)){ImplAttributes=m.ImplAttributes};
 foreach(var p in m.Parameters)clone.Parameters.Add(new ParameterDefinition(p.Name,p.Attributes,module.ImportReference(p.ParameterType)));
 target.Methods.Add(clone);methods.Add(m.FullName,clone);
}
foreach(var original in source.Methods.Where(m=>m.HasBody)){
 var copy=methods[original.FullName];copy.Body=new MethodBody(copy){InitLocals=original.Body.InitLocals,MaxStackSize=original.Body.MaxStackSize};
 var variables=original.Body.Variables.ToDictionary(v=>v,v=>new VariableDefinition(module.ImportReference(v.VariableType)));
 foreach(var v in variables.Values)copy.Body.Variables.Add(v);
 var instructions=original.Body.Instructions.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
 foreach(var i in original.Body.Instructions){
  var clone=instructions[i];clone.OpCode=i.OpCode;
  clone.Operand=i.Operand switch{
   Instruction branch=>instructions[branch],
   Instruction[] branches=>branches.Select(x=>instructions[x]).ToArray(),
   VariableDefinition variable=>variables[variable],
   ParameterDefinition parameter=>copy.Parameters[parameter.Index],
   MethodReference method when method.DeclaringType.FullName==source.FullName=>methods[method.FullName],
   MethodReference method=>module.ImportReference(method),
   FieldReference field when field.DeclaringType.FullName==source.FullName=>fields[field.FullName],
   FieldReference field=>module.ImportReference(field),
   TypeReference type when type.FullName==source.FullName=>target,
   TypeReference type=>module.ImportReference(type),
   null=>null,
   string or int or long or float or double or byte or sbyte=>i.Operand,
   _=>throw new NotSupportedException("Unsupported audio instruction: "+i)
  };copy.Body.Instructions.Add(clone);
 }
 foreach(var handler in original.Body.ExceptionHandlers)copy.Body.ExceptionHandlers.Add(new ExceptionHandler(handler.HandlerType){
  CatchType=handler.CatchType==null?null:module.ImportReference(handler.CatchType),
  TryStart=instructions[handler.TryStart],TryEnd=handler.TryEnd==null?null:instructions[handler.TryEnd],
  HandlerStart=instructions[handler.HandlerStart],HandlerEnd=handler.HandlerEnd==null?null:instructions[handler.HandlerEnd],
  FilterStart=handler.FilterStart==null?null:instructions[handler.FilterStart]});
}
MethodDefinition Audio(string name)=>target.Methods.Single(m=>m.Name==name);
var changed=new HashSet<string>();
void Changed(MethodDefinition m){changed.Add(m.FullName);foreach(var i in m.Body.Instructions){
 if(i.OpCode.OperandType!=OperandType.ShortInlineBrTarget)continue;
 var longName=i.OpCode.Name.Replace(".s","");i.OpCode=typeof(OpCodes).GetFields().Select(f=>f.GetValue(null)).OfType<OpCode>().Single(o=>o.Name==longName);
}}
void Before(MethodDefinition method,Instruction anchor,params Instruction[] added){
 // Redirect existing branches to include the injected instructions.
 foreach(var i in method.Body.Instructions){if(ReferenceEquals(i.Operand,anchor))i.Operand=added[0];else if(i.Operand is Instruction[] branches)for(int n=0;n<branches.Length;n++)if(ReferenceEquals(branches[n],anchor))branches[n]=added[0];}
 foreach(var h in method.Body.ExceptionHandlers){if(h.TryStart==anchor)h.TryStart=added[0];if(h.HandlerStart==anchor)h.HandlerStart=added[0];if(h.FilterStart==anchor)h.FilterStart=added[0];}
 var il=method.Body.GetILProcessor();foreach(var i in added)il.InsertBefore(anchor,i);Changed(method);
}
void Guard(MethodDefinition method,FieldDefinition field,bool already){
 var first=method.Body.Instructions[0];var il=method.Body.GetILProcessor();
 // Existing branches must keep targeting the original first instruction, not the entry guard.
 foreach(var i in new[]{Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,field),Instruction.Create(already?OpCodes.Brfalse:OpCodes.Brtrue,first),Instruction.Create(OpCodes.Ret)})il.InsertBefore(first,i);
 Changed(method);
}
var sound=module.GetType("WeekendDropsUI.Services.SoundUtil");
foreach(var pair in new[]{("Init","Initialize"),("Play","Play")}){
 var m=sound.Methods.Single(m=>m.Name==pair.Item1);m.Body=new MethodBody(m);var il=m.Body.GetILProcessor();il.Append(Instruction.Create(OpCodes.Ldarg_0));il.Append(Instruction.Create(OpCodes.Call,Audio(pair.Item2)));il.Append(Instruction.Create(OpCodes.Ret));Changed(m);
}
var panel=module.GetType("WeekendDropsUI.UI.ChallengePanel");
MethodDefinition Panel(string name)=>panel.Methods.Single(m=>m.Name==name);
var visible=panel.Fields.Single(f=>f.Name=="_visible");
Guard(Panel("Show"),visible,true);Guard(Panel("Hide"),visible,false);
foreach(var name in new[]{"Show","Toggle","SwitchTab"}){
 var method=Panel(name);var replacements=method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand=="ButtonClick").ToList();
 if(replacements.Count!=(name=="Toggle"?2:1))throw new InvalidOperationException("Unexpected navigation structure: "+method.FullName);
 replacements[0].Operand=name=="SwitchTab"?"ButtonBottomBarClick":"MenuInspectorWindowOpen";Changed(method);
}
var marketOpen=Panel("OpenMarketScreen");Guard(marketOpen,panel.Fields.Single(f=>f.Name=="_marketOpen"),true);
foreach(var ret in marketOpen.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).Skip(1).ToArray())Before(marketOpen,ret,Instruction.Create(OpCodes.Call,Audio("Open")));
// Tier button clicks are not confirmations. The confirmed notification retains the reward sound.
var tiers=Panel("DrawTierTrack");var premature=tiers.Body.Instructions.Where(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName==sound.FullName&&m.Name=="Play").ToArray();
if(premature.Length!=1)throw new InvalidOperationException("Unexpected tier-track cue structure");premature[0].Operand=Audio("Click");Changed(tiers);
var donated=Panel("OnDonateComplete");Before(donated,donated.Body.Instructions[0],Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Call,Audio("RewardResult")));
IEnumerable<TypeDefinition> All(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(All));
MethodDefinition Coroutine(string name)=>module.Types.SelectMany(All).Single(t=>t.FullName.StartsWith("WeekendDropsUI.Plugin/<"+name+">",StringComparison.Ordinal)).Methods.Single(m=>m.Name=="MoveNext");
// The original daily coroutine only waits for completion; do not attach worker-thread continuations.
var daily=Coroutine("ClaimDailyCoroutine");var completed=daily.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="get_IsCompleted");
var dailyTask=daily.DeclaringType.Fields.Single(f=>f.FieldType.FullName=="System.Threading.Tasks.Task`1<System.Boolean>");
Before(daily,completed.Next.Next,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,dailyTask),Instruction.Create(OpCodes.Call,Audio("RewardTask")));
var bonus=Coroutine("ClaimDailyBonusCoroutine");var bonusResult=bonus.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="get_Result");
Before(bonus,bonusResult.Next,Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Call,Audio("RewardResult")));
var card=Coroutine("CardActionCoroutine");var resultStore=card.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld&&i.Operand is FieldReference f&&f.Name.Contains("<result>"));
var resultField=(FieldReference)resultStore.Operand;var revealField=card.DeclaringType.Fields.Single(f=>f.Name=="reveal");
Before(card,resultStore.Next,Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,resultField),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Ldfld,revealField),Instruction.Create(OpCodes.Call,Audio("CardResult")));
// Observe only revealed kit cards; render tooltip after every card to avoid clipping/overdraw.
var marketCard=Panel("DrawMarketCard");
Before(marketCard,marketCard.Body.Instructions[0],Instruction.Create(OpCodes.Ldarg_1),Instruction.Create(OpCodes.Ldarg_2),Instruction.Create(OpCodes.Call,Audio("HoverKit")));
// Reset hover each GUI event and draw kit details above the cards.
var onGui=module.GetType("WeekendDropsUI.Plugin").Methods.Single(m=>m.Name=="OnGUI");
var begin=onGui.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="UIScale"&&m.Name=="Begin");
Before(onGui,begin.Next,Instruction.Create(OpCodes.Call,Audio("BeginKitFrame")));
var ceremony=onGui.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="TrophyCeremony"&&m.Name=="Draw");
Before(onGui,ceremony.Next,Instruction.Create(OpCodes.Call,Audio("DrawKitTooltip")));
// Avoid walking LootNet's inventory-backed getter every rendered frame.
var raidUpdate=module.GetType("WeekendDropsUI.Plugin").Methods.Single(m=>m.Name=="Update");
var lootRead=raidUpdate.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="LootNetInterop"&&m.Name=="CurrentLootValue");
var sample=lootRead.Next;
if(sample.Operand is not MethodReference sampleCall||sampleCall.Name!="SampleLootValue")throw new InvalidOperationException("Unexpected raid loot sampling structure");
Before(raidUpdate,lootRead,Instruction.Create(OpCodes.Call,Audio("ShouldSampleLoot")),Instruction.Create(OpCodes.Brfalse,sample.Next));
// Take one uncached sample before the extraction coroutine snapshots LootValue.
var result=module.GetType("WeekendDropsUI.Plugin").Methods.Single(m=>m.Name=="ReportRaidResult");
var finalRead=Instruction.Create(OpCodes.Call,(MethodReference)lootRead.Operand);
Before(result,result.Body.Instructions[0],finalRead,Instruction.Create(OpCodes.Call,sampleCall));
// Reject references to the intermediate build assembly; the client must be self-contained.
if(module.AssemblyReferences.Any(r=>r.Name==audio.Name.Name))throw new InvalidOperationException("Audio build module leaked as a runtime dependency");
using var game=AssemblyDefinition.ReadAssembly(args[4]);
Changed(ApplyPresetPreview.Apply(module,game.MainModule));
var checkedInstructions=module.Types.SelectMany(All).SelectMany(t=>t.Methods).Where(m=>changed.Contains(m.FullName)||m.DeclaringType==target).Sum(StackCheck.Verify);
// Keep loader GUID stable for existing configuration and companion dependencies.
input.Name.Version=new Version(1,0,8,0);
var plugin=module.GetType("WeekendDropsUI.Plugin").CustomAttributes.Single(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin");
plugin.ConstructorArguments[1]=new CustomAttributeArgument(module.TypeSystem.String,"Weekend Drops Continued");
plugin.ConstructorArguments[2]=new CustomAttributeArgument(module.TypeSystem.String,"1.0.9");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);input.Write(args[2]);
using var verified=AssemblyDefinition.ReadAssembly(args[2]);
if(verified.MainModule.GetType(target.FullName)==null)throw new InvalidOperationException("Integrated type is missing");
foreach(var m in verified.MainModule.Types.SelectMany(All).SelectMany(t=>t.Methods).Where(m=>m.HasBody)){
 var body=m.Body.Instructions.ToHashSet();foreach(var i in body){if(i.Operand is Instruction branch&&!body.Contains(branch))throw new InvalidOperationException("Foreign branch");if(i.Operand is Instruction[] branches&&branches.Any(b=>!body.Contains(b)))throw new InvalidOperationException("Foreign switch target");}
}
using var baseline=AssemblyDefinition.ReadAssembly(args[0]);
string Body(MethodDefinition method){
 if(!method.HasBody)return "no-body";
 var indices=method.Body.Instructions.Select((i,n)=>(i,n)).ToDictionary(x=>x.i,x=>x.n);
 string Operand(object? value)=>value switch{
  Instruction i=>"target:"+indices[i],Instruction[] a=>string.Join(",",a.Select(i=>indices[i])),
  MethodReference m=>m.FullName,FieldReference f=>f.FullName,TypeReference t=>t.FullName,
  ParameterDefinition p=>"parameter:"+p.Index,VariableDefinition v=>"local:"+v.Index,
  null=>"",_=>value.ToString()??""};
 return string.Join("\n",method.Body.Instructions.Select(i=>i.OpCode.Name+" "+Operand(i.Operand)));
}
var oldMethods=baseline.MainModule.Types.SelectMany(All).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
var newMethods=verified.MainModule.Types.SelectMany(All).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
var unexpectedlyChanged=oldMethods.Values.Where(m=>!changed.Contains(m.FullName)&&(!newMethods.TryGetValue(m.FullName,out var other)||Body(m)!=Body(other))).Select(m=>m.FullName).ToArray();
if(unexpectedlyChanged.Length!=0)throw new InvalidOperationException("Unrelated UI methods changed: "+string.Join(",",unexpectedlyChanged));
var oldPlugins=baseline.MainModule.Types.SelectMany(All).SelectMany(t=>t.CustomAttributes).Count(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin");
var newPlugins=verified.MainModule.Types.SelectMany(All).SelectMany(t=>t.CustomAttributes).Count(a=>a.AttributeType.FullName=="BepInEx.BepInPlugin");
if(oldPlugins!=newPlugins)throw new InvalidOperationException("The build introduced another plugin");
File.WriteAllText(args[3],JsonSerializer.Serialize(new{input=args[0],inputSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))),output=args[2],outputSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[2]))),integratedType=target.FullName,changedMethods=changed.Order().ToArray(),externalAudioDependency=false,checkedInstructions,unchangedExistingMethods=oldMethods.Count-changed.Count,pluginCount=newPlugins},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Integrated native sounds into WeekendDropsUI.dll: "+changed.Count+" changed methods; no new runtime DLL dependency.");
