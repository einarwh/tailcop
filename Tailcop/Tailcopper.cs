using System;
using System.Collections.Generic;

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

        private Map<MethodDefinition, IEnumerable<Instruction>> FindTailCalls(AssemblyDefinition assembly)
        {
            var result = new Map<MethodDefinition, IEnumerable<Instruction>>();

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    var calls = new List<Instruction>();
                    foreach (var insn in method.Body.Instructions)
                    {
                        if (insn.OpCode == OpCodes.Call)
                        {
                            var methodRef = (MethodReference)insn.Operand;
                            if (methodRef == method)
                            {
                                // TODO: require tail call! 
                                // Next instruction must be RET.
                                calls.Add(insn);
                            }
                        }
                    }
                    if (calls.Count() > 0) 
                    {
                        result[method] = calls;
                    }
                }
            }

            return result;

        }

        private Map<MethodDefinition, IList<Instruction>> FindTailCalls(AssemblyDefinition assembly)
        {
            return assembly.MainModule.Types
                .SelectMany(t => t.Methods)
                .Select(m => new Tuple(m, FindTailCalls(m.Body.Instructions)))
                .Where(tp => tp.Item2.Count() > 0)
                .GroupBy(tp => tp.Item1, tp => tp.Item2);
            // ToDictionary somehow.
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
            var map = FindTailCalls();
            if (map.Count() == 0) return false;

            foreach (var method in callMap.Keys)
            {
                TamperWith(method, callMap[method]);
            }

            return true;
        }

        private bool TamperWith(MethodDefinition method, List<Instruction>> calls) 
        {
            foreach (var call in calls)
            {
                var il = method.Body.GetILProcessor();
                int counter = method.Parameters.Count;
                var last = call;
                do
                {
                    var starg = il.Create(OpCodes.Starg, --counter);
                    il.InsertAfter(last, starg);
                    last = starg;
                }
                while (counter > 0);
                var loop = il.Create(OpCodes.Br_S, method.Body.Instructions[0]);
                il.InsertAfter(last, loop);
                il.Remove(call);
            }
        }

        private bool TamperWith1(AssemblyDefinition assembly)
        {
            bool result = false;

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                foreach (MethodDefinition method in type.Methods)
                {
                    var calls = new List<Instruction>();
                    foreach (var insn in method.Body.Instructions)
                    {
                        if (insn.OpCode == OpCodes.Call)
                        {
                            var methodRef = (MethodReference)insn.Operand;
                            if (methodRef == method)
                            {
                                result = true;
                                calls.Add(insn);
                            }
                        }
                    }
                    foreach (var call in calls)
                    {
                        var il = method.Body.GetILProcessor();
                        int counter = method.Parameters.Count;
                        var last = call;
                        do
                        {
                            var starg = il.Create(OpCodes.Starg, --counter);
                            il.InsertAfter(last, starg);
                            last = starg;
                        }
                        while (counter > 0);
                        var loop = il.Create(OpCodes.Br_S, method.Body.Instructions[0]);
                        il.InsertAfter(last, loop);
                        il.Remove(call);
                    }
                }
            }

            return result;
        }

    }
}
