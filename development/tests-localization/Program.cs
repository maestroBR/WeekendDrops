using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WeekendDrops.Services;
using WeekendDrops.Models;
using WeekendDrops.LocalizationFixes;
var cultures=new Dictionary<string,string>{{"en","en-US"},{"ge","de-DE"},{"jp","ja-JP"},{"ru","ru-RU"},{"fr","fr-FR"},{"kr","ko-KR"},{"ch","zh-CN"},{"es","es-ES"},{"pl","pl-PL"},{"cz","cs-CZ"},{"it","it-IT"},{"po","pt-BR"},{"hu","hu-HU"},{"ro","ro-RO"},{"sk","sk-SK"},{"tu","tr-TR"},{"es-mx","es-MX"}};
if(args.Contains("--culture-days")){
 Console.WriteLine(JsonSerializer.Serialize(cultures.ToDictionary(x=>x.Key,x=>CultureInfo.GetCultureInfo(x.Value).DateTimeFormat.AbbreviatedDayNames)));
 return;
}
var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../client-issues/locales"));
foreach(var culture in new[]{"en-US","pt-BR","ru-RU","de-DE"}){
 CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);
 if(WeekendWindow.ScheduleText(new(){WeekendStartDay=5,WeekendStartHour=18,WeekendEndDay=1,WeekendEndHour=4})!="Fri 18:00 to Mon 04:00")throw new Exception("Server culture leaked");
}
foreach(var code in LanguageCodes.Supported){
 var translations=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(folder,code+".json")))!;
 var culturalDays=CultureInfo.GetCultureInfo(cultures[code]).DateTimeFormat.AbbreviatedDayNames;
 for(var day=0;day<7;day++)
  if(translations["ui.weekday."+day]!=culturalDays[day])throw new Exception("Weekday index/culture mismatch: "+code+" day "+day);
 string Local(string key,string fallback)=>translations.TryGetValue(key,out var text)?text:fallback;
 var result=ScheduleText.Format("Fri 18:00 to Mon 04:00",Local);
 if(!result.Contains("18:00")||!result.Contains("04:00"))throw new Exception("Hour changed");
 if(!result.Contains(translations["ui.weekday.5"])||!result.Contains(translations["ui.weekday.1"]))throw new Exception("Localized weekdays missing");
 var custom=WeekendWindow.ScheduleText(new(){WeekendStartDay=2,WeekendStartHour=7,WeekendEndDay=6,WeekendEndHour=23});
 if(!ScheduleText.Format(custom,Local).Contains("07:00"))throw new Exception("Configured schedule replaced");
 foreach(var id in new[]{"handover_medical","handover_ammo","handover_provisions","handover_grenades"})
  if(string.IsNullOrWhiteSpace(Local("shop."+id+".name",""))||string.IsNullOrWhiteSpace(Local("shop."+id+".desc","")))throw new Exception("Missing kit translation");
 if(ScheduleText.Format("unrecognized schedule",Local)!="unrecognized schedule")throw new Exception("Unknown text modified");
 Console.WriteLine(code+": "+result);
}
var fullFolder=Path.GetFullPath(Path.Combine(folder,"../../localization/locales"));
var baseline=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Path.Combine(fullFolder,"en.json")))!;
var formatted=0;
foreach(var code in LanguageCodes.Supported){
 var texts=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Path.Combine(fullFolder,code+".json")))!;
 foreach(var pair in baseline){
  if(pair.Value.ValueKind!=JsonValueKind.String)continue;
  var original=pair.Value.GetString()!;
  var matches=Regex.Matches(original,@"\{(\d+)(?:[^}]*)\}");
  if(matches.Count==0)continue;
  var arguments=Enumerable.Repeat<object>(0d,matches.Max(x=>int.Parse(x.Groups[1].Value))+1).ToArray();
  try{string.Format(CultureInfo.InvariantCulture,original,arguments);}catch(FormatException){continue;}
  try{string.Format(CultureInfo.InvariantCulture,texts[pair.Key].GetString()!,arguments);formatted++;}
  catch(FormatException){throw new Exception("Runtime format failure: "+code+" / "+pair.Key);}
 }
}
Console.WriteLine("Runtime numeric format checks passed: "+formatted);
foreach(var code in LanguageCodes.Supported)
 if(LanguageCodes.Resolve(code)!=code)throw new Exception("Native locale not selected: "+code);
foreach(var alias in new Dictionary<string,string>{{"de-DE","ge"},{"ja-JP","jp"},{"ko-KR","kr"},{"cs-CZ","cz"},{"tr-TR","tu"},{"pt-BR","po"},{"zh-CN","ch"},{"es-419","es-mx"},{"es-AR","es-mx"},{"es-ES","es"},{"es-AD","es"},{"es-XX","es"},{"es-US","es-mx"},{"en-US","en"},{"ru-RU","ru"},{"fr-FR","fr"},{"it-IT","it"},{"pl-PL","pl"},{"hu-HU","hu"},{"ro-RO","ro"},{"sk-SK","sk"},{"ja_JP","jp"},{"unknown","en"}})
 if(LanguageCodes.Resolve(alias.Key)!=alias.Value)throw new Exception("Alias mismatch: "+alias.Key);
if(LanguageCodes.Resolve(null)!="en"||LanguageCodes.Resolve("")!="en")throw new Exception("Empty-language fallback");
Console.WriteLine("PASS: invariant server schedule, 17 client languages, configured times, package keys and unknown-text fallback.");
var bridge=new GameLanguageBridge(typeof(FakeGameLocale));
foreach(var code in new[]{"po","en","ru","es-mx"}){FakeGameLocale.Instance.Change(code);if(bridge.Read()!=code)throw new Exception("Auto game language mismatch: "+code);}
FakeGameLocale.Instance.Change("po");if(LanguageCodes.Resolve(bridge.Read())!="po")throw new Exception("Portuguese auto fallback");
Console.WriteLine("PASS: Auto reads selected game culture, including po and runtime language changes.");
namespace WeekendDrops.Models{public class ModConfig{public bool DebugMode{get;set;}public int WeekendStartDay{get;set;}public int WeekendStartHour{get;set;}public int WeekendEndDay{get;set;}public int WeekendEndHour{get;set;}}}

internal sealed class FakeGameLocale {
 public static FakeGameLocale Instance{get;}=new();
 private string Culture{get;set;}="en";
 public void Change(string code)=>Culture=code;
}
