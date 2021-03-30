using Lab4.Ast;
using Lab4.Ast.ClassMembers;
using Lab4.Ast.Declarations;
using Lab4.Parsing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Lab4.Compiling {
	sealed class ProgramCompiler {
		readonly AllTypes allTypes;
		readonly ModuleDefinition module;
		readonly ProgramNode programNode;
		readonly SourceFile sourceFile;
		readonly TypeDefinition mainClass;
		public readonly MethodDefinition MainMethod;
		readonly Dictionary<string, TypeDefinition> typeDefinitionByName
			= new Dictionary<string, TypeDefinition>();
		readonly Dictionary<ClassMethod, MethodDefinition> classMethodDefinitionByNode
			= new Dictionary<ClassMethod, MethodDefinition>();
		readonly Dictionary<FunctionDeclaration, MethodDefinition> functionMethodDefinitionByNode
			= new Dictionary<FunctionDeclaration, MethodDefinition>();
		public ProgramCompiler(
			AllTypes allTypes,
			ProgramNode programNode,
			string mainClassName,
			string mainMethodName
		) {
			this.allTypes = allTypes;
			module = allTypes.Module;
			this.programNode = programNode;
			sourceFile = programNode.SourceFile;
			mainClass = new TypeDefinition(
				"", mainClassName,
				TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
				module.TypeSystem.Object
			);
			module.Types.Add(mainClass);
			MainMethod = new MethodDefinition(
				"Main",
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
				module.TypeSystem.Void
			);
			mainClass.Methods.Add(MainMethod);
		}
		public void Compile() {
			AddClasses();
			AddClassFields();
			AddClassMethods();
			AddFunctions();
			CompileMethods();
			CompileMainMethod();
		}
		Exception MakeError(INode node, string message) {
			return new Exception(sourceFile.MakeErrorMessage(node.Position, message));
		}
		TypeReference GetTypeReference(TypeNode typeNode) {
			var maybeTypeRef = allTypes.TryGetTypeRef(typeNode);
			if (maybeTypeRef == null) {
				throw MakeError(typeNode, $"Неизвестный тип {typeNode.FormattedString}");
			}
			var typeRef = maybeTypeRef.Value;
			if (typeRef == allTypes.Null) {
				throw MakeError(typeNode, $"Ожидали нормальный тип, получили {typeNode.FormattedString}");
			}
			return typeRef.TypeReference;
		}
		void AddClasses() {
			foreach (var classDeclaration in programNode.Declarations.OfType<ClassDeclaration>()) {
				var className = classDeclaration.Name;
				var type = new TypeDefinition(
					"", className,
					TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
					module.TypeSystem.Object
				);
				mainClass.NestedTypes.Add(type);
				if (!allTypes.TryAddType(className, type)) {
					throw MakeError(classDeclaration, $"Класс {className} уже объявлен");
				}
				typeDefinitionByName.Add(className, type);
			}
		}
		void AddClassFields() {
			foreach (var classDeclaration in programNode.Declarations.OfType<ClassDeclaration>()) {
				var className = classDeclaration.Name;
				var type = typeDefinitionByName[className];
				var fieldNames = new HashSet<string>();
				foreach (var classField in classDeclaration.Members.OfType<ClassField>()) {
					var fieldName = classField.Name;
					if (!fieldNames.Add(fieldName)) {
						throw MakeError(classField, $"Поле {fieldName} класса {className} уже объявлено");
					}
					var fieldAttributes = FieldAttributes.Public;
					if (classField.IsReadOnly) {
						fieldAttributes |= FieldAttributes.InitOnly;
					}
					var field = new FieldDefinition(
						fieldName,
						fieldAttributes,
						GetTypeReference(classField.Type)
					);
					type.Fields.Add(field);
				}
			}
		}
		void AddClassMethods() {
			foreach (var classDeclaration in programNode.Declarations.OfType<ClassDeclaration>()) {
				var type = typeDefinitionByName[classDeclaration.Name];
				foreach (var classMethod in classDeclaration.Members.OfType<ClassMethod>()) {
					var method = new MethodDefinition(
						classMethod.Name,
						MethodAttributes.Public | MethodAttributes.HideBySig,
						GetTypeReference(classMethod.ReturnType)
					);
					type.Methods.Add(method);
					classMethodDefinitionByNode.Add(classMethod, method);
					AddParameters(method, classMethod.Parameters);
				}
				AddAndCompileClassConstructor(type);
			}
		}
		void AddFunctions() {
			foreach (var functionDeclaration in programNode.Declarations.OfType<FunctionDeclaration>()) {
				var method = new MethodDefinition(
					functionDeclaration.Name,
					MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
					GetTypeReference(functionDeclaration.ReturnType)
				);
				mainClass.Methods.Add(method);
				functionMethodDefinitionByNode.Add(functionDeclaration, method);
				AddParameters(method, functionDeclaration.Parameters);
				allTypes.AddFunction(method);
			}
		}
		void AddParameters(MethodDefinition md, IReadOnlyList<Parameter> Parameters) {
			var parameterNames = new HashSet<string>();
			foreach (var parameterNode in Parameters) {
				var parameterName = parameterNode.Name;
				if (!parameterNames.Add(parameterName)) {
					throw MakeError(parameterNode, $"Параметр {parameterName} уже объявлен");
				}
				var parameter = new ParameterDefinition(
					parameterNode.Name,
					ParameterAttributes.None,
					GetTypeReference(parameterNode.Type)
				);
				md.Parameters.Add(parameter);
			}
		}
		void AddAndCompileClassConstructor(TypeDefinition type) {
			var constructor = new MethodDefinition(
				".ctor",
				MethodAttributes.Public
				| MethodAttributes.HideBySig
				| MethodAttributes.SpecialName
				| MethodAttributes.RTSpecialName,
				module.TypeSystem.Void
			);
			type.Methods.Add(constructor);
			var cil = constructor.Body.GetILProcessor();
			cil.Emit(OpCodes.Ldarg, constructor.Body.ThisParameter);
			cil.Emit(OpCodes.Call, module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes)));
			foreach (var field in type.Fields) {
				var parameter = new ParameterDefinition(field.Name, ParameterAttributes.None, field.FieldType);
				constructor.Parameters.Add(parameter);
				cil.Emit(OpCodes.Ldarg, constructor.Body.ThisParameter);
				cil.Emit(OpCodes.Ldarg, parameter);
				cil.Emit(OpCodes.Stfld, field);
			}
			cil.Emit(OpCodes.Ret);
		}
		void CompileMethods() {
			foreach (var declaration in programNode.Declarations) {
				if (declaration is FunctionDeclaration functionDeclaration) {
					CompileFunctionDeclaration(functionDeclaration);
				}
				else if (declaration is ClassDeclaration classDeclaration) {
					CompileClassDeclaration(classDeclaration);
				}
			}
		}
		void CompileFunctionDeclaration(FunctionDeclaration functionDeclaration) {
			var method = functionMethodDefinitionByNode[functionDeclaration];
			MethodBodyCompiler.Compile(sourceFile, allTypes, method, functionDeclaration.Body.Statements);
		}
		void CompileClassDeclaration(ClassDeclaration classDeclaration) {
			var type = typeDefinitionByName[classDeclaration.Name];
			foreach (var classMethod in classDeclaration.Members.OfType<ClassMethod>()) {
				var method = classMethodDefinitionByNode[classMethod];
				MethodBodyCompiler.Compile(sourceFile, allTypes, method, classMethod.Body.Statements);
			}
		}
		void CompileMainMethod() {
			MethodBodyCompiler.Compile(sourceFile, allTypes, MainMethod, programNode.Statements);
		}
	}
}
