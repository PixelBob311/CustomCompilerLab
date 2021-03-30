namespace Lab4.Ast.Expressions {
	sealed class Ternary : IExpression {
		public int Position { get; }
		public readonly IExpression Condition;
		public readonly IExpression Consequent;
		public readonly IExpression Alternative;
		public Ternary(int position, IExpression condition, IExpression consequent, IExpression alternative) {
			Position = position;
			Condition = condition;
			Consequent = consequent;
			Alternative = alternative;
		}
		public string FormattedString => $"{Condition.FormattedString} ? {Consequent.FormattedString} : {Alternative.FormattedString}";
		public void Accept(IExpressionVisitor visitor) => visitor.VisitTernary(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitTernary(this);
	}
}
