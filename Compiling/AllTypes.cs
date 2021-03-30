using BuiltinTypes;
using Lab4.Ast;
using Lab4.Ast.TypeNodes;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Lab4.Compiling {
	sealed class AllTypes {
		public readonly ModuleDefinition Module;
		public readonly TypeRef Int;
		public readonly TypeRef Bool;
		public readonly TypeRef String;
		public readonly TypeRef Void;
		public readonly TypeRef Object;
		public readonly TypeRef Null;
		readonly Dictionary<string, TypeRef> typeByName = new Dictionary<string, TypeRef>();
		readonly Dictionary<TypeRef, string> nameByType = new Dictionary<TypeRef, string>();
		readonly List<MethodReference> functions = new List<MethodReference>();
		public AllTypes(ModuleDefinition module) {
			Module = module;
			Int = AddBuiltinType("int", module.TypeSystem.Int32);
			Bool = AddBuiltinType("bool", module.TypeSystem.Boolean);
			String = AddBuiltinType("string", module.TypeSystem.String);
			Void = AddBuiltinType("void", module.TypeSystem.Void);
			Object = AddBuiltinType("object", module.TypeSystem.Object);
			Null = AddBuiltinType("Null", null);
			foreach (var m in typeof(BuiltinFunctions).GetMethods()) {
				if (m.IsStatic) {
					AddFunction(module.ImportReference(m));
				}
			}
		}
		public void AddFunction(MethodReference mr) {
			functions.Add(Module.ImportReference(mr));
		}
		TypeRef AddBuiltinType(string name, TypeReference typeReference) {
			var type = new TypeRef(typeReference);
			if (!TryAddType(name, type)) {
				throw new Exception();
			}
			return type;
		}
		public bool TryAddType(string name, TypeRef type) {
			if (typeByName.ContainsKey(name) || nameByType.ContainsKey(type)) {
				return false;
			}
			nameByType.Add(type, name);
			typeByName.Add(name, type);
			return true;
		}
		public bool CanAssign(TypeRef sourceType, TypeRef targetType) {
			if (sourceType == targetType) {
				return true;
			}
			if (sourceType == Null && targetType.CanBeNull) {
				return true;
			}
			return false;
		}
		public bool CanCall(MethodReference mr, IReadOnlyList<TypeRef> argumentTypes) {
			var parameterTypes = mr.Parameters.Select(p => new TypeRef(p.ParameterType)).ToList();
			if (parameterTypes.Count != argumentTypes.Count) {
				return false;
			}
			for (var i = 0; i < parameterTypes.Count; i++) {
				if (!CanAssign(argumentTypes[i], parameterTypes[i])) {
					return false;
				}
			}
			return true;
		}
		public IReadOnlyList<MethodReference> GetCallableFunctions(
			string functionName, IReadOnlyList<TypeRef> argumentTypes
		) {
			return functions
				.Where(fn => fn.Name == functionName && CanCall(fn, argumentTypes))
				.Select(Module.ImportReference)
				.ToList();
		}
		public IReadOnlyList<MethodReference> GetCallableMethods(
			TypeRef type, string methodName, IReadOnlyList<TypeRef> argumentTypes
		) {
			if (type == Null) {
				return Array.Empty<MethodReference>();
			}
			return type.GetTypeDefinition().Methods
				.Where(fn => fn.Name == methodName && CanCall(fn, argumentTypes))
				.Select(Module.ImportReference)
				.ToList();
		}
		public TypeRef? TryGetTypeRef(TypeNode type) {
			if (type is SimpleTypeNode simpleType) {
				return TryGetTypeRef(simpleType.Name);
			}
			if (type is ParenthesesTypeNode parenthesesType) {
				return TryGetTypeRef(parenthesesType.Type);
			}
			throw new NotSupportedException();
		}
		public TypeRef? TryGetTypeRef(string name) {
			if (typeByName.TryGetValue(name, out var type)) {
				return type;
			}
			return null;
		}
		public string GetTypeName(TypeRef type) {
			if (nameByType.TryGetValue(type, out var name)) {
				return name;
			}
			return type.TypeReference.FullName;
		}
		public TypeRef? TryGetCommonType(TypeRef a, TypeRef b) {
			if (a == b) {
				return a;
			}
			if (a == Null && b.CanBeNull) {
				return b;
			}
			if (b == Null && a.CanBeNull) {
				return a;
			}
			return null;
		}
	}
}
