using Lab4.Ast.Expressions;
namespace Lab4.Ast.Statements {
	sealed class While : IStatement {
		public int Position { get; }
		public readonly IExpression Condition;
		public readonly Block Body;
		public readonly Identifier Label;
		public While(int position, IExpression condition, Identifier label, Block body) {
			Position = position;
			Condition = condition;
			Label = label;
			Body = body;
		}
		public string FormattedString {
			get {
				var labelString = Label == null ? "" : $" as {Label.FormattedString}";
				return $"while ({Condition.FormattedString}){labelString} {Body.FormattedString}";
			}
		}
		public void Accept(IStatementVisitor visitor) => visitor.VisitWhile(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitWhile(this);
	}
}
