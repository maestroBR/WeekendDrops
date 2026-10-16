using Mono.Cecil;using Mono.Cecil.Cil;
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../.."));
var baseline=Path.Combine(root,"client-audio/.local/WeekendDropsUI.base.dll");
using var game=AssemblyDefinition.ReadAssembly(Path.Combine(Environment.GetEnvironmentVariable("WEEKEND_GAME_ROOT") ?? throw new Exception("Set WEEKEND_GAME_ROOT"),"EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll"));
using var ui=AssemblyDefinition.ReadAssembly(baseline);int passed=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);passed++;}
var cache=ui.MainModule.GetType("WeekendDropsUI.Services.WdItemIconCache");
var load=cache.NestedTypes.Single(t=>t.Name.StartsWith("<Load>")).Methods.Single(m=>m.Name=="MoveNext");
Check(load.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CreateItem"),"Original failure was not reproduced");
var fixedMethod=ApplyPresetPreview.Apply(ui.MainModule,game.MainModule);
Check(fixedMethod==load,"Wrong UI routine changed");
Check(!load.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName=="EFT.ItemFactory"&&m.Name=="CreateItem"),"Bare item creation remains in icon loader");
Check(load.Body.Instructions.Count(i=>i.Operand is MethodReference m&&m.Name=="GetPresetItem")==1,"Expected one native preset lookup");
Check(StackCheck.Verify(load)>100,"Preview coroutine did not pass stack verification");
var native=game.MainModule.GetType("EFT.ItemFactory").Methods.Single(m=>m.Name=="GetPresetItem");
Check(native.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CloneItem"),"Native presets must be cloned, not shared mutable inventory");
Check(native.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="CreateItem"),"Native fallback for non-preset items missing");
Check(load.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="GetItemSpriteAsync"),"Native image generation removed");
using var changed=AssemblyDefinition.ReadAssembly(baseline);
var changedLoad=changed.MainModule.GetType("WeekendDropsUI.Services.WdItemIconCache").NestedTypes.Single(t=>t.Name.StartsWith("<Load>")).Methods.Single(m=>m.Name=="MoveNext");
var call=changedLoad.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="CreateItem");call.Previous.OpCode=OpCodes.Ldc_I4_0;
bool rejected=false;try{ApplyPresetPreview.Apply(changed.MainModule,game.MainModule);}catch(InvalidOperationException){rejected=true;}
Check(rejected,"Unexpected base UI structure was silently modified");
using var integrated=AssemblyDefinition.ReadAssembly(Path.Combine(root,"client-audio/bin/Release/WeekendDropsUI.dll"));
var helper=integrated.MainModule.GetType("WeekendDropsUI.Services.NativeAudio");
Check(!helper.Methods.Any(m=>new[]{"DrawPreview","InspectPreview","ClosePreview","BeginPreviewFrame","EndPreviewFrame"}.Contains(m.Name)),"3D preview methods remain");
Check(!integrated.MainModule.Types.SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName==helper.FullName&&m.Name=="SetupItemPreview"),"3D setup remains");
foreach(var name in new[]{"DrawShopCard","DrawBundlePreview"}){
 var method=integrated.MainModule.GetType("WeekendDropsUI.UI.ChallengePanel").Methods.Single(m=>m.Name==name);
 Check(method.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.DeclaringType.Name=="WdItemIconCache"&&m.Name=="Draw"),"Original icon renderer missing");
}
var market=integrated.MainModule.GetType("WeekendDropsUI.UI.ChallengePanel").Methods.Single(m=>m.Name=="DrawMarketCard");
Check(market.Body.Instructions.Count(i=>i.Operand is MethodReference m&&m.Name=="HoverKit")==1,"Kit hover hook missing/duplicated");
var hover=helper.Methods.Single(m=>m.Name=="HoverKit");
foreach(var field in new[]{"Revealed","Mystery","Bought","Extras","TemplateId","Count"})Check(hover.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr&&(string)i.Operand==field),"Kit guard/data missing: "+field);
var tooltip=helper.Methods.Single(m=>m.Name=="DrawKitTooltip");
Check(tooltip.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="Localized"),"Kit names must use game localization");
Check(tooltip.Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="extraCounts"),"Kit quantities missing");
Check(StackCheck.Verify(hover)>0&&StackCheck.Verify(tooltip)>0,"Tooltip stack invalid");
var update=integrated.MainModule.GetType("WeekendDropsUI.Plugin").Methods.Single(m=>m.Name=="Update");
var read=update.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.Name=="CurrentLootValue");
Check(read.Previous.OpCode==OpCodes.Brfalse&&read.Previous.Previous.Operand is MethodReference gate&&gate.Name=="ShouldSampleLoot","Raid inventory read is not gated");
Check(ReferenceEquals(read.Previous.Operand,read.Next.Next),"Throttle must skip the read and sample together");
var report=integrated.MainModule.GetType("WeekendDropsUI.Plugin").Methods.Single(m=>m.Name=="ReportRaidResult");
Check(report.Body.Instructions[0].Operand is MethodReference final&&final.Name=="CurrentLootValue","Extraction must take a fresh uncached reading");
Check(report.Body.Instructions[1].Operand is MethodReference sample&&sample.Name=="SampleLootValue","Extraction must store fresh sample before snapshot");
Check(!report.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="ShouldSampleLoot"),"Extraction must bypass cadence");
Console.WriteLine($"Passed: {passed}, Failed: 0, Skipped: 0. Actual UI/native method structure checked; no game rendering or delivery executed.");
