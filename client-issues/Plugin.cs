using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
namespace WeekendDrops.LocalizationFixes {
 [BepInPlugin("project.brainiac.weekendlocalizationfixes","Weekend Drops Localization Fixes","1.0.1")]
 [BepInDependency("com.20fpsguy.WeekendDrops")]
 public sealed class Plugin:BaseUnityPlugin {
  static readonly Dictionary<string,Dictionary<string,string>> translations=new Dictionary<string,Dictionary<string,string>>();
  static PropertyInfo language;static MethodInfo localize, invalidate;static GameLanguageBridge gameLanguage;Harmony harmony;
  void Awake(){try{
   var folder=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
   foreach(var code in LanguageCodes.Supported)translations[code]=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,"locales",code+".json")));
   var loc=AccessTools.TypeByName("WeekendDropsUI.Services.Loc");language=AccessTools.Property(loc,"Language");localize=AccessTools.Method(loc,"Or",new[]{typeof(string),typeof(string)});
   if(language==null||localize==null)throw new MissingMemberException("Weekend localization API");
   var manager=typeof(EFT.Player).Assembly.GetType("EFT.LocalizationManager",true);
   gameLanguage=new GameLanguageBridge(manager);
   invalidate=AccessTools.Method(loc,"Invalidate",Type.EmptyTypes);
   var auto=AccessTools.Method(loc,"GameLanguage",Type.EmptyTypes);
   var change=AccessTools.Method(manager,"set_Culture",new[]{typeof(string)});
   if(auto==null||invalidate==null||change==null)throw new MissingMemberException("Weekend auto language API");
   harmony=new Harmony("project.brainiac.weekendlocalizationfixes");harmony.Patch(auto,prefix:new HarmonyMethod(typeof(Plugin),nameof(AutoLanguage)));
   harmony.Patch(change,postfix:new HarmonyMethod(typeof(Plugin),nameof(LanguageChanged)));
   harmony.Patch(localize,postfix:new HarmonyMethod(typeof(Plugin),nameof(Text)));
   harmony.Patch(AccessTools.Method(loc,"T",new[]{typeof(string),typeof(object[])}),prefix:new HarmonyMethod(typeof(Plugin),nameof(Schedule)));
   Logger.LogInfo("Weekend auto language reads EFT.LocalizationManager.Culture; game language changes invalidate cached translations. Shop/schedule fixes active.");
  }catch(Exception e){harmony?.UnpatchSelf();Logger.LogError(e);}}
  static bool AutoLanguage(ref string __result){var code=gameLanguage.Read();if(string.IsNullOrWhiteSpace(code))return true;__result=code;return false;}
  static void LanguageChanged()=>invalidate.Invoke(null,null);
  static string Code()=>LanguageCodes.Resolve((string)language.GetValue(null,null));
  static void Text(string __0,ref string __result){string value;if(__0!=null&&translations[Code()].TryGetValue(__0,out value))__result=value;}
  static string Local(string key,string fallback)=>(string)localize.Invoke(null,new object[]{key,fallback});
  static void Schedule(string __0,ref object[] __1){
   if(__0!="ui.noWeekendSub"||__1==null||__1.Length!=1||!(__1[0] is string schedule))return;
   var args=(object[])__1.Clone();args[0]=ScheduleText.Format(schedule,Local);__1=args;
  }
  void OnDestroy()=>harmony?.UnpatchSelf();
 }
}