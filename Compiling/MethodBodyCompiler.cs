using BuiltinTypes;
using Lab4.Ast;
using Lab4.Ast.Expressions;
using Lab4.Ast.Statements;
using Lab4.Parsing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
namespace Lab4.Compiling {
	sealed class MethodBodyCompiler : IStatementVisitor, IExpressionVisitor<TypeRef> {
		static readonly VariableDefinition missingVariable = null;
		readonly SourceFile sourceFile;
		readonly AllTypes allTypes;
		readonly ModuleDefinition module;
		readonly MethodDefinition method;
		readonly ILProcessor cil;
		readonly Dictionary<string, VariableDefinition> variables = new Dictionary<string, VariableDefinition>();
		Dictionary<string, VariableDefinition> currentBlockShadowedVariables = null;
		readonly List<Instruction> loopStarts = new List<Instruction>();
		readonly List<Instruction> loopEnds = new List<Instruction>();
		readonly Dictionary<string, int> loopEndIndexByLabel = new Dictionary<string, int>();
		MethodBodyCompiler(SourceFile sourceFile, AllTypes allTypes, MethodDefinition method) {
			this.sourceFile = sourceFile;
			this.allTypes = allTypes;
			module = allTypes.Module;
			this.method = method;
			cil = method.Body.GetILProcessor();
		}
		public static void Compile(
			SourceFile sourceFile,
			AllTypes types,
			MethodDefinition method,
			IEnumerable<IStatement> statements
		) {
			new MethodBodyCompiler(sourceFile, types, method).CompileMethodStatements(statements);
		}
		void CompileMethodStatements(IEnumerable<IStatement> statements) {
			foreach (var statement in statements) {
				CompileStatement(statement);
			}
			if (cil.Body.Instructions.Count != 0 && cil.Body.Instructions.Last().OpCode == OpCodes.Ret) {
				return;
			}
			if (method.ReturnType == allTypes.Void) {
				cil.Emit(OpCodes.Ret);
				return;
			}
			cil.Emit(OpCodes.Ldstr, "Пропустили return");
			cil.Emit(OpCodes.Newobj, module.ImportReference(
				typeof(Exception).GetConstructor(new[] { typeof(string) })));
			cil.Emit(OpCodes.Throw);
		}
		void CompileBlock(Block block) {
			var oldShadowedVariables = currentBlockShadowedVariables;
			currentBlockShadowedVariables = new Dictionary<string, VariableDefinition>();
			foreach (var statement in block.Statements) {
				CompileStatement(statement);
			}
			foreach (var kv in currentBlockShadowedVariables) {
				var name = kv.Key;
				var shadowedVariable = kv.Value;
				if (shadowedVariable == missingVariable) {
					variables.Remove(name);
				}
				else {
					variables[name] = shadowedVariable;
				}
			}
			currentBlockShadowedVariables = oldShadowedVariables;
		}
		#region statements
		void CompileStatement(IStatement statement) {
			statement.Accept(this);
		}
		public void VisitExpressionStatement(ExpressionStatement expressionStatement) {
			var type = CompileExpression(expressionStatement.Expr);
			if (type != allTypes.Void) {
				cil.Emit(OpCodes.Pop);
			}
		}
		public void VisitVariableDeclaration(VariableDeclaration variableDeclaration) {
			var initialValueType = CompileExpression(variableDeclaration.InitialValue);
			var name = variableDeclaration.VariableName;
			if (currentBlockShadowedVariables != null && !currentBlockShadowedVariables.ContainsKey(name)) {
				if (variables.TryGetValue(name, out var existingVariable)) {
					currentBlockShadowedVariables[name] = existingVariable;
				}
				else {
					currentBlockShadowedVariables[name] = missingVariable;
				}
			}
			var variableType = initialValueType;
			if (variableDeclaration.Type != null) {
				var maybeVariableType = allTypes.TryGetTypeRef(variableDeclaration.Type);
				if (maybeVariableType == null) {
					throw UnknownType(variableDeclaration.Type);
				}
				variableType = maybeVariableType.Value;
			}
			if (variableType == allTypes.Null) {
				var variableTypeName = allTypes.GetTypeName(variableType);
				throw MakeError(variableDeclaration, $"Нельзя создать переменную типа {variableTypeName}");
			}
			if (!allTypes.CanAssign(initialValueType, variableType)) {
				throw UnassignableType(variableDeclaration, initialValueType, variableType);
			}
			var variable = new VariableDefinition(variableType.TypeReference);
			method.Body.Variables.Add(variable);
			variables[name] = variable;
			cil.Emit(OpCodes.Stloc, variable);
		}
		public void VisitAssignment(Assignment assignment) {
			{
				if (assignment.Target is Identifier identifier) {
					var valueType = CompileExpression(assignment.Value);
					if (variables.TryGetValue(identifier.Name, out var variable)) {
						if (!allTypes.CanAssign(valueType, variable.VariableType)) {
							throw UnassignableType(assignment, valueType, variable.VariableType);
						}
						cil.Emit(OpCodes.Stloc, variable);
						return;
					}
					var parameter = method.Parameters.SingleOrDefault(x => x.Name == identifier.Name);
					if (parameter != null) {
						if (!allTypes.CanAssign(valueType, parameter.ParameterType)) {
							throw UnassignableType(assignment, valueType, parameter.ParameterType);
						}
						cil.Emit(OpCodes.Starg, parameter);
						return;
					}
					throw MakeError(identifier, $"Присваивание в неизвестную переменную {identifier.Name}");
				}
			}
			{
				if (assignment.Target is MemberAccess memberAccess) {
					var objType = CompileExpression(memberAccess.Obj);
					if (objType != allTypes.Null) {
						var field = objType.GetTypeDefinition().Fields.SingleOrDefault(f => f.Name == memberAccess.MemberName);
						if (field != null) {
							if (field.Resolve().IsInitOnly) {
								throw MakeError(memberAccess, $"Нельзя присвоить значение в поле {allTypes.GetTypeName(objType)}.{memberAccess.MemberName}, т.к. оно имеет модификатор readonly");
							}
							var valueType = CompileExpression(assignment.Value);
							if (allTypes.CanAssign(valueType, field.FieldType)) {
								cil.Emit(OpCodes.Stfld, module.ImportReference(field));
								return;
							}
							throw MakeError(memberAccess, $"Нельзя присвоить {allTypes.GetTypeName(valueType)} в поле {allTypes.GetTypeName(objType)}.{memberAccess.MemberName} типа {allTypes.GetTypeName(field.FieldType)}");
						}
					}
					throw MakeError(memberAccess, $"Тип {allTypes.GetTypeName(objType)} не содержит поля {memberAccess.MemberName}");
				}
			}
			{
				if (assignment.Target is IndexAccess indexAccess) {
					var objType = CompileExpression(indexAccess.Obj);
					if (objType != allTypes.Null) {
						var indexType = CompileExpression(indexAccess.Index);
						var valueType = CompileExpression(assignment.Value);
						var methods = allTypes.GetCallableMethods(objType, "set_Item", new[] { indexType, valueType });
						if (methods.Count > 1) {
							throw MakeError(indexAccess, $"Не удалось вызвать индексатор для записи с аргументом ({allTypes.GetTypeName(indexType)}), так как подошло несколько ({methods.Count})");
						}
						if (methods.Count != 0) {
							cil.Emit(OpCodes.Callvirt, methods[0]);
							return;
						}
						throw MakeError(indexAccess.Obj, $"Метод для присвоения элемента по индексу не был найден ");
					}
				}
			}
			throw MakeError(assignment, $"Нельзя присваивать в {assignment.Target.FormattedString}");
		}
		public void VisitIf(If ifStatement) {
			var conditionType = CompileExpression(ifStatement.Condition);
			if (conditionType != allTypes.Bool) {
				throw UnexpectedType(ifStatement.Condition, conditionType, allTypes.Bool);
			}
			var afterIf = Instruction.Create(OpCodes.Nop);
			cil.Emit(OpCodes.Brfalse, afterIf);
			CompileBlock(ifStatement.Body);
			cil.Append(afterIf);
		}
		public void VisitWhile(While whileStatement) {
			var beforeCondition = Instruction.Create(OpCodes.Nop);
			loopStarts.Add(beforeCondition);
			cil.Append(beforeCondition);
			var conditionType = CompileExpression(whileStatement.Condition);
			if (conditionType != allTypes.Bool) {
				throw UnexpectedType(whileStatement.Condition, conditionType, allTypes.Bool);
			}
			var afterWhile = Instruction.Create(OpCodes.Nop);
			loopEnds.Add(afterWhile);
			bool hasOldLoopEndIndex = false;
			int oldLoopEndEndex = 0;
			if (whileStatement.Label != null) {
				hasOldLoopEndIndex = loopEndIndexByLabel.TryGetValue(whileStatement.Label.Name, out oldLoopEndEndex);
				loopEndIndexByLabel[whileStatement.Label.Name] = loopEnds.Count - 1;
			}
			cil.Emit(OpCodes.Brfalse, afterWhile);
			CompileBlock(whileStatement.Body);
			cil.Emit(OpCodes.Br, beforeCondition);
			cil.Append(afterWhile);
			if (whileStatement.Label != null) {
				if (hasOldLoopEndIndex) {
					loopEndIndexByLabel[whileStatement.Label.Name] = oldLoopEndEndex;
				}
				else {
					loopEndIndexByLabel.Remove(whileStatement.Label.Name);
				}
			}
			loopStarts.RemoveAt(loopStarts.Count - 1);
			loopEnds.RemoveAt(loopEnds.Count - 1);
		}
		public void VisitReturn(Return returnStatement) {
			var valueType = allTypes.Void;
			if (returnStatement.Value != null) {
				valueType = CompileExpression(returnStatement.Value);
			}
			if (!allTypes.CanAssign(valueType, method.ReturnType)) {
				throw UnassignableType(returnStatement, valueType, method.ReturnType);
			}
			cil.Emit(OpCodes.Ret);
		}
		public void VisitBreak(Break breakStatement) {
			int nestingLevel = 1;
			if (breakStatement.Target is Number numberTarget) {
				nestingLevel = ToInt(numberTarget);
				if (!(1 <= nestingLevel && nestingLevel <= loopEnds.Count)) {
					throw MakeError(breakStatement, "break находится вне цикла");
				}
			}
			else if (breakStatement.Target is Identifier identifierTarget) {
				if (!loopEndIndexByLabel.TryGetValue(identifierTarget.Name, out var loopEndIndex)) {
					throw MakeError(identifierTarget, $"Нет цикла с меткой {identifierTarget.Name}");
				}
				nestingLevel = loopEnds.Count - loopEndIndex;
			}
			cil.Emit(OpCodes.Br, loopEnds[loopEnds.Count - nestingLevel]);
		}
		public void VisitContinue(Continue continueStatement) {
			int nestingLevel = 1;
			if (continueStatement.NestingLevel != null) {
				nestingLevel = ToInt(continueStatement.NestingLevel);
			}
			if (!(1 <= nestingLevel && nestingLevel <= loopStarts.Count)) {
				throw MakeError(continueStatement, "continue находится вне цикла");
			}
			cil.Emit(OpCodes.Br, loopStarts[loopStarts.Count - nestingLevel]);
		}
		#endregion
		#region expressions
		TypeRef CompileExpression(IExpression expression) {
			return expression.Accept(this);
		}
		public TypeRef VisitNumber(Number expression) {
			cil.Emit(OpCodes.Ldc_I4, int.Parse(expression.Lexeme));
			return allTypes.Int;
		}
		public int ToInt(Number number) {
			if (!int.TryParse(number.Lexeme, NumberStyles.None, NumberFormatInfo.InvariantInfo, out int value)) {
				throw MakeError(number, $"Не удалось преобразовать {number.Lexeme} в int");
			}
			return value;
		}
		public TypeRef VisitStringLiteral(StringLiteral stringLiteral) {
			var value = stringLiteral.Lexeme.Substring(1, stringLiteral.Lexeme.Length - 2).Replace("''", "'");
			cil.Emit(OpCodes.Ldstr, value);
			return allTypes.String;
		}
		public TypeRef VisitArrayLiteral(ArrayLiteral arrayLiteral) {
			if (arrayLiteral.Elements.Count == 0) {
				throw MakeError(arrayLiteral, "Нельзя создать пустой массив");
			}
			cil.Emit(OpCodes.Ldc_I4, arrayLiteral.Elements.Count);
			var newarr = Instruction.Create(OpCodes.Newarr, module.TypeSystem.Object);
			cil.Append(newarr);
			var stelems = new Instruction[arrayLiteral.Elements.Count];
			var elementsType = allTypes.Null;
			for (int i = 0; i < arrayLiteral.Elements.Count; i++) {
				cil.Emit(OpCodes.Dup);
				cil.Emit(OpCodes.Ldc_I4, i);
				var element = arrayLiteral.Elements[i];
				var elementType = CompileExpression(element);
				if (i == 0) {
					elementsType = elementType;
				}
				else {
					var commonType = allTypes.TryGetCommonType(elementsType, elementType);
					if (commonType == null) {
						throw NoCommonType(element, elementsType, elementType);
					}
					elementsType = commonType.Value;
				}
				var stelem = Instruction.Create(OpCodes.Stelem_Ref);
				cil.Append(stelem);
				stelems[i] = stelem;
			}
			if (elementsType == allTypes.Null) {
				throw MakeError(arrayLiteral, "Нельзя создавать массив Null-ов");
			}
			newarr.Operand = elementsType.TypeReference;
			OpCode stelemOpCode;
			if (elementsType == allTypes.Bool) {
				stelemOpCode = OpCodes.Stelem_I1;
			}
			else if (elementsType == allTypes.Int) {
				stelemOpCode = OpCodes.Stelem_I4;
			}
			else if (elementsType.CanBeNull) {
				stelemOpCode = OpCodes.Stelem_Ref;
			}
			else {
				var typeName = allTypes.GetTypeName(elementsType);
				throw MakeError(arrayLiteral, $"Неподдерживаемый тип элементов массива {typeName}");
			}
			for (int i = 0; i < stelems.Length; i++) {
				stelems[i].OpCode = stelemOpCode;
			}
			return new ArrayType(elementsType.TypeReference);
		}
		public TypeRef VisitIdentifier(Identifier identifier) {
			if (identifier.Name == "this") {
				if (!method.HasThis) {
					throw MakeError(identifier, "Здесь нельзя использовать this");
				}
				cil.Emit(OpCodes.Ldarg, method.Body.ThisParameter);
				return method.Body.ThisParameter.ParameterType;
			}
			if (identifier.Name == "true") {
				cil.Emit(OpCodes.Ldc_I4, 1);
				return allTypes.Bool;
			}
			if (identifier.Name == "false") {
				cil.Emit(OpCodes.Ldc_I4, 0);
				return allTypes.Bool;
			}
			if (identifier.Name == "null") {
				cil.Emit(OpCodes.Ldnull);
				return allTypes.Null;
			}
			if (variables.TryGetValue(identifier.Name, out var variable)) {
				cil.Emit(OpCodes.Ldloc, variable);
				return variable.VariableType;
			}
			var parameter = method.Parameters.SingleOrDefault(x => x.Name == identifier.Name);
			if (parameter != null) {
				cil.Emit(OpCodes.Ldarg, parameter);
				return parameter.ParameterType;
			}
			throw MakeError(identifier, $"Неизвестный идентификатор {identifier.Name}");
		}
		public TypeRef VisitParentheses(Parentheses expression) {
			return CompileExpression(expression.Expr);
		}
		public TypeRef VisitBinary(Binary binary) {
			switch (binary.Operator) {
				case BinaryOperator.Addition:
					return CompileAddition(binary);
				case BinaryOperator.Subtraction:
					return CompileSubtraction(binary);
				case BinaryOperator.Multiplication:
					return CompileMultiplication(binary);
				case BinaryOperator.Division:
					return CompileDivision(binary);
				case BinaryOperator.Remainder:
					return CompileRemainder(binary);
				case BinaryOperator.Less:
					return CompileLess(binary);
				case BinaryOperator.LessEqual:
					return CompileLessEqual(binary);
				case BinaryOperator.Greater:
					return CompileGreater(binary);
				case BinaryOperator.GreaterEqual:
					return CompileGreaterEqual(binary);
				case BinaryOperator.Equal:
					return CompileEqual(binary);
				case BinaryOperator.NotEqual:
					return CompileNotEqual(binary);
				case BinaryOperator.And:
					return CompileAnd(binary);
				case BinaryOperator.Or:
					return CompileOr(binary);
				default:
					throw new NotSupportedException();
			}
		}
		#region binary
		TypeRef CompileAddition(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Addition);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			if (leftType == allTypes.Int && rightType == allTypes.Int) {
				cil.Emit(OpCodes.Add);
				return allTypes.Int;
			}
			if (leftType == allTypes.String && rightType == allTypes.String) {
				var method = typeof(string).GetMethod(nameof(string.Concat), new Type[] { typeof(string), typeof(string) });
				cil.Emit(OpCodes.Call, module.ImportReference(method));
				return allTypes.String;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		TypeRef CompileSubtraction(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Subtraction);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			if (leftType == allTypes.Int && rightType == allTypes.Int) {
				cil.Emit(OpCodes.Sub);
				return allTypes.Int;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		TypeRef CompileMultiplication(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Multiplication);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			if (leftType == allTypes.Int && rightType == allTypes.Int) {
				cil.Emit(OpCodes.Mul);
				return allTypes.Int;
			}
			if (leftType == allTypes.String && rightType == allTypes.Int) {
				var repeatMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.Repeat)).MakeGenericMethod(new Type[] { typeof(string) });
				cil.Emit(OpCodes.Call, module.ImportReference(repeatMethod));
				var concatMethod = typeof(string).GetMethod(nameof(string.Concat), new Type[] { typeof(IEnumerable<string>) });
				cil.Emit(OpCodes.Call, module.ImportReference(concatMethod));
				return allTypes.String;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		TypeRef CompileDivision(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Division);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			if (leftType == allTypes.Int && rightType == allTypes.Int) {
				cil.Emit(OpCodes.Div);
				return allTypes.Int;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		TypeRef CompileRemainder(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Remainder);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			if (leftType == allTypes.Int && rightType == allTypes.Int) {
				cil.Emit(OpCodes.Rem);
				return allTypes.Int;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		#region less
		TypeRef CompileLess(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Less);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			return EmitLess(binary, leftType, rightType);
		}
		TypeRef CompileLessEqual(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.LessEqual);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			EmitSwapTopStackValues(leftType, rightType);
			var resultType = EmitLess(binary, rightType, leftType);
			return EmitNegateBinaryResult(binary, leftType, rightType, resultType);
		}
		TypeRef CompileGreater(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Greater);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			EmitSwapTopStackValues(leftType, rightType);
			return EmitLess(binary, rightType, leftType);
		}
		TypeRef CompileGreaterEqual(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.GreaterEqual);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			var resultType = EmitLess(binary, leftType, rightType);
			return EmitNegateBinaryResult(binary, leftType, rightType, resultType);
		}
		TypeRef EmitLess(Binary binary, TypeRef leftType, TypeRef rightType) {
			if (leftType == allTypes.Int && rightType == allTypes.Int || leftType == allTypes.Bool && rightType == allTypes.Bool) {
				cil.Emit(OpCodes.Clt);
				return allTypes.Bool;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		#endregion
		void EmitSwapTopStackValues(TypeRef beforeTopType, TypeRef topType) {
			var beforeTop = new VariableDefinition(beforeTopType.TypeReference);
			method.Body.Variables.Add(beforeTop);
			var top = new VariableDefinition(topType.TypeReference);
			method.Body.Variables.Add(top);
			cil.Emit(OpCodes.Stloc, top);
			cil.Emit(OpCodes.Stloc, beforeTop);
			cil.Emit(OpCodes.Ldloc, top);
			cil.Emit(OpCodes.Ldloc, beforeTop);
		}
		#region equal
		TypeRef CompileEqual(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Equal);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			return EmitEqual(binary, leftType, rightType);
		}
		TypeRef CompileNotEqual(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.NotEqual);
			var leftType = CompileExpression(binary.Left);
			var rightType = CompileExpression(binary.Right);
			var resultType = EmitEqual(binary, leftType, rightType);
			return EmitNegateBinaryResult(binary, leftType, rightType, resultType);
		}
		TypeRef EmitEqual(Binary binary, TypeRef leftType, TypeRef rightType) {
			if (leftType == rightType) {
				cil.Emit(OpCodes.Ceq);
				return allTypes.Bool;
			}
			if (leftType.CanBeNull && rightType == allTypes.Null) {
				cil.Emit(OpCodes.Ceq);
				return allTypes.Bool;
			}
			if (leftType == allTypes.Null && rightType.CanBeNull) {
				cil.Emit(OpCodes.Ceq);
				return allTypes.Bool;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		#endregion
		TypeRef EmitNegateBinaryResult(Binary binary, TypeRef leftType, TypeRef rightType, TypeRef resultType) {
			if (resultType == allTypes.Bool) {
				cil.Emit(OpCodes.Ldc_I4, 0);
				cil.Emit(OpCodes.Ceq);
				return allTypes.Bool;
			}
			throw MakeBinaryError(binary, leftType, rightType);
		}
		TypeRef CompileAnd(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.And);
			var leftType = CompileExpression(binary.Left);
			TypeRef rightType;
			if (leftType == allTypes.Bool) {
				var exit = Instruction.Create(OpCodes.Nop);
				cil.Emit(OpCodes.Dup);
				cil.Emit(OpCodes.Brfalse, exit);
				cil.Emit(OpCodes.Pop);
				rightType = CompileExpression(binary.Right);
				if (rightType != allTypes.Bool) {
					throw MakeBinaryError(binary, leftType, rightType);
				}
				cil.Append(exit);
				return allTypes.Bool;
			}
			rightType = CompileExpression(binary.Right);
			throw MakeBinaryError(binary, leftType, rightType);
		}
		TypeRef CompileOr(Binary binary) {
			Debug.Assert(binary.Operator == BinaryOperator.Or);
			var leftType = CompileExpression(binary.Left);
			TypeRef rightType;
			if (leftType == allTypes.Bool) {
				var exit = Instruction.Create(OpCodes.Nop);
				cil.Emit(OpCodes.Dup);
				cil.Emit(OpCodes.Brtrue, exit);
				cil.Emit(OpCodes.Pop);
				rightType = CompileExpression(binary.Right);
				if (rightType != allTypes.Bool) {
					throw MakeBinaryError(binary, leftType, rightType);
				}
				cil.Append(exit);
				return allTypes.Bool;
			}
			rightType = CompileExpression(binary.Right);
			throw MakeBinaryError(binary, leftType, rightType);
		}
		Exception MakeBinaryError(Binary binary, TypeRef leftType, TypeRef rightType) {
			return MakeError(binary, $"Бинарная операция {binary.OperatorString} не поддерживается для типов {allTypes.GetTypeName(leftType)} и {allTypes.GetTypeName(rightType)}");
		}
		#endregion
		public TypeRef VisitTernary(Ternary ternary) {
			var conditionType = CompileExpression(ternary.Condition);
			if (conditionType != allTypes.Bool) {
				throw UnexpectedType(ternary.Condition, conditionType, allTypes.Bool);
			}
			var alternativeExpression = Instruction.Create(OpCodes.Nop);
			cil.Emit(OpCodes.Brfalse, alternativeExpression);
			var consequentType = CompileExpression(ternary.Consequent);
			var afterIf = Instruction.Create(OpCodes.Nop);
			cil.Emit(OpCodes.Br, afterIf);
			cil.Append(alternativeExpression);
			var alternativeType = CompileExpression(ternary.Alternative);
			cil.Append(afterIf);
			var commonType = allTypes.TryGetCommonType(consequentType, alternativeType);
			if (commonType == null) {
				throw NoCommonType(ternary, consequentType, alternativeType);
			}
			return commonType.Value;
		}
		public TypeRef VisitCall(Call call) {
			{
				if (call.Function is Identifier identifier) {
					var argumentTypes = new List<TypeRef>();
					foreach (var argument in call.Arguments) {
						argumentTypes.Add(CompileExpression(argument));
					}
					{
						var methods = allTypes.GetCallableFunctions(identifier.Name, argumentTypes);
						if (methods.Count > 1) {
							var argumentTypeNames = string.Join(", ", argumentTypes.Select(allTypes.GetTypeName));
							throw MakeError(call, $"Не удалось вызвать {identifier.Name} с аргументами ({argumentTypeNames}), так как подошло несколько функций ({methods.Count})");
						}
						if (methods.Count != 0) {
							cil.Emit(OpCodes.Call, methods[0]);
							return methods[0].ReturnType;
						}
					}
					{
						var maybeType = allTypes.TryGetTypeRef(identifier.Name);
						if (maybeType != null) {
							var type = maybeType.Value;
							TypeDefinition typeDefinition = null;
							MethodReference constructor = null;
							if (type != allTypes.Null) {
								typeDefinition = type.GetTypeDefinition();
								constructor = allTypes.GetCallableMethods(type, ".ctor", argumentTypes).SingleOrDefault();
							}
							if (constructor == null) {
								var typeName = allTypes.GetTypeName(type);
								var argumentTypeNames = string.Join(", ", argumentTypes.Select(x => allTypes.GetTypeName(x)));
								var parameterTypeNames = string.Join(", ", typeDefinition.Fields.Select(x => allTypes.GetTypeName(x.FieldType)));
								throw MakeError(call, $"Не удалось создать {typeName} c аргументами ({argumentTypeNames}), ожидали аргументы ({parameterTypeNames})");
							}
							cil.Emit(OpCodes.Newobj, constructor);
							return type;
						}
					}
					{
						var argumentTypeNames = string.Join(", ", argumentTypes.Select(allTypes.GetTypeName));
						throw MakeError(call, $"Не удалось вызвать {identifier.Name} c аргументами ({argumentTypeNames})");
					}
				}
			}
			{
				if (call.Function is MemberAccess memberAccess) {
					var objType = CompileExpression(memberAccess.Obj);
					var argumentTypes = new List<TypeRef>();
					foreach (var argument in call.Arguments) {
						argumentTypes.Add(CompileExpression(argument));
					}
					var methods = allTypes.GetCallableMethods(objType, memberAccess.MemberName, argumentTypes);
					if (methods.Count > 1) {
						var argumentTypeNames = string.Join(", ", argumentTypes.Select(allTypes.GetTypeName));
						throw MakeError(call, $"Не удалось вызвать {memberAccess.MemberName} у {allTypes.GetTypeName(objType)} с аргументами ({argumentTypeNames}), так как подошло несколько функций ({methods.Count})");
					}
					if (methods.Count != 0) {
						cil.Emit(OpCodes.Call, methods[0]);
						return methods[0].ReturnType;
					}
					{
						var argumentTypeNames = string.Join(", ", argumentTypes.Select(allTypes.GetTypeName));
						throw MakeError(call, $"Не удалось вызвать {memberAccess.MemberName} у {allTypes.GetTypeName(objType)} c аргументами ({argumentTypeNames})");
					}
				}
			}
			throw MakeError(call, $"Не удалось вызвать {call.FormattedString}");
		}
		public TypeRef VisitMemberAccess(MemberAccess memberAccess) {
			var objType = CompileExpression(memberAccess.Obj);
			if (objType != allTypes.Null) {
				var field = objType.GetTypeDefinition().Fields.SingleOrDefault(f => f.Name == memberAccess.MemberName);
				var property = objType.GetTypeDefinition().Properties.SingleOrDefault(p => p.Name == memberAccess.MemberName);
				if (field != null && property != null) {
					throw MakeError(memberAccess, $"Объект {allTypes.GetTypeName(objType)} содержит одновременно поле и свойство {memberAccess.MemberName}");
				}
				if (field != null) {
					cil.Emit(OpCodes.Ldfld, module.ImportReference(field));
					return field.FieldType;
				}
				if (property != null) {
					cil.Emit(OpCodes.Call, module.ImportReference(property.GetMethod));
					return property.PropertyType;
				}
			}
			throw MakeError(memberAccess, $"Объект {allTypes.GetTypeName(objType)} не содержит поля или свойства {memberAccess.MemberName}");
		}
		public TypeRef VisitIndexAccess(IndexAccess indexAccess) {
			var objType = CompileExpression(indexAccess.Obj);
			var indexType = CompileExpression(indexAccess.Index);
			if (objType == allTypes.String && indexType == allTypes.Int) {
				var stringGetChars = typeof(string).GetMethod("get_Chars", new[] { typeof(int) });
				cil.Emit(OpCodes.Callvirt, module.ImportReference(stringGetChars));
				var charToString = typeof(char).GetMethod(nameof(char.ToString), new[] { typeof(char) });
				cil.Emit(OpCodes.Call, module.ImportReference(charToString));
				return allTypes.String;
			}
			if (objType.TypeReference is ArrayType arrayType && arrayType.Rank == 1 && indexType == allTypes.Int) {
				var elementType = new TypeRef(arrayType.ElementType);
				OpCode ldelemOpCode;
				if (elementType == allTypes.Bool) {
					ldelemOpCode = OpCodes.Ldelem_U1;
				}
				else if (elementType == allTypes.Int) {
					ldelemOpCode = OpCodes.Ldelem_I4;
				}
				else if (elementType.CanBeNull) {
					ldelemOpCode = OpCodes.Ldelem_Ref;
				}
				else {
					var typeName = allTypes.GetTypeName(elementType);
					throw MakeError(indexAccess, $"Неподдерживаемый тип элементов массива {typeName}");
				}
				cil.Emit(ldelemOpCode);
				return elementType;
			}
			{
				var methods = allTypes.GetCallableMethods(objType, "get_Item", new[] { indexType });
				if (methods.Count > 1) {
					throw MakeError(indexAccess, $"Не удалось вызвать индексатор для чтения с аргументом ({allTypes.GetTypeName(indexType)}), так как подошло несколько ({methods.Count})");
				}
				if (methods.Count != 0) {
					cil.Emit(OpCodes.Callvirt, methods[0]);
					return methods[0].ReturnType;
				}
				throw MakeError(indexAccess, $"Индексатор для чтения не был найден");
			}
		}
		public TypeRef VisitSliceAccess(SliceAccess sliceAccess) {
			var objType = CompileExpression(sliceAccess.Obj);
			if (objType == allTypes.String) {
				TypeRef beginIndexType;
				if (sliceAccess.BeginIndex == null) {
					cil.Emit(OpCodes.Ldc_I4, 0);
					beginIndexType = allTypes.Int;
				}
				else {
					beginIndexType = CompileExpression(sliceAccess.BeginIndex);
				}
				if (beginIndexType == allTypes.Int) {
					if (sliceAccess.EndIndex == null) {
						var substringMethod = typeof(string).GetMethod(nameof(string.Substring), new[] { typeof(int) });
						cil.Emit(OpCodes.Call, module.ImportReference(substringMethod));
						return allTypes.String;
					}
					var endIndexType = CompileExpression(sliceAccess.EndIndex);
					if (beginIndexType == allTypes.Int && endIndexType == allTypes.Int) {
						var sliceMethod = typeof(BuiltinFunctions).GetMethod(nameof(BuiltinFunctions.slice), new[] { typeof(string), typeof(int), typeof(int) });
						cil.Emit(OpCodes.Call, module.ImportReference(sliceMethod));
						return allTypes.String;
					}
				}
			}
			throw MakeError(sliceAccess, $"Невозоможно получить срез для {allTypes.GetTypeName(objType)}");
		}
		public TypeRef VisitSliceWithStepAccess(SliceWithStepAccess sliceWithStepAccess) {
			throw new NotImplementedException();
		}
		public TypeRef VisitTypedExpression(TypedExpression expression) {
			var exprType = CompileExpression(expression.Expr);
			var maybeExpectedType = allTypes.TryGetTypeRef(expression.Type);
			if (maybeExpectedType == null) {
				throw UnknownType(expression.Type);
			}
			var expectedType = maybeExpectedType.Value;
			if (exprType != expectedType) {
				throw UnexpectedType(expression.Expr, exprType, expectedType);
			}
			return expectedType;
		}
		#endregion
		Exception MakeError(INode node, string message) {
			return new Exception(sourceFile.MakeErrorMessage(node.Position, message));
		}
		Exception UnknownType(TypeNode typeNode) {
			throw MakeError(typeNode, $"Неизвестный тип {typeNode.FormattedString}");
		}
		Exception UnexpectedType(INode expression, TypeRef actual, TypeRef expected) {
			var actualName = allTypes.GetTypeName(actual);
			var expectedName = allTypes.GetTypeName(expected);
			var message = $"Выражение {expression.FormattedString} имеет тип {actualName} вместо {expectedName}";
			return MakeError(expression, message);
		}
		Exception UnassignableType(INode node, TypeRef sourceType, TypeRef targetType) {
			var sourceTypeName = allTypes.GetTypeName(sourceType);
			var targetTypeName = allTypes.GetTypeName(targetType);
			return MakeError(node, $"Нельзя присвоить {sourceTypeName} в {targetTypeName}");
		}
		Exception NoCommonType(INode node, TypeRef firstType, TypeRef secondType) {
			var firstTypeName = allTypes.GetTypeName(firstType);
			var secondTypeName = allTypes.GetTypeName(secondType);
			return MakeError(node, $"Отсутствует общий тип у {firstTypeName} и {secondTypeName}");
		}
	}
}
