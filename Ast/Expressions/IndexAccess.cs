namespace Lab4.Ast.Expressions {
	sealed class IndexAccess : IExpression {
		public int Position { get; }
		public readonly IExpression Obj;
		public readonly IExpression Index;
		public IndexAccess(int position, IExpression obj, IExpression index) {
			Position = position;
			Obj = obj;
			Index = index;
		}
		public string FormattedString => $"{Obj.FormattedString}[{Index.FormattedString}]";
		public void Accept(IExpressionVisitor visitor) => visitor.VisitIndexAccess(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitIndexAccess(this);
	}
}
