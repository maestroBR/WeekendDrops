using WeekendDropsUI.Services;
using EFT.UI;
using Comfort.Common;
int passed=0;
void Check(bool good,string message){if(!good)throw new Exception(message);passed++;}
var log=new BepInEx.Logging.ManualLogSource();NativeAudio.Initialize(log);
Singleton<GUISounds>.Instance=new GUISounds();Singleton<GUISounds>.Instantiated=true;
void Cue(Action action,EUISoundType expected){var start=GUISounds.Played.Count;action();Check(GUISounds.Played.Count==start+1&&GUISounds.Played.Last()==expected,"Wrong cue: "+expected);}
foreach(var alias in new Dictionary<string,EUISoundType>{{"QuestTurnedIn",EUISoundType.QuestFinished},{"QuestUpdated",EUISoundType.QuestSubTrackComplete},{"TaskMarkerFound",EUISoundType.QuestSubTrackComplete},{"InsuranceItemReturnedToStash",EUISoundType.TradeOperationComplete},{"Ready",EUISoundType.TradeOperationComplete},{"TabButton",EUISoundType.ButtonBottomBarClick},{"MenuEscape",EUISoundType.MenuInspectorWindowClose}})Cue(()=>NativeAudio.Play(new[]{alias.Key,"ButtonClick"}),alias.Value);
Cue(()=>NativeAudio.Play(new[]{"unknown","ButtonClick"}),EUISoundType.ButtonClick);
Cue(()=>NativeAudio.Play(new[]{"buttonclick"}),EUISoundType.ButtonClick);
Cue(NativeAudio.Open,EUISoundType.MenuInspectorWindowOpen);
Cue(()=>NativeAudio.Click(new[]{"QuestTurnedIn"}),EUISoundType.ButtonClick);
Cue(()=>NativeAudio.RewardResult(true),EUISoundType.QuestFinished);
Cue(()=>NativeAudio.RewardResult(false),EUISoundType.ErrorMessage);
Cue(()=>NativeAudio.RewardTask(Task.FromResult(true)),EUISoundType.QuestFinished);
Cue(()=>NativeAudio.RewardTask(Task.FromResult(false)),EUISoundType.ErrorMessage);
Cue(()=>NativeAudio.CardResult("ok",false),EUISoundType.TradeOperationComplete);
Cue(()=>NativeAudio.CardResult("ok",true),EUISoundType.MenuDropdownSelect);
foreach(var result in new[]{"insufficient_tarcoin","already_bought","closed","failed","",null})Cue(()=>NativeAudio.CardResult(result,false),EUISoundType.ErrorMessage);
int before=GUISounds.Played.Count;
NativeAudio.Play(null);NativeAudio.Play(Array.Empty<string>());NativeAudio.Play(new[]{"unknown","999","-1",null});
NativeAudio.RewardTask(null);NativeAudio.RewardTask(new TaskCompletionSource<bool>().Task);NativeAudio.RewardTask(Task.FromCanceled<bool>(new CancellationToken(true)));NativeAudio.RewardTask(Task.FromException<bool>(new Exception("test fault")));
Check(GUISounds.Played.Count==before,"Invalid or pending request played a success cue");
Singleton<GUISounds>.Instantiated=false;NativeAudio.Open();Check(GUISounds.Played.Count==before,"Missing native singleton played sound");Singleton<GUISounds>.Instantiated=true;
GUISounds.Fail=true;NativeAudio.Open();NativeAudio.Open();Check(log.Warnings==1,"Native failure should warn once without breaking UI");GUISounds.Fail=false;
Cue(NativeAudio.Open,EUISoundType.MenuInspectorWindowOpen);
Check(log.Info==1,"Initialization was not recorded");
Console.WriteLine($"Passed: {passed}, Failed: 0, Skipped: 0. Controlled native-audio stub; audibility in Tarkov remains unverified.");
namespace EFT.UI {
 public enum EUISoundType{BackpackClose,BackpackOpen,ButtonBottomBarClick,ButtonClick,ErrorMessage,MenuDropdownSelect,MenuEscape,MenuInspectorWindowClose,MenuInspectorWindowOpen,QuestFinished,QuestSubTrackComplete,TradeOperationComplete}
 public class GUISounds{public static readonly List<EUISoundType> Played=new();public static bool Fail;public void PlayUISound(EUISoundType sound){if(Fail)throw new InvalidOperationException();Played.Add(sound);}}
}
namespace Comfort.Common{public static class Singleton<T>{public static bool Instantiated;public static T Instance;}}
namespace BepInEx.Logging{public class ManualLogSource{public int Warnings;public int Info;public void LogInfo(object value){Info++;}public void LogWarning(object value){Warnings++;}}}
