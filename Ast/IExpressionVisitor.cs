using Lab4.Ast.Expressions;
namespace Lab4.Ast {
	interface IExpressionVisitor {
		void VisitNumber(Number number);
		void VisitStringLiteral(StringLiteral stringLiteral);
		void VisitArrayLiteral(ArrayLiteral arrayLiteral);
		void VisitIdentifier(Identifier identifier);
		void VisitParentheses(Parentheses parentheses);
		void VisitBinary(Binary binary);
		void VisitTernary(Ternary ternary);
		void VisitCall(Call call);
		void VisitMemberAccess(MemberAccess memberAccess);
		void VisitIndexAccess(IndexAccess indexAccess);
		void VisitSliceAccess(SliceAccess sliceAccess);
		void VisitSliceWithStepAccess(SliceWithStepAccess sliceWithStepAccess);
		void VisitTypedExpression(TypedExpression typedExpression);
	}
	interface IExpressionVisitor<T> {
		T VisitNumber(Number number);
		T VisitStringLiteral(StringLiteral stringLiteral);
		T VisitArrayLiteral(ArrayLiteral arrayLiteral);
		T VisitIdentifier(Identifier identifier);
		T VisitParentheses(Parentheses parentheses);
		T VisitBinary(Binary binary);
		T VisitTernary(Ternary ternary);
		T VisitCall(Call call);
		T VisitMemberAccess(MemberAccess memberAccess);
		T VisitIndexAccess(IndexAccess indexAccess);
		T VisitSliceAccess(SliceAccess sliceAccess);
		T VisitSliceWithStepAccess(SliceWithStepAccess sliceWithStepAccess);
		T VisitTypedExpression(TypedExpression typedExpression);
	}
}
