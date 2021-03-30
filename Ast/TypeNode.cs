namespace Lab4.Ast {
	abstract class TypeNode : INode {
		public abstract int Position { get; }
		public abstract string FormattedString { get; }
	}
}
