namespace Lab4.Ast.TypeNodes {
	sealed class SimpleTypeNode : TypeNode {
		public override int Position { get; }
		public string Name { get; }
		public SimpleTypeNode(int position, string name) {
			Position = position;
			Name = name;
		}
		public override string FormattedString => Name;
	}
}
