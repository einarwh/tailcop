using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Tailcop
{
    class Tailcopper
    {
        private readonly string _assemblyOutputFileName;

        public Tailcopper() : this("Copped.exe")
        {
        }

        public Tailcopper(string assemblyOutputFileName)
        {
            _assemblyOutputFileName = assemblyOutputFileName;
        }

        public bool Rewrite(string assemblyPath)
        {
            var assembly = ReadAssembly(assemblyPath);
            bool result = Rewrite(assembly);
            if (result)
            {
                assembly.Write(_assemblyOutputFileName);
            }
            return result;
        }

        private static AssemblyDefinition ReadAssembly(string assemblyPath)
        {
            var resolver = new DefaultAssemblyResolver();

            var assembly = AssemblyDefinition.ReadAssembly(
                assemblyPath,
                new ReaderParameters { AssemblyResolver = resolver });
            return assembly;
        }

        private Dictionary<MethodDefinition, IEnumerable<Instruction>> FindStaticTailCalls(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Types
                .SelectMany(t => t.Methods)
                .Select(m => Tuple.Create(m, FindStaticTailCalls(m)))
                .Where(tp => tp.Item2.Any())
                .GroupBy(tp => tp.Item1, tp => tp.Item2)
                .ToDictionary(g => g.Key, g => g.SelectMany(it => it));
        }

        private Dictionary<MethodDefinition, IEnumerable<Instruction>> FindInstanceTailCalls(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Types
                .SelectMany(t => t.Methods)
                .Select(m => Tuple.Create(m, FindInstanceTailCalls(m)))
                .Where(tp => tp.Item2.Any())
                .GroupBy(tp => tp.Item1, tp => tp.Item2)
                .ToDictionary(g => g.Key, g => g.SelectMany(it => it));
        }

        private IList<Instruction> FindStaticTailCalls(MethodDefinition method) 
        {
            var calls = new List<Instruction>();
            if (method.IsStatic)
            {
                foreach (var insn in method.Body.Instructions)
                {
                    if (insn.OpCode == OpCodes.Call)
                    {
                        var methodRef = (MethodReference)insn.Operand;
                        if (methodRef == method)
                        {
                            if (insn.Next != null && insn.Next.OpCode == OpCodes.Ret)
                            {
                                calls.Add(insn);
                            }
                        }
                    }
                }                
            }
            return calls;
        }

        private IList<Instruction> FindInstanceTailCalls(MethodDefinition method)
        {
            var calls = new List<Instruction>();
            if (!method.IsVirtual && !method.IsStatic)
            {
                foreach (var insn in method.Body.Instructions)
                {
                    if (insn.OpCode == OpCodes.Call || insn.OpCode == OpCodes.Callvirt)
                    {
                        var methodRef = (MethodReference)insn.Operand;
                        if (methodRef == method)
                        {
                            if (insn.Next != null && insn.Next.OpCode == OpCodes.Ret)
                            {
                                calls.Add(insn);                            
                            }
                        }
                    }
                }
            }

            // No candidates found.
            if (calls.Count == 0)
            {
                return calls;
            }

            // Filter away any candidates that are not pure *this*
            var map = AnalyzeMethod(method);
            return calls.Where(c => SafeToRewrite(c, map)).ToList();
        }

        public bool SafeToRewrite(Instruction call, Dictionary<Instruction, Node> map)
        {
            var insn = call;
            while (!map.Keys.Contains(insn))
            {
                insn = insn.Previous;
                if (insn == null)
                {
                    throw new NullReferenceException("This shouldn't happen. Why is there no node for this insn?");
                }
            }

            var node = map[insn];
            var stacks = node.GetPossibleStacksAt(call);
            var callee = (MethodReference)call.Operand;
            var pops = callee.Parameters.Count;
            var rewrite = stacks.Select(
                s =>
                {
                    var st = s;
                    for (int i = 0; i < pops; i++)
                    {
                        st = st.Pop();
                    }
                    return st;
                }).All(s => s.Peek());
            return rewrite;
        }

        public Dictionary<Instruction, Node> AnalyzeMethod(MethodDefinition method)
        {
            var insn = method.Body.Instructions[0];
            var map = new Dictionary<Instruction, Node>();
            var root = new Node(method, insn);
            map[insn] = root;
            while (insn != null)
            {
                if (IsBranching(insn))
                {
                    var ot = insn.OpCode.OperandType;
                    var targets = new List<Instruction>();
                    if (ot == OperandType.InlineSwitch)
                    {
                        targets.AddRange((Instruction[])insn.Operand);
                    }
                    else
                    {
                        targets.Add((Instruction)insn.Operand);
                        if (insn.OpCode.FlowControl == FlowControl.Cond_Branch)
                        {
                            targets.Add(insn.Next);
                        }
                    }
                    foreach (var t in targets)
                    {
                        if (!map.ContainsKey(t))
                        {
                            map[t] = new Node(method, t);
                        }
                    }
                }
                insn = insn.Next;
            }

            // For each node in map: add targets and init G function!
            foreach (var n in map.Values)
            {
                n.AddEdges(map);
                n.InitG(map.Keys.ToList());
            }

            // Proceed to process?
            root.Process(ImmutableHashSet<EvalStack>.Empty.Add(new EvalStack()));

            return map;
        }

        private bool IsBranching(Instruction insn)
        {
            var ot = insn.OpCode.OperandType;
            return ot == OperandType.InlineBrTarget ||
                ot == OperandType.ShortInlineBrTarget ||
                ot == OperandType.InlineSwitch;
        }

        private bool Rewrite(AssemblyDefinition assembly)
        {
            bool statics = RewriteStaticTailCalls(assembly);
            bool instances = RewriteInstanceTailCalls(assembly);
            return statics || instances;
        }

        private bool RewriteStaticTailCalls(AssemblyDefinition assembly)
        {
            var map = FindStaticTailCalls(assembly);
            if (!map.Any()) return false;

            foreach (var method in map.Keys)
            {
                Rewrite(method, map[method]);
            }

            return true;
        }

        private bool RewriteInstanceTailCalls(AssemblyDefinition assembly)
        {
            var map = FindInstanceTailCalls(assembly);
            if (!map.Any()) return false;

            foreach (var method in map.Keys)
            {
                Rewrite(method, map[method]);
            }

            return true;
        }

        private void Rewrite(MethodDefinition method, IEnumerable<Instruction> calls)
        {
            Console.WriteLine(method.Name);
            foreach (var call in calls)
            {
                var il = method.Body.GetILProcessor();
                int counter = method.Parameters.Count;
                if (method.HasThis)
                {
                    counter++;
                }
                while (counter > 0)
                {
                    --counter;
                    if (method.HasThis && counter == 0)
                    {
                        var pop = il.Create(OpCodes.Pop);
                        il.InsertBefore(call, pop);
                    }
                    else
                    {
                        var starg = il.Create(OpCodes.Starg, counter);
                        il.InsertBefore(call, starg);                        
                    }
                }                    
                var start = method.Body.Instructions[0];
                var loop = il.Create(OpCodes.Br_S, start);
                il.InsertBefore(call, loop);
                il.Remove(call.Next); // Ret
                il.Remove(call);
            }
        }
    }
}
