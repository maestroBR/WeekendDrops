using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Collections.Concurrent;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Commerce;
using WeekendDrops.Models;
using WeekendDrops.Services;
AssemblyLoadContext.Default.Resolving += (context, name) => {
 var path = Path.Combine(Environment.GetEnvironmentVariable("WEEKEND_GAME_ROOT") ?? throw new Exception("Set WEEKEND_GAME_ROOT"),"SPT_Runtime") + Path.DirectorySeparatorChar + name.Name + ".dll";
 return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"config"));
Checks.Run();
public class QuietLog : DispatchProxy {
 protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
 public static ISptLogger<T> For<T>() => Create<ISptLogger<T>,QuietLog>();
}
static class Checks {
 static int passed;
 static int mails;
 static void Assert(bool value,string message){if(!value)throw new Exception(message);passed++;}
 static void Field(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(obj,value);
 static bool MailPrefix(){Interlocked.Increment(ref mails);return false;}
 [MethodImpl(MethodImplOptions.NoInlining)]public static void Run(){
  if(!(Path.GetFullPath(WdPaths.ModDir).TrimEnd(Path.DirectorySeparatorChar) == Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar)))throw new Exception("Refusing to touch non-test data: "+WdPaths.ModDir);
  if(Directory.Exists(WdPaths.DataDir))Directory.Delete(WdPaths.DataDir,true);
  Directory.CreateDirectory(WdPaths.DataDir);
  var sunday = new DateTime(2026,10,11);
  foreach(var hours in new[]{(138,28),(55,104),(10,18),(18,10),(20,20)}){
   var cfg=new ModConfig{WeekendStartDay=hours.Item1/24,WeekendStartHour=hours.Item1%24,WeekendEndDay=hours.Item2/24,WeekendEndHour=hours.Item2%24};
   int duration=(hours.Item2-hours.Item1+168)%168;
   var expected=new HashSet<int>();for(int n=0;n<(duration==0?168:duration);n++)expected.Add((hours.Item1+n)%168);
   for(int h=0;h<168;h++){
    var now=sunday.AddHours(h).AddMinutes(30);
    Assert(WeekendWindow.IsActiveAt(cfg,now)==expected.Contains(h),"Incorrect window at "+now);
    var anchor=DateTime.Parse(WeekendWindow.CurrentIdAt(cfg,now)).AddHours(cfg.WeekendStartHour);
    Assert(anchor<=now&&anchor.AddDays(7)>now,"Period is not anchored to most recent opening");
   }
  }
  var normal=new ModConfig{WeekendStartDay=5,WeekendStartHour=18,WeekendEndDay=1,WeekendEndHour=4};
  Assert(!WeekendWindow.IsActiveAt(normal,new DateTime(2026,10,16,17,59,59)),"Opened too early");
  Assert(WeekendWindow.IsActiveAt(normal,new DateTime(2026,10,16,18,0,0)),"Start boundary excluded");
  Assert(!WeekendWindow.IsActiveAt(normal,new DateTime(2026,10,19,4,0,0)),"End boundary included");
  normal.DebugMode=true;Assert(WeekendWindow.IsActiveAt(normal,sunday),"Debug override lost");
  var extended=new NightMarketSchedule{StartHour=22,DurationHours=48,Days=[1],RerollDays=1};
  var after=new DateTime(2026,10,14,12,0,0);
  Assert(NightMarketWindow.IsOpen(extended,after),"48-hour market closed after first midnight");
  Assert(NightMarketWindow.SecondsUntilClose(extended,after)==36000,"Wrong extended close time");
  Assert(NightMarketWindow.CurrentPeriodId(extended,after)=="2026-10-12","Market rerolled inside window");
  Assert(!NightMarketWindow.IsOpen(extended,new DateTime(2026,10,14,22,0,0)),"Market end boundary included");
  var gp=new GpBalanceService(QuietLog.For<GpBalanceService>());
  var tc=new TarcoinBalanceService(QuietLog.For<TarcoinBalanceService>());
  gp.Add("a",100);tc.Add("a",100);
  Assert(!gp.TrySpend("a",-1)&&!tc.TrySpend("a",-1),"Negative cost accepted");
  gp.Add("rich",int.MaxValue);tc.Add("rich",int.MaxValue);
  bool overflow=false;try{gp.Add("rich",1);}catch(OverflowException){overflow=true;}
  Assert(overflow&&gp.Get("rich")==int.MaxValue,"GP overflow changed balance");
  overflow=false;try{tc.Add("rich",1);}catch(OverflowException){overflow=true;}
  Assert(overflow&&tc.Get("rich")==int.MaxValue,"TC overflow changed balance");
  Assert(!gp.TryTransfer("a","rich",1)&&gp.Get("a")==100,"Failed transfer debited sender");
  int wins=0;Parallel.For(0,30,_=>{if(gp.TrySpend("a",10))Interlocked.Increment(ref wins);});
  Assert(wins==10&&gp.Get("a")==0,"Concurrent spending bypassed funds");
  Assert(new GpBalanceService(QuietLog.For<GpBalanceService>()).Get("rich")==int.MaxValue,"GP reload differs");
  var path=Path.Combine(WdPaths.DataDir,"atomic.json");
  AtomicFile.WriteAllText(path,"{\"value\":0}");
  Parallel.For(1,50,i=>AtomicFile.WriteAllText(path,JsonSerializer.Serialize(new{value=i})));
  using(var json=JsonDocument.Parse(File.ReadAllText(path)))Assert(json.RootElement.GetProperty("value").GetInt32()>0,"Invalid atomic result");
  Assert(!Directory.EnumerateFiles(WdPaths.DataDir,"*.tmp").Any(),"Temporary files left behind");
  var directory=Path.Combine(WdPaths.DataDir,"occupied");Directory.CreateDirectory(directory);
  bool failed=false;try{AtomicFile.WriteAllText(directory,"replace directory");}catch(IOException){failed=true;}
  Assert(failed&&Directory.Exists(directory)&&!Directory.EnumerateFiles(WdPaths.DataDir,"*.tmp").Any(),"Failed replace damaged target or leaked temp file");
  var harmony=new Harmony("weekenddrops.local.regression");
  harmony.Patch(AccessTools.Method(typeof(MailSendService),"SendSystemMessageToPlayer"),prefix:new HarmonyMethod(typeof(Checks),nameof(MailPrefix)));
  var mail=(MailSendService)RuntimeHelpers.GetUninitializedObject(typeof(MailSendService));
  var weekend=(WeekendChallengeService)RuntimeHelpers.GetUninitializedObject(typeof(WeekendChallengeService));Field(weekend,"_config",new ModConfig());
  var market=new NightMarketService(weekend,tc,mail,null!,null!,QuietLog.For<NightMarketService>());
  Field(market,"_forceOpen",true);Field(market,"_config",new NightMarketConfig{CardCount=1,Pool=[new(){Id="sample",Name="Sample",TemplateId="5449016a4bdc2d6f028b456f",Cost=10,Weight=1}]});
  var sid=new MongoId().ToString();tc.Add(sid,100);market.Reveal(sid,0);
  var results=new ConcurrentBag<string>();Parallel.For(0,20,_=>results.Add(market.Buy(sid,0)));
  Assert(results.Count(x=>x=="ok")==1&&results.Count(x=>x=="already_bought")==19,"Same market card bought more than once");
  Assert(tc.Get(sid)==90&&mails==1,"Duplicate charge or mail");
  var persisted=JsonSerializer.Deserialize<NightMarketState>(File.ReadAllText(Path.Combine(WdPaths.DataDir,sid+"_nightmarket.json")))!;
  Assert(persisted.Cards[0].Bought,"Bought card did not persist");
  var daily = new DailyChallengeService(null!,mail,gp,null!,null!,QuietLog.For<DailyChallengeService>());
  Field(daily,"_shopItems",new List<ShopItemDefinition>{new(){Id="stock-one",Name="Single",TemplateId="5449016a4bdc2d6f028b456f",GpCost=1,Stock=1}});
  Field(daily,"_config",new ModConfig{ShopRestockHours=0});
  var buyer=new MongoId();gp.Add(buyer.ToString(),100);mails=0;results=new();
  Parallel.For(0,20,_=>results.Add(daily.BuyShopItem(buyer,"stock-one")));
  Assert(results.Count(x=>x=="ok")==1&&results.Count(x=>x=="out_of_stock")==19,"Global shop stock oversold");
  Assert(mails==1&&gp.Get(buyer.ToString())==99,"Global stock duplicate charge or mail");
  var invalid = new ShopItemDefinition{Id="invalid",TemplateId="bad",GpCost=10,Stock=1};
  Field(daily,"_shopItems",new List<ShopItemDefinition>{invalid});
  try{daily.BuyShopItem(buyer,"invalid");}catch(Exception){}
  Assert(gp.Get(buyer.ToString())==99,"Invalid delivery charged GP");
  var coins=(TarcoinLoot)RuntimeHelpers.GetUninitializedObject(typeof(TarcoinLoot));Field(coins,"_config",new TarcoinConfig{Enabled=false});
  var contracts=new ContractService(mail,gp,tc,coins,null!,null!,QuietLog.For<ContractService>());
  Field(contracts,"_config",new ContractsConfig{Contracts=[new(){Id="once",Map="customs",GpReward=0,RequireExtract=false}]});
  var owner=new MongoId();
  File.WriteAllText(Path.Combine(WdPaths.DataDir,owner+"_contracts.json"),JsonSerializer.Serialize(new PlayerContractState{ActiveContractId="once"}));
  results=new();Parallel.For(0,20,_=>results.Add(contracts.CompleteContract(owner,new(){ContractId="once",Location="bigmap"})));
  Assert(results.Count(x=>x=="ok")==1&&results.Count(x=>x=="not_active")==19,"Contract completed more than once");
  foreach(var filename in new[]{"gp_balances.json","tarcoin_balances.json"}){
   var balanceFile=Path.Combine(WdPaths.DataDir,filename);var valid=File.ReadAllText(balanceFile);File.WriteAllText(balanceFile,"{broken");
   bool rejected=false;try{if(filename.StartsWith("gp"))new GpBalanceService(QuietLog.For<GpBalanceService>());else new TarcoinBalanceService(QuietLog.For<TarcoinBalanceService>());}catch(InvalidDataException){rejected=true;}
   Assert(rejected&&File.ReadAllText(balanceFile)=="{broken","Unreadable balances silently reset");AtomicFile.WriteAllText(balanceFile,valid);
  }
  var marketFile=Path.Combine(WdPaths.DataDir,sid+"_nightmarket.json");File.WriteAllText(marketFile,"{broken");
  bool rejectedMarket=false;try{market.GetState(sid);}catch(InvalidDataException){rejectedMarket=true;}
  Assert(rejectedMarket&&File.ReadAllText(marketFile)=="{broken","Corrupt market rerolled bought cards");
  var loaderLog=QuietLog.For<WeekendDrops.WeekendDropsLoader>();
  var appliedBefore=Harmony.GetAllPatchedMethods().Count();
  WeekendDrops.Patches.ContractSpawnPatch.Apply(contracts,loaderLog);
  WeekendDrops.Patches.ContractBotLootPatch.Apply(contracts,loaderLog);
  WeekendDrops.Patches.WeekendXpPatch.Apply(new WeekendModifierService(QuietLog.For<WeekendModifierService>()),null!,loaderLog);
  WeekendDrops.Patches.LootContainerAmmoStackPatch.Apply(null!,loaderLog);
  WeekendDrops.Patches.CrateCompositionPatch.Apply(null!,loaderLog);
  WeekendDrops.Patches.WeaponKitPatch.Apply(null!,loaderLog);
  Assert(Harmony.GetAllPatchedMethods().Count()>=appliedBefore+5,"Server Harmony targets not installed in isolated process");
  var owners=Harmony.GetAllPatchedMethods().SelectMany(m=>Harmony.GetPatchInfo(m)!.Owners).ToHashSet();
  foreach(var ownerId in new[]{"com.20fpsguy.WeekendDrops.ContractSpawn","com.20fpsguy.WeekendDrops.ContractBotLoot","com.weekenddrops.xp","com.20fpsguy.WeekendDrops.AmmoStack","com.20fpsguy.WeekendDrops.CrateComposition","com.20fpsguy.WeekendDrops.WeaponKit"})Assert(owners.Contains(ownerId),"Missing server patch: "+ownerId);
  harmony.UnpatchSelf();
  Console.WriteLine($"Passed: {passed}, Failed: 0, Skipped: 0. Isolated local replay; no live profiles, routes or gameplay touched.");
 }
}
