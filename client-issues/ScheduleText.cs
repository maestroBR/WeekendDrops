using System;
using System.Text.RegularExpressions;
namespace WeekendDrops.LocalizationFixes {
 internal static class ScheduleText {
  static readonly string[] Days={"Sun","Mon","Tue","Wed","Thu","Fri","Sat"};
  static readonly Regex Range=new Regex(@"^(Sun|Mon|Tue|Wed|Thu|Fri|Sat) (\d{2}:\d{2}) to (Sun|Mon|Tue|Wed|Thu|Fri|Sat) (\d{2}:\d{2})$");
  internal static string Format(string input,Func<string,string,string> local){
   var m=Range.Match(input??"");if(!m.Success)return input;
   string start=local("ui.weekday."+Array.IndexOf(Days,m.Groups[1].Value),m.Groups[1].Value);
   string end=local("ui.weekday."+Array.IndexOf(Days,m.Groups[3].Value),m.Groups[3].Value);
   return string.Format(local("ui.scheduleRange","{0} {1} – {2} {3}"),start,m.Groups[2].Value,end,m.Groups[4].Value);
  }
 }
}