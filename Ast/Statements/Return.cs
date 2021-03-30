namespace Lab4.Ast.Statements {
	sealed class Return : IStatement {
		public int Position { get; }
		public readonly IExpression Value;
		public Return(int position, IExpression value) {
			Position = position;
			Value = value;
		}
		public string FormattedString => $"return{(Value == null ? "" : $" {Value.FormattedString}")};\n";
		public void Accept(IStatementVisitor visitor) => visitor.VisitReturn(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitReturn(this);
	}
}
