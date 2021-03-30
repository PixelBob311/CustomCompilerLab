namespace Lab4.Ast.TypeNodes {
	sealed class ParenthesesTypeNode : TypeNode {
		public override int Position { get; }
		public TypeNode Type { get; }
		public ParenthesesTypeNode(int position, TypeNode type) {
			Position = position;
			Type = type;
		}
		public override string FormattedString => $"({Type.FormattedString})";
	}
}
