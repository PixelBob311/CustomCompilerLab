using Lab4.Ast.Expressions;
namespace Lab4.Ast.Statements {
	sealed class Continue : IStatement {
		public int Position { get; }
		public Number NestingLevel;
		public Continue(int position, Number nestingLevel) {
			Position = position;
			NestingLevel = nestingLevel;
		}
		public string FormattedString => $"continue{(NestingLevel == null ? "" : $" {NestingLevel.FormattedString}")};\n";
		public void Accept(IStatementVisitor visitor) => visitor.VisitContinue(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitContinue(this);
	}
}
