using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Tailcop
{
    class Node
    {
        private static char _nextId = 'A';

        private readonly char _id;

        private readonly Instruction _first;

        private Instruction _last;

        private Func<EvalStack, EvalStack> _g;

        private readonly List<Node> _targets = new List<Node>();

        private readonly Dictionary<StackBehaviour, int> _stackOpCountMap; 

        private ImmutableHashSet<EvalStack> _I = ImmutableHashSet<EvalStack>.Empty;

        private ImmutableHashSet<EvalStack> _O = ImmutableHashSet<EvalStack>.Empty;

        private readonly MethodDefinition _method;

        public Node(MethodDefinition method, Instruction insn)
        {
            _method = method;
            _id = _nextId;
            _nextId = (char)(1 + _nextId);
            _first = insn;
            _stackOpCountMap = GetStackOpCountMap();
        }

        private Instruction GetLastInstructionInBlock(Instruction first, List<Instruction> jumpTargets)
        {
            Instruction last = null;
            var insn = first;
            while (last == null)
            {
                if (insn == null)
                {
                    throw new NullReferenceException("insn was null. this should never happen.");
                }
                if (IsBlockTerminating(insn) ||
                    insn.Next == null ||
                    jumpTargets.Contains(insn.Next))
                {
                    last = insn;
                }
                insn = insn.Next;
            }

            return last;
        }

        private bool IsBlockTerminating(Instruction insn)
        {
            var fc = insn.OpCode.FlowControl;
            return fc == FlowControl.Branch || fc == FlowControl.Cond_Branch || fc == FlowControl.Return;
        }

        private Func<EvalStack, EvalStack> CreateG()
        {
            return CreateComposite(_first, _last);
        }

        public Func<EvalStack, EvalStack> CreateH(Instruction call)
        {
            return CreateComposite(_first, call.Previous);
        }

        private Func<EvalStack, EvalStack> CreateComposite(Instruction first, Instruction last)
        {
            Instruction insn = first;
            Func<EvalStack, EvalStack> fun = stack => stack;
            while (insn != null)
            {
                var f = CreateF(insn);
                var fresh = fun;
                fun = stack => f(fresh(stack));
                insn = (insn == last) ? null : insn.Next;
            }

            return fun;
        }

        public ImmutableHashSet<EvalStack> GetPossibleStacksAt(Instruction call)
        {
            var h = CreateH(call);
            return _I.Select(h).ToImmutableHashSet();
        } 

        private Func<EvalStack, EvalStack> CreateF(Instruction insn)
        {
            var op = insn.OpCode;
            if (op == OpCodes.Ldarg_0)
            {
                // Prototypical *this* producer!!
                return stack => stack.Push(true);
            }
 
            return CreateConsumerF(insn);
        }

        public Func<EvalStack, EvalStack> CreateConsumerF(Instruction insn) {    
		    var op = insn.OpCode;
            if (op == OpCodes.Leave || op == OpCodes.Leave_S)
            {
                return stack => new EvalStack();
            }

            if (op == OpCodes.Ret)
            {
                bool isVoid = "System.Void".Equals(_method.ReturnType.FullName);
                if (isVoid)
                {
                    return stack => stack;
                }
                return stack => stack.Pop();
            }

            if (op == OpCodes.Newobj)
            {
                return CreateConstructorConsumerF(insn);
            }

            if (IsCallInstruction(op))
            {
                return CreateCallingConsumerF(insn);
            }

		    int pops = GetPopCount(op.StackBehaviourPop);
		    int pushes = GetPushCount(op.StackBehaviourPush);
		    return CreateConsumerF(pops, pushes);
	    }

        private Func<EvalStack, EvalStack> CreateConstructorConsumerF(Instruction insn)
        {
            var ctor = (MethodReference)insn.Operand;
            return CreateConsumerF(ctor.Parameters.Count, 1);
        }

        private int GetPushCount(StackBehaviour sb)
        {
            return _stackOpCountMap[sb];
        }

        private bool IsCallInstruction(OpCode op)
        {
            return op == OpCodes.Call || op == OpCodes.Calli || op == OpCodes.Callvirt || op == OpCodes.Newobj;
        }

        private int GetPopCount(StackBehaviour sb)
        {
            return _stackOpCountMap[sb];
        }

        public Func<EvalStack, EvalStack> CreateCallingConsumerF(Instruction insn)
        {
            var target = (MethodReference) insn.Operand;
            int pops = GetPopCountForCall(target);
            int pushes = GetPushCountForCall(insn.OpCode, target);
            return CreateConsumerF(pops, pushes);
        }

        public int GetPopCountForCall(MethodReference target)
        {
            var result = target.Parameters.Count + (target.HasThis ? 1 : 0);
            return result;
        }

        public int GetPushCountForCall(OpCode op, MethodReference target)
        {
            if (op == OpCodes.Newobj)
            {
                return 1;
            }

            return target.ReturnType.FullName.Equals("System.Void") ? 0 : 1;
        }

        private Dictionary<StackBehaviour, int> GetStackOpCountMap()
        {
            var result = new Dictionary<StackBehaviour, int>
                {
                    { StackBehaviour.Pop0, 0 },
                    { StackBehaviour.Pop1, 1 },
                    { StackBehaviour.Pop1_pop1, 2 },
                    { StackBehaviour.Popi, 1 },
                    { StackBehaviour.Popi_pop1, 2 },
                    { StackBehaviour.Popi_popi, 2 },
                    { StackBehaviour.Popi_popi8, 2 },
                    { StackBehaviour.Popi_popi_popi, 3 },
                    { StackBehaviour.Popi_popr4, 2 },
                    { StackBehaviour.Popi_popr8, 2 },
                    { StackBehaviour.Popref, 1 },
                    { StackBehaviour.Popref_pop1, 2 },
                    { StackBehaviour.Popref_popi, 2 },
                    { StackBehaviour.Popref_popi_popi, 3 },
                    { StackBehaviour.Popref_popi_popi8, 3 },
                    { StackBehaviour.Popref_popi_popr4, 3 },
                    { StackBehaviour.Popref_popi_popr8, 3 },
                    { StackBehaviour.Popref_popi_popref, 3 },
                    // { StackBehavour.PopAll, 0 },
                    { StackBehaviour.Push0, 0 },
                    { StackBehaviour.Push1, 1 },
                    { StackBehaviour.Push1_push1, 2 },
                    { StackBehaviour.Pushi, 1 },
                    { StackBehaviour.Pushi8, 1 },
                    { StackBehaviour.Pushr4, 1 },
                    { StackBehaviour.Pushr8, 1 },
                    { StackBehaviour.Pushref, 1 },
                    // { StackBehavour.Varpop, 0 },
                    // { StackBehavour.Varpush, 0 },				
                };
            return result;
        }

        public Func<EvalStack, EvalStack> CreateConsumerF(int pops, int pushes)
        {
            Func<EvalStack, EvalStack> pop = stack => stack.Pop();
            Func<EvalStack, EvalStack> push = stack => stack.Push(false);
            Func<EvalStack, EvalStack> result = stack => stack;
            for (int i = 0; i < pops; i++)
            {
                var fresh = result;
                result = stack => pop(fresh(stack));
            }
            for (int i = 0; i < pushes; i++)
            {
                var fresh = result;
                result = stack => push(fresh(stack));
            }
            return result;
        }


        public Func<EvalStack, EvalStack> G
        {
            get { return _g; }
        }

        private bool IsBranching(Instruction insn)
        {
            var ot = insn.OpCode.OperandType;
            return ot == OperandType.InlineBrTarget ||
                ot == OperandType.ShortInlineBrTarget ||
                ot == OperandType.InlineSwitch;
        }

        private bool IsReturn(Instruction insn)
        {
            return insn.OpCode == OpCodes.Ret;
        }

        public void AddEdges(Dictionary<Instruction, Node> map)
        {
            var insn = _first;
            while (!IsBranching(insn))
            {
                insn = insn.Next;
                if (insn == null || IsReturn(insn)) { return; }
            }

            var ot = insn.OpCode.OperandType;
            var lbls = new List<Instruction>();
            if (ot == OperandType.InlineSwitch)
            {
                lbls.AddRange((Instruction[])insn.Operand);
            }
            else
            {
                lbls.Add((Instruction)insn.Operand);
                if (FlowControl.Cond_Branch == insn.OpCode.FlowControl)
                {
                    lbls.Add(insn.Next);
                }
            }

            foreach (var lbl in lbls)
            {
                _targets.Add(map[lbl]);
            }
        }

        public void Process(ImmutableHashSet<EvalStack> set)
        {
            if (G == null)
            {
                throw new NullReferenceException("Need to init G.");
            }

            var x = set.Except(_I);
            if (!x.IsEmpty)
            {
                _I = _I.Union(x);
                _O = _I.Select(s => G(s)).ToImmutableHashSet();
                foreach (var n in _targets)
                {
                    n.Process(_O);
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(); 
            sb.AppendLine("NODE " + _id).AppendLine(" > Instructions");
            var insn = _first;
            if (_last == null)
            {
                throw new NullReferenceException("Skip instructions until _last is set.");
            }
            while (insn != null)
            {
                sb.AppendLine("   " + insn.OpCode.Name);
 
                if (insn == _last)
                {
                    break;
                }
                insn = insn.Next;
            }
            sb.Append(" > I" + Environment.NewLine);
            foreach (var s in _I)
            {
                sb.AppendLine("   " + s);
            }
            sb.Append(" > O" + Environment.NewLine);
            foreach (var s in _O)
            {
                sb.Append("   " + s + Environment.NewLine);
            }
            sb.Append(" > Targets" + Environment.NewLine);
            foreach (var t in _targets)
            {
                sb.Append("   " + t._id + Environment.NewLine);
            }
            return sb.ToString();
        }

        public void InitG(List<Instruction> jumpTargets)
        {
            _last = GetLastInstructionInBlock(_first, jumpTargets);
            _g = CreateG();
        }
    }
}
