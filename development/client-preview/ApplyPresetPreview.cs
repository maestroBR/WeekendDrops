using Mono.Cecil;
using Mono.Cecil.Cil;

// Build-time integration into the original UI; this is not a runtime plugin.
internal static class ApplyPresetPreview
{
 public static MethodDefinition Apply(ModuleDefinition ui,ModuleDefinition game)
 {
  var factory=game.GetType("EFT.ItemFactory");
  var preset=factory.Methods.Single(m=>m.Name=="GetPresetItem"&&m.Parameters.Count==1&&m.Parameters[0].ParameterType.FullName=="System.String");
  var cache=ui.GetType("WeekendDropsUI.Services.WdItemIconCache");
  var load=cache.NestedTypes.Single(t=>t.Name.StartsWith("<Load>",StringComparison.Ordinal)).Methods.Single(m=>m.Name=="MoveNext");
  var create=load.Body.Instructions.Single(i=>i.Operand is MethodReference m&&m.DeclaringType.FullName=="EFT.ItemFactory"&&m.Name=="CreateItem");
  // Original stack: factory, random preview ID, template ID, null update data.
  // GetPresetItem consumes factory + template ID and clones the native complete preset.
  var nullData=create.Previous;var template=nullData.Previous;var owner=template.Previous;var randomId=owner.Previous;
  if(nullData.OpCode!=OpCodes.Ldnull||template.OpCode!=OpCodes.Ldfld||template.Operand is not FieldReference f||f.Name!="templateId"||owner.OpCode!=OpCodes.Ldarg_0||randomId.OpCode!=OpCodes.Ldloc_S)
   throw new InvalidOperationException("Icon creation structure changed; review before integrating preset previews.");
  randomId.OpCode=OpCodes.Nop;randomId.Operand=null;
  nullData.OpCode=OpCodes.Nop;nullData.Operand=null;
  create.Operand=ui.ImportReference(preset);
  return load;
 }
}
