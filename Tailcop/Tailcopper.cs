using System;
using System.Collections.Generic;
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

        public bool TamperWith(string assemblyPath)
        {
            var assembly = ReadAssembly(assemblyPath);
            bool result = TamperWith(assembly);
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

        private Dictionary<MethodDefinition, IEnumerable<Instruction>> FindTailCalls(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Types
                .SelectMany(t => t.Methods)
                .Select(m => Tuple.Create(m, FindTailCalls(m)))
                .Where(tp => tp.Item2.Any())
                .GroupBy(tp => tp.Item1, tp => tp.Item2)
                .ToDictionary(g => g.Key, g => g.SelectMany(it => it));
        }

        private IList<Instruction> FindTailCalls(MethodDefinition method) 
        {
            var calls = new List<Instruction>();
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
            return calls;
        }

        private bool TamperWith(AssemblyDefinition assembly) 
        {
            var map = FindTailCalls(assembly);
            if (!map.Any()) return false;

            foreach (var method in map.Keys)
            {
                TamperWith(method, map[method]);
            }

            return true;
        }

        private void TamperWith(MethodDefinition method, IEnumerable<Instruction> calls)
        {
            foreach (var call in calls)
            {
                var il = method.Body.GetILProcessor();
                int counter = method.Parameters.Count;
                while (counter > 0)
                {
                    var starg = il.Create(OpCodes.Starg, --counter);
                    il.InsertBefore(call, starg);
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
