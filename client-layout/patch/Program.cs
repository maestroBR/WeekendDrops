using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.Json;
var source=args[0];var output=args[1];var helper=args[2];
using var asm=AssemblyDefinition.ReadAssembly(source);
using var support=AssemblyDefinition.ReadAssembly(helper);
var wrapper=asm.MainModule.ImportReference(support.MainModule.Types.Single(t=>t.Name=="TextLayout").Methods.Single(m=>m.Name=="Label"));
IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts){foreach(var t in ts){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
var types=All(asm.MainModule.Types).ToArray();var panel=types.Single(t=>t.FullName=="WeekendDropsUI.UI.ChallengePanel");var changes=new List<object>();
void Const(string method,int offset,float before,float after){var m=panel.Methods.Single(m=>m.Name==method);var i=m.Body.Instructions.Single(i=>i.Offset==offset);if(i.OpCode!=OpCodes.Ldc_R4 || (float)i.Operand!=before)throw new Exception("Unexpected layout IL "+method+" "+offset);i.Operand=after;changes.Add(new{method,offset,before,after});}
Const("DrawShopCard",0x03b1,20,40);Const("DrawShopCard",0x03de,86,108);Const("DrawShopCard",0x03eb,18,32);
foreach(var i in panel.Methods.Single(m=>m.Name=="DrawShopContent").Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldc_R4 && ((float)i.Operand==164 || (float)i.Operand==176))){var before=(float)i.Operand;i.Operand=before+36;changes.Add(new{method="DrawShopContent",offset=i.Offset,before,after=before+36});}
Const("DrawBuyFooter",0x0030,108,144);Const("DrawBuyFooter",0x008a,106,142);Const("DrawBarterFooter",0x0009,104,140);
int labels=0;var labelMethods=new List<string>();
foreach(var t in types.Where(t=>t.Namespace=="WeekendDropsUI.UI"))foreach(var m in t.Methods.Where(m=>m.HasBody)){
 int count=0;
 foreach(var i in m.Body.Instructions)if(i.OpCode==OpCodes.Call && i.Operand is MethodReference mr && mr.FullName=="System.Void UnityEngine.GUI::Label(UnityEngine.Rect,System.String,UnityEngine.GUIStyle)"){i.Operand=wrapper;labels++;count++;}
 if(count>0)labelMethods.Add(m.FullName);
}
if(labels<10)throw new Exception("Too few labels");asm.Write(output);using var check=AssemblyDefinition.ReadAssembly(output);if(check.Name.FullName!=asm.Name.FullName || check.MainModule.Mvid!=asm.MainModule.Mvid)throw new Exception("Identity changed");
using var original=AssemblyDefinition.ReadAssembly(source);
var beforeMethods=All(original.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToDictionary(m=>m.FullName);
var afterMethods=All(check.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).ToDictionary(m=>m.FullName);
if(beforeMethods.Count!=afterMethods.Count)throw new Exception("Method set changed");
int differences=0;
foreach(var pair in beforeMethods){var a=pair.Value.Body.Instructions;var b=afterMethods[pair.Key].Body.Instructions;if(a.Count!=b.Count)throw new Exception("Instruction count changed");for(int n=0;n<a.Count;n++)if(a[n].ToString()!=b[n].ToString()){differences++;bool label=a[n].Operand is MethodReference mr && mr.FullName=="System.Void UnityEngine.GUI::Label(UnityEngine.Rect,System.String,UnityEngine.GUIStyle)" && b[n].Operand is MethodReference nr && nr.FullName==wrapper.FullName;bool geometry=pair.Value.DeclaringType==original.MainModule.Types.Single(t=>t.FullName=="WeekendDropsUI.UI.ChallengePanel") && new[]{"DrawShopCard","DrawShopContent","DrawBuyFooter","DrawBarterFooter"}.Contains(pair.Value.Name) && a[n].OpCode==OpCodes.Ldc_R4 && b[n].OpCode==OpCodes.Ldc_R4;if(!label&&!geometry)throw new Exception("Unexpected IL change "+pair.Key);}}
if(differences!=labels+changes.Count)throw new Exception("Diff mismatch");
Console.WriteLine("PASS: "+beforeMethods.Count+" method bodies checked; only "+differences+" authorized IL operands changed.");
File.WriteAllText(args[3],JsonSerializer.Serialize(new{labels,labelMethods,changes,identity=check.Name.FullName,mvid=check.MainModule.Mvid},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Patched "+labels+" labels in "+labelMethods.Count+" methods, "+changes.Count+" geometry constants.");
