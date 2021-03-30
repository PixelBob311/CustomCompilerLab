namespace Lab4.Ast.Expressions {
	sealed class StringLiteral : IExpression {
		public int Position { get; }
		public readonly string Lexeme;
		public StringLiteral(int position, string lexeme) {
			Position = position;
			Lexeme = lexeme;
		}
		public string FormattedString => Lexeme;
		public void Accept(IExpressionVisitor visitor) => visitor.VisitStringLiteral(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitStringLiteral(this);
	}
}
