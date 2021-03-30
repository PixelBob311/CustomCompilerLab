namespace Lab4.Ast {
	interface INode {
		int Position { get; }
		string FormattedString { get; }
	}
}
