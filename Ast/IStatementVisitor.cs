using Lab4.Ast.Statements;
namespace Lab4.Ast {
	interface IStatementVisitor {
		void VisitExpressionStatement(ExpressionStatement expressionStatement);
		void VisitVariableDeclaration(VariableDeclaration variableDeclaration);
		void VisitAssignment(Assignment assignment);
		void VisitIf(If ifStatement);
		void VisitWhile(While whileStatement);
		void VisitReturn(Return returnStatement);
		void VisitBreak(Break breakStatement);
		void VisitContinue(Continue continueStatement);
	}
	interface IStatementVisitor<T> {
		T VisitExpressionStatement(ExpressionStatement expressionStatement);
		T VisitVariableDeclaration(VariableDeclaration variableDeclaration);
		T VisitAssignment(Assignment assignment);
		T VisitIf(If ifStatement);
		T VisitWhile(While whileStatement);
		T VisitReturn(Return returnStatement);
		T VisitBreak(Break breakStatement);
		T VisitContinue(Continue continueStatement);
	}
}
