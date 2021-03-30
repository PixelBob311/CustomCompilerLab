namespace Lab4.Ast.Statements {
	sealed class VariableDeclaration : IStatement {
		public int Position { get; }
		public readonly string VariableName;
		public readonly TypeNode Type;
		public readonly IExpression InitialValue;
		public VariableDeclaration(int position, string variableName, TypeNode type, IExpression initialValue) {
			Position = position;
			VariableName = variableName;
			Type = type;
			InitialValue = initialValue;
		}
		public string FormattedString {
			get {
				var type = Type != null ? $" : {Type.FormattedString}" : "";
				return $"var {VariableName}{type} = {InitialValue.FormattedString};\n";
			}
		}
		public void Accept(IStatementVisitor visitor) => visitor.VisitVariableDeclaration(this);
		public T Accept<T>(IStatementVisitor<T> visitor) => visitor.VisitVariableDeclaration(this);
	}
}
