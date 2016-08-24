/*
 Thanks to yck1509
 */

using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace NoFuserEx.Deobfuscator {
    class MemberCloner {
        internal static TypeDef CloneTypeDef(TypeDef originalType) {
            var importer = new Importer(originalType.Module, ImporterOptions.TryToUseTypeDefs);
            var newType = Clone(originalType);
            Copy(originalType, newType, importer, true);
            return newType;
        }

        static TypeDefUser Clone(TypeDef originalType) {
            var ret = new TypeDefUser(originalType.Namespace, originalType.Name) { Attributes = originalType.Attributes };

            if (originalType.ClassLayout != null)
                ret.ClassLayout = new ClassLayoutUser(originalType.ClassLayout.PackingSize, originalType.ClassSize);

            foreach (var genericParam in originalType.GenericParameters)
                ret.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));

            foreach (var nestedType in originalType.NestedTypes)
                ret.NestedTypes.Add(Clone(nestedType));

            foreach (var method in originalType.Methods)
                ret.Methods.Add(Clone(method));

            foreach (var field in originalType.Fields)
                ret.Fields.Add(Clone(field));

            return ret;
        }

        static MethodDefUser Clone(MethodDef originalMethod) {
            var ret = new MethodDefUser(originalMethod.Name, null, originalMethod.ImplAttributes, originalMethod.Attributes);

            foreach (var genericParam in originalMethod.GenericParameters)
                ret.GenericParameters.Add(new GenericParamUser(genericParam.Number, genericParam.Flags, "-"));

            return ret;
        }

        static FieldDefUser Clone(FieldDef originalField) {
            return new FieldDefUser(originalField.Name, null, originalField.Attributes);
        }

        static void Copy(TypeDef originalType, TypeDef newType, Importer ctx, bool copySelf) {
            if (copySelf)
                CopyTypeDef(originalType, newType, ctx);

            foreach (var nestedType in originalType.NestedTypes)
                Copy(nestedType, newType, ctx, true);

            foreach (var method in originalType.Methods) {
                CopyMethodDef(method, newType, ctx);
            }

            foreach (var field in originalType.Fields)
                CopyFieldDef(field, newType, ctx);
        }

        static void CopyTypeDef(TypeDef originalType, TypeDef newType, Importer ctx) {
            newType.BaseType = (ITypeDefOrRef)ctx.Import(originalType.BaseType);
            foreach (var iface in originalType.Interfaces)
                newType.Interfaces.Add(new InterfaceImplUser((ITypeDefOrRef)ctx.Import(iface.Interface)));
        }

        static void CopyMethodDef(MethodDef originalMethod, TypeDef newType, Importer ctx) {
            var newMethod = newType.FindMethod(originalMethod.Name);

            // I don't know why but if declaring type is a nested type the dnlib can't found them. :/
            // Then we go to do it manually 
            if (newMethod == null) {
                foreach (var type in newType.GetTypes()) {
                    foreach (var method in type.Methods) {
                        if (method.Name == originalMethod.Name)
                            newMethod = method;
                    }
                }
            }

            if (newMethod == null)
                Logger.Exception(new Exception($"Error cloning the method: {originalMethod.Name}"));

            newMethod.Signature = ctx.Import(originalMethod.Signature);
            newMethod.Parameters.UpdateParameterTypes();

            if (originalMethod.ImplMap != null)
                newMethod.ImplMap = new ImplMapUser(new ModuleRefUser(originalMethod.Module, originalMethod.ImplMap.Module.Name), originalMethod.ImplMap.Name, originalMethod.ImplMap.Attributes);

            foreach (var ca in originalMethod.CustomAttributes)
                newMethod.CustomAttributes.Add(new CustomAttribute((ICustomAttributeType)ctx.Import(ca.Constructor)));

            if (!originalMethod.HasBody) return;
            newMethod.Body = new CilBody(originalMethod.Body.InitLocals, new List<Instruction>(),
                new List<ExceptionHandler>(), new List<Local>()) { MaxStack = originalMethod.Body.MaxStack };

            var bodyMap = new Dictionary<object, object>();

            foreach (var local in originalMethod.Body.Variables) {
                var newLocal = new Local(ctx.Import(local.Type));
                newMethod.Body.Variables.Add(newLocal);
                newLocal.Name = local.Name;
                newLocal.PdbAttributes = local.PdbAttributes;

                bodyMap[local] = newLocal;
            }

            foreach (var instr in originalMethod.Body.Instructions) {
                var newInstr = new Instruction(instr.OpCode, instr.Operand) { SequencePoint = instr.SequencePoint };

                if (newInstr.Operand is IType)
                    newInstr.Operand = ctx.Import((IType)newInstr.Operand);

                else if (newInstr.Operand is IMethod)
                    newInstr.Operand = ctx.Import((IMethod)newInstr.Operand);

                else if (newInstr.Operand is IField)
                    newInstr.Operand = ctx.Import((IField)newInstr.Operand);

                newMethod.Body.Instructions.Add(newInstr);
                bodyMap[instr] = newInstr;
            }

            foreach (var instr in newMethod.Body.Instructions) {
                if (instr.Operand != null && bodyMap.ContainsKey(instr.Operand))
                    instr.Operand = bodyMap[instr.Operand];

                else if (instr.Operand is Instruction[])
                    instr.Operand = ((Instruction[])instr.Operand).Select(target => (Instruction)bodyMap[target]).ToArray();
            }

            foreach (var eh in originalMethod.Body.ExceptionHandlers)
                newMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(eh.HandlerType) {
                    CatchType = eh.CatchType == null ? null : (ITypeDefOrRef)ctx.Import(eh.CatchType),
                    TryStart = (Instruction)bodyMap[eh.TryStart],
                    TryEnd = (Instruction)bodyMap[eh.TryEnd],
                    HandlerStart = (Instruction)bodyMap[eh.HandlerStart],
                    HandlerEnd = (Instruction)bodyMap[eh.HandlerEnd],
                    FilterStart = eh.FilterStart == null ? null : (Instruction)bodyMap[eh.FilterStart]
                });

            newMethod.Body.SimplifyMacros(newMethod.Parameters);
        }

        static void CopyFieldDef(FieldDef originalField, TypeDef newType, Importer ctx) {
            var newField = newType.FindField(originalField.Name);

            // I don't know why but if declaring type is a nested type the dnlib can't found them. :/
            // Then we go to do it manually 
            // Equals methods
            if (newField == null) {
                foreach (var type in newType.GetTypes()) {
                    foreach (var field in type.Fields) {
                        if (field.Name == originalField.Name)
                            newField = field;
                    }
                }
            }

            newField.Signature = ctx.Import(originalField.Signature);
        }
    }
}
