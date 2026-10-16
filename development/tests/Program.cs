using Path = System.IO.Path;
using System.Text.Json;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using WeekendDrops.Services;
AssemblyLoadContext.Default.Resolving+=(context,name)=>{
 var path=Path.Combine(Environment.GetEnvironmentVariable("WEEKEND_GAME_ROOT") ?? throw new Exception("Set WEEKEND_GAME_ROOT"),"SPT_Runtime") + Path.DirectorySeparatorChar+name.Name+".dll";return File.Exists(path)?context.LoadFromAssemblyPath(path):null;
};
Replay.Run();
static class Replay {
 [MethodImpl(MethodImplOptions.NoInlining)]public static void Run(){
 var root=Path.Combine(Environment.GetEnvironmentVariable("WEEKEND_GAME_ROOT") ?? throw new Exception("Set WEEKEND_GAME_ROOT"),"SPT_Runtime/SPT_Data/database") + Path.DirectorySeparatorChar;
 using var globals=JsonDocument.Parse(File.ReadAllText(root+"globals.json"));
 var json=new JsonSerializerOptions();json.Converters.Add(new SPTarkov.Server.Core.Utils.Json.Converters.StringToMongoIdConverter());
 var presets=globals.RootElement.GetProperty("ItemPresets").EnumerateObject().Select(p=>p.Value.Deserialize<Preset>(json)!).ToList();
 using var pool=JsonDocument.Parse(File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../config/night_market.json"))));
 var entries=pool.RootElement.GetProperty("pool").EnumerateArray().Where(x=>x.TryGetProperty("kit",out var k)&&k.GetBoolean()).ToList();
 var allIds=new HashSet<MongoId>();int checkedCount=0;
 foreach(var e in entries){
  string tpl=e.GetProperty("templateId").GetString()!;
  var preset=presets.FirstOrDefault(x=>x.Encyclopedia?.ToString()==tpl);
  if(preset==null)throw new Exception("Missing default preset "+tpl);
  var original=JsonSerializer.Serialize(preset.Items,json);
  for(int copy=0;copy<2;copy++){
   var cloned=JsonSerializer.Deserialize<List<Item>>(original,json)!;
   var result=NightMarketPresetTree.Prepare(cloned,new MongoId(tpl));
   if(result.Count<=1||result[0].Template.ToString()!=tpl||result[0].ParentId!=null||result[0].SlotId!="main")throw new Exception("Incomplete weapon "+tpl);
   var ids=result.Select(x=>x.Id).ToHashSet();
   if(result.Skip(1).Any(x=>x.ParentId==null||!ids.Contains(new MongoId(x.ParentId))))throw new Exception("Orphan attachment");
   foreach(var id in ids)if(!allIds.Add(id))throw new Exception("ID reused across deliveries");
  }
  if(JsonSerializer.Serialize(preset.Items,json)!=original)throw new Exception("Shared preset changed");
  checkedCount++;
 }
 bool rejected=false;try{NightMarketPresetTree.Prepare([],new MongoId("5447a9cd4bdc2dbd208b4567"));}catch(InvalidOperationException){rejected=true;}
 if(!rejected)throw new Exception("Empty preset accepted");
 var sample=presets.First(p=>p.Items.Count>2);
 foreach(var kind in new[]{"duplicate","wrongroot","cycle"}){
  var clone=JsonSerializer.Deserialize<List<Item>>(JsonSerializer.Serialize(sample.Items,json),json)!;
  var tpl=clone.First(x=>x.Id==sample.Parent).Template;
  if(kind=="duplicate")clone[1].Id=clone[0].Id;
  if(kind=="wrongroot")tpl=new MongoId("111111111111111111111111");
  if(kind=="cycle"){clone[1].ParentId=clone[2].Id.ToString();clone[2].ParentId=clone[1].Id.ToString();}
  bool bad=false;try{NightMarketPresetTree.Prepare(clone,tpl);}catch(InvalidOperationException){bad=true;}
  if(!bad)throw new Exception("Invalid preset accepted: "+kind);
 }
 Console.WriteLine($"PASS: {checkedCount} configured weapon presets, two independent deliveries each, all parents remapped, shared presets unchanged, invalid input rejected. Offline data; no profiles or gameplay.");
 }
}
