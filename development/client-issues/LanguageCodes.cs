using System;
using System.Collections.Generic;
namespace WeekendDrops.LocalizationFixes {
 internal static class LanguageCodes {
  internal static readonly string[] Supported={"en","ge","jp","ru","fr","kr","ch","es","pl","cz","it","po","hu","ro","sk","tu","es-mx"};
  static readonly HashSet<string> Known=new HashSet<string>(Supported,StringComparer.Ordinal);
  static readonly HashSet<string> LatinSpanish=new HashSet<string>(new[]{"419","mx","ar","bo","cl","co","cr","cu","do","ec","sv","gt","hn","ni","pa","py","pe","pr","uy","ve","us"},StringComparer.Ordinal);
  internal static string Resolve(string requested){
   var code=(requested??"en").Trim().Replace('_','-').ToLowerInvariant();
   if(Known.Contains(code))return code;
   var parts=code.Split('-');var main=parts[0];
   switch(main){
    case "de":return "ge";
    case "ja":return "jp";
    case "ko":return "kr";
    case "zh":return "ch";
    case "pt":return "po";
    case "cs":return "cz";
    case "tr":return "tu";
    case "es":return parts.Length>1&&LatinSpanish.Contains(parts[1])?"es-mx":"es";
   }
   return Known.Contains(main)?main:"en";
  }
 }
}
