namespace Lab4.Ast.Statements {
	sealed class Break : IStatement {
		public int Position { get; }
		public IExpression Target;
		public Break(int position, IExpression target) {
			Position = position;
			Target = target;
		}
		public string FormattedString => $"break{(Target == null ? "" : $" {Target.FormattedString}")};\n";
		public void Accept(IStatementVisitor visitor) => visitor.VisitBreak(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitBreak(this);
	}
}
