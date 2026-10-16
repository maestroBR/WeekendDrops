using System;
using System.Reflection;
namespace WeekendDrops.LocalizationFixes {
 internal sealed class GameLanguageBridge {
  readonly MethodInfo instance,culture;
  internal GameLanguageBridge(Type manager){
   var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
   instance=manager.GetMethod("get_Instance",flags,null,Type.EmptyTypes,null);
   culture=manager.GetMethod("get_Culture",flags,null,Type.EmptyTypes,null);
   if(instance==null||!instance.IsStatic||instance.ReturnType!=manager||culture==null||culture.IsStatic||culture.ReturnType!=typeof(string))throw new MissingMemberException("EFT.LocalizationManager Instance/Culture contract");
  }
  internal string Read(){var target=instance.Invoke(null,null);return target==null?null:(string)culture.Invoke(target,null);}
 }
}
