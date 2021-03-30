namespace Lab4.Ast.Expressions {
	sealed class MemberAccess : IExpression {
		public int Position { get; }
		public readonly IExpression Obj;
		public readonly string MemberName;
		public MemberAccess(int position, IExpression obj, string memberName) {
			Position = position;
			Obj = obj;
			MemberName = memberName;
		}
		public string FormattedString => $"{Obj.FormattedString}.{MemberName}";
		public void Accept(IExpressionVisitor visitor) => visitor.VisitMemberAccess(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitMemberAccess(this);
	}
}
