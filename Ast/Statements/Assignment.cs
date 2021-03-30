namespace Lab4.Ast.Statements {
	sealed class Assignment : IStatement {
		public int Position { get; }
		public readonly IExpression Target;
		public readonly IExpression Value;
		public Assignment(int position, IExpression target, IExpression value) {
			Position = position;
			Target = target;
			Value = value;
		}
		public string FormattedString => $"{Target.FormattedString} = {Value.FormattedString};\n";
		public void Accept(IStatementVisitor visitor) => visitor.VisitAssignment(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitAssignment(this);
	}
}
