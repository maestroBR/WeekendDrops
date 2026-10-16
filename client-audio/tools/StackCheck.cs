using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class StackCheck
{
 public static int Verify(MethodDefinition method)
 {
  if(!method.HasBody)return 0;
  var stack=new Dictionary<Instruction,int>();var work=new Queue<(Instruction,int)>();int max=0;
  void Add(Instruction? instruction,int depth){if(instruction==null)return;if(stack.TryGetValue(instruction,out int known)){if(known!=depth)throw new InvalidOperationException("Stack merge mismatch in "+method.FullName+" at "+instruction);return;}stack[instruction]=depth;work.Enqueue((instruction,depth));}
  Add(method.Body.Instructions.FirstOrDefault(),0);
  foreach(var h in method.Body.ExceptionHandlers){Add(h.HandlerStart,h.HandlerType==ExceptionHandlerType.Catch||h.HandlerType==ExceptionHandlerType.Filter?1:0);Add(h.FilterStart,1);}
  while(work.Count>0){
   var (i,depth)=work.Dequeue();
   if(i.OpCode==OpCodes.Leave||i.OpCode==OpCodes.Leave_S){Add((Instruction)i.Operand,0);continue;}
   int pop,push;
   if(i.OpCode.StackBehaviourPop==StackBehaviour.Varpop){
    if(i.OpCode==OpCodes.Ret)pop=method.ReturnType.FullName=="System.Void"?0:1;
    else if(i.Operand is MethodReference m)pop=m.Parameters.Count+(m.HasThis&&i.OpCode!=OpCodes.Newobj?1:0);
    else throw new InvalidOperationException("Unsupported variable pop: "+i);
   }else pop=Count(i.OpCode.StackBehaviourPop);
   if(depth<pop)throw new InvalidOperationException("Stack underflow in "+method.FullName+" at "+i);
   if(i.OpCode.StackBehaviourPush==StackBehaviour.Varpush){var call=(MethodReference)i.Operand;push=call.ReturnType.FullName=="System.Void"?0:1;}
   else push=Count(i.OpCode.StackBehaviourPush);
   int after=depth-pop+push;max=Math.Max(max,Math.Max(depth,after));
   if(i.OpCode==OpCodes.Leave||i.OpCode==OpCodes.Leave_S){Add((Instruction)i.Operand,0);continue;}
   if(i.OpCode.FlowControl is FlowControl.Return or FlowControl.Throw){if(after!=0)throw new InvalidOperationException("Nonempty terminal stack in "+method.FullName+" at "+i);continue;}
   if(i.Operand is Instruction branch){Add(branch,after);if(i.OpCode.FlowControl==FlowControl.Branch)continue;}
   if(i.Operand is Instruction[] branches)foreach(var b in branches)Add(b,after);
   Add(i.Next,after);
  }
  method.Body.MaxStackSize=Math.Max(method.Body.MaxStackSize,max);return stack.Count;
 }
 static int Count(StackBehaviour behavior)
 {
  if(behavior is StackBehaviour.Pop0 or StackBehaviour.Push0)return 0;
  if(behavior is StackBehaviour.Varpop or StackBehaviour.Varpush)throw new InvalidOperationException("Unresolved variable stack behavior");
  return behavior.ToString().Split('_').Length;
 }
}
