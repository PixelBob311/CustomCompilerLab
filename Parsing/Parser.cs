using Lab4.Ast;
using Lab4.Ast.ClassMembers;
using Lab4.Ast.Declarations;
using Lab4.Ast.Expressions;
using Lab4.Ast.Statements;
using Lab4.Ast.TypeNodes;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Lab4.Parsing {
	sealed class Parser {
		readonly SourceFile sourceFile;
		readonly IReadOnlyList<Token> tokens;
		int tokenIndex = 0;
		Token CurrentToken => tokens[tokenIndex];
		int CurrentPosition => CurrentToken.Position;
		Parser(SourceFile sourceFile, IReadOnlyList<Token> tokens) {
			this.sourceFile = sourceFile;
			this.tokens = tokens;
		}
		#region stuff
		string[] DebugCurrentPosition => sourceFile.FormatLines(CurrentPosition,
			inlinePointer: true,
			pointer: " <|> "
			).ToArray();
		string DebugCurrentLine => string.Join("", sourceFile.FormatLines(CurrentPosition,
			linesAround: 0,
			inlinePointer: true,
			pointer: " <|> "
			).ToArray());
		static bool IsNotWhitespace(Token t) {
			switch (t.Type) {
				case TokenType.Whitespaces:
				case TokenType.SingleLineComment:
				case TokenType.MultiLineComment:
					return false;
				default:
					return true;
			}
		}
		void ExpectEof() {
			if (CurrentToken.Type != TokenType.EnfOfFile) {
				throw MakeError($"Не допарсили до конца, остался {CurrentToken}");
			}
		}
		void ReadNextToken() {
			tokenIndex += 1;
		}
		void Reset() {
			tokenIndex = 0;
		}
		Exception MakeError(string message) {
			return new Exception(sourceFile.MakeErrorMessage(CurrentPosition, message));
		}
		bool SkipIf(string s) {
			if (CurrentToken.Lexeme == s) {
				ReadNextToken();
				return true;
			}
			return false;
		}
		void Expect(string s) {
			if (!SkipIf(s)) {
				throw MakeError($"Ожидали \"{s}\", получили {CurrentToken}");
			}
		}
		#endregion
		public static ProgramNode Parse(SourceFile sourceFile) {
			var eof = new Token(sourceFile.Text.Length, TokenType.EnfOfFile, "");
			var tokens = Lexer.GetTokens(sourceFile).Concat(new[] { eof }).Where(IsNotWhitespace).ToList();
			var parser = new Parser(sourceFile, tokens);
			return parser.ParseProgram();
		}
		ProgramNode ParseProgram() {
			Reset();
			var declarations = new List<IDeclaration>();
			while (true) {
				var declaration = TryParseDeclaration();
				if (declaration == null) {
					break;
				}
				declarations.Add(declaration);
			}
			var statements = new List<IStatement>();
			while (CurrentToken.Type != TokenType.EnfOfFile) {
				statements.Add(ParseStatement());
			}
			var result = new ProgramNode(sourceFile, declarations, statements);
			ExpectEof();
			return result;
		}
		IDeclaration TryParseDeclaration() {
			var pos = CurrentPosition;
			if (SkipIf(ClassDeclaration.Keyword)) {
				var name = ParseIdentifier();
				var members = ParseClassMembers();
				return new ClassDeclaration(pos, name, members);
			}
			if (SkipIf(FunctionDeclaration.Keyword)) {
				var type = ParseType();
				var name = ParseIdentifier();
				var parameters = ParseParameters();
				var body = ParseBlock();
				return new FunctionDeclaration(pos, type, name, parameters, body);
			}
			return null;
		}
		IReadOnlyList<IClassMember> ParseClassMembers() {
			Expect("{");
			var members = new List<IClassMember>();
			while (!SkipIf("}")) {
				members.Add(ParseClassMember());
			}
			return members;
		}
		IClassMember ParseClassMember() {
			var pos = CurrentPosition;
			var isReadOnly = SkipIf("readonly");
			var type = ParseType();
			var name = ParseIdentifier();
			if (SkipIf(";")) {
				return new ClassField(pos, type, name, isReadOnly);
			}
			if (isReadOnly) {
				throw MakeError($"Метод не может быть readonly");
			}
			var parameters = ParseParameters();
			var body = ParseBlock();
			return new ClassMethod(pos, type, name, parameters, body);
		}
		IReadOnlyList<Parameter> ParseParameters() {
			Expect("(");
			var parameters = new List<Parameter>();
			if (!SkipIf(")")) {
				parameters.Add(ParseParameter());
				while (SkipIf(",")) {
					parameters.Add(ParseParameter());
				}
				Expect(")");
			}
			return parameters;
		}
		Parameter ParseParameter() {
			var type = ParseType();
			var pos = CurrentPosition;
			var name = ParseIdentifier();
			return new Parameter(pos, type, name);
		}
		Block ParseBlock() {
			var pos = CurrentPosition;
			Expect("{");
			var statements = new List<IStatement>();
			while (!SkipIf("}")) {
				statements.Add(ParseStatement());
			}
			return new Block(pos, statements);
		}
		IStatement ParseStatement() {
			var pos = CurrentPosition;
			if (SkipIf("if")) {
				Expect("(");
				var condition = ParseExpression();
				Expect(")");
				var block = ParseBlock();
				return new If(pos, condition, block);
			}
			if (SkipIf("while")) {
				Expect("(");
				var condition = ParseExpression();
				Expect(")");
				Identifier label = null;
				if (SkipIf("as")) {
					label = new Identifier(CurrentPosition, ParseIdentifier());
				}
				var block = ParseBlock();
				return new While(pos, condition, label, block);
			}
			if (SkipIf("return")) {
				IExpression value = null;
				if (!SkipIf(";")) {
					value = ParseExpression();
					Expect(";");
				}
				return new Return(pos, value);
			}
			if (SkipIf("var")) {
				var variable = ParseIdentifier();
				TypeNode type = null;
				if (SkipIf(":")) {
					type = ParseType();
				}
				Expect("=");
				var expression = ParseExpression();
				Expect(";");
				return new VariableDeclaration(pos, variable, type, expression);
			}
			if (SkipIf("break")) {
				IExpression target = null;
				if (!SkipIf(";")) {
					target = ParseExpression();
					Expect(";");
				}
				return new Break(pos, target);
			}
			if (SkipIf("continue")) {
				var nestingLevel = TryParseNumber();
				Expect(";");
				return new Continue(pos, nestingLevel);
			}
			var leftExpression = ParseExpression();
			if (SkipIf("=")) {
				var rightExpression = ParseExpression();
				Expect(";");
				return new Assignment(pos, leftExpression, rightExpression);
			}
			else {
				Expect(";");
				return new ExpressionStatement(pos, leftExpression);
			}
		}
		TypeNode ParseType() {
			if (SkipIf("(")) {
				var type = new ParenthesesTypeNode(CurrentPosition, ParseType());
				Expect(")");
				return type;
			}
			return new SimpleTypeNode(CurrentPosition, ParseIdentifier());
		}
		string ParseIdentifier() {
			if (CurrentToken.Type != TokenType.Identifier) {
				throw MakeError($"Ожидали идентификатор, получили {CurrentToken}");
			}
			var lexeme = CurrentToken.Lexeme;
			ReadNextToken();
			return lexeme;
		}
		#region expressions
		IExpression ParseExpression() {
			return ParseTernaryExpression();
		}
		IExpression ParseExpressionType(IExpression expression) {
			var pos = CurrentPosition;
			if (SkipIf("::")) {
				return new TypedExpression(pos, expression, ParseType());
			}
			return expression;
		}
		IExpression ParseTernaryExpression() {
			var pos = CurrentPosition;
			var expression = ParseOrExpression();
			if (SkipIf("?")) {
				var consequent = ParseExpression();
				Expect(":");
				var alternative = ParseExpression();
				return ParseExpressionType(new Ternary(pos, expression, consequent, alternative));
			}
			return expression;
		}
		IExpression ParseOrExpression() {
			var left = ParseAndExpression();
			while (true) {
				var pos = CurrentPosition;
				if (SkipIf("||")) {
					var right = ParseAndExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Or, right));
				}
				else {
					break;
				}
			}
			return left;
		}
		IExpression ParseAndExpression() {
			var left = ParseEqualityExpression();
			while (true) {
				var pos = CurrentPosition;
				if (SkipIf("&&")) {
					var right = ParseEqualityExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.And, right));
				}
				else {
					break;
				}
			}
			return left;
		}
		IExpression ParseEqualityExpression() {
			var left = ParseRelationalExpression();
			while (true) {
				var pos = CurrentPosition;
				if (SkipIf("==")) {
					var right = ParseRelationalExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Equal, right));
				}
				if (SkipIf("!=")) {
					var right = ParseRelationalExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.NotEqual, right));
				}
				else {
					break;
				}
			}
			return left;
		}
		IExpression ParseRelationalExpression() {
			var left = ParseAdditiveExpression();
			while (true) {
				var pos = CurrentPosition;
				if (SkipIf("<")) {
					var right = ParseAdditiveExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Less, right));
				}
				if (SkipIf("<=")) {
					var right = ParseAdditiveExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.LessEqual, right));
				}
				if (SkipIf(">")) {
					var right = ParseAdditiveExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Greater, right));
				}
				if (SkipIf(">=")) {
					var right = ParseAdditiveExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.GreaterEqual, right));
				}
				else {
					break;
				}
			}
			return left;
		}
		IExpression ParseAdditiveExpression() {
			var left = ParseMultiplicativeExpression();
			while (true) {
				var pos = CurrentPosition;
				if (SkipIf("+")) {
					var right = ParseMultiplicativeExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Addition, right));
				}
				else if (SkipIf("-")) {
					var right = ParseMultiplicativeExpression();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Subtraction, right));
				}
				else {
					break;
				}
			}
			return left;
		}
		IExpression ParseMultiplicativeExpression() {
			var left = ParsePrimary();
			while (true) {
				var pos = CurrentPosition;
				if (SkipIf("*")) {
					var right = ParsePrimary();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Multiplication, right));
				}
				else if (SkipIf("/")) {
					var right = ParsePrimary();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Division, right));
				}
				else if (SkipIf("%")) {
					var right = ParsePrimary();
					left = ParseExpressionType(new Binary(pos, left, BinaryOperator.Remainder, right));
				}
				else {
					break;
				}
			}
			return left;
		}
		IExpression ParsePrimary() {
			var expression = ParsePrimitive();
			while (true) {
				int pos = CurrentPosition;
				if (SkipIf("(")) {
					var arguments = new List<IExpression>();
					if (!SkipIf(")")) {
						arguments.Add(ParseExpression());
						while (SkipIf(",")) {
							arguments.Add(ParseExpression());
						}
						Expect(")");
					}
					expression = ParseExpressionType(new Call(pos, expression, arguments));
				}
				else if (SkipIf(".")) {
					var memberName = ParseIdentifier();
					expression = ParseExpressionType(new MemberAccess(pos, expression, memberName));
				}
				else if (SkipIf("[")) {
					if (SkipIf(":")) {
						if (SkipIf("]")) {
							expression = ParseExpressionType(new SliceAccess(pos, expression, null, null));
						}
						else if (SkipIf(":")) {
							if (SkipIf("]")) {
								expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, null, null, null));
							}
							else {
								var step = ParseExpression();
								Expect("]");
								expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, null, null, step));
							}
						}
						else {
							var endIndex = ParseExpression();
							if (SkipIf("]")) {
								expression = ParseExpressionType(new SliceAccess(pos, expression, null, endIndex));
							}
							else {
								Expect(":");
								if (SkipIf("]")) {
									expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, null, endIndex, null));
								}
								else {
									var step = ParseExpression();
									Expect("]");
									expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, null, endIndex, step));
								}
							}
						}
					}
					else {
						var beginIndex = ParseExpression();
						if (SkipIf(":")) {
							if (SkipIf("]")) {
								expression = ParseExpressionType(new SliceAccess(pos, expression, beginIndex, null));
							}
							else if (SkipIf(":")) {
								if (SkipIf("]")) {
									expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, beginIndex, null, null));
								}
								else {
									var step = ParseExpression();
									Expect("]");
									expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, beginIndex, null, step));
								}
							}
							else {
								var endIndex = ParseExpression();
								if (SkipIf("]")) {
									expression = ParseExpressionType(new SliceAccess(pos, expression, beginIndex, endIndex));
								}
								else {
									Expect(":");
									if (SkipIf("]")) {
										expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, beginIndex, endIndex, null));
									}
									else {
										var step = ParseExpression();
										Expect("]");
										expression = ParseExpressionType(new SliceWithStepAccess(pos, expression, beginIndex, endIndex, step));
									}
								}
							}
						}
						else {
							Expect("]");
							expression = ParseExpressionType(new IndexAccess(pos, expression, beginIndex));
						}
					}
				}
				else {
					break;
				}
			}
			return expression;
		}
		IExpression ParsePrimitive() {
			var pos = CurrentPosition;
			if (CurrentToken.Type == TokenType.NumberLiteral) {
				var lexeme = CurrentToken.Lexeme;
				ReadNextToken();
				return ParseExpressionType(new Number(pos, lexeme));
			}
			if (CurrentToken.Type == TokenType.StringLiteral) {
				var lexeme = CurrentToken.Lexeme;
				ReadNextToken();
				return ParseExpressionType(new StringLiteral(pos, lexeme));
			}
			if (CurrentToken.Type == TokenType.Identifier) {
				var lexeme = CurrentToken.Lexeme;
				ReadNextToken();
				return ParseExpressionType(new Identifier(pos, lexeme));
			}
			if (SkipIf("(")) {
				var parentheses = new Parentheses(pos, ParseExpression());
				Expect(")");
				return ParseExpressionType(parentheses);
			}
			if (SkipIf("[")) {
				var elements = new List<IExpression>();
				if (!SkipIf("]")) {
					elements.Add(ParseExpression());
					while (SkipIf(",")) {
						elements.Add(ParseExpression());
					}
					Expect("]");
				}
				return new ArrayLiteral(pos, elements);
			}
			throw MakeError($"Ожидали идентификатор, число или открывающую скобку, получили {CurrentToken}");
		}
		Number TryParseNumber() {
			var pos = CurrentPosition;
			if (CurrentToken.Type == TokenType.NumberLiteral) {
				var lexeme = CurrentToken.Lexeme;
				ReadNextToken();
				return new Number(pos, lexeme);
			}
			return null;
		}
		#endregion
	}
}
