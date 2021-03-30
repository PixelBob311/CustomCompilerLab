using System.Collections.Generic;
using System.Linq;
namespace Lab4.Ast {
	sealed class Block : INode {
		public int Position { get; }
		public readonly IReadOnlyList<IStatement> Statements;
		public Block(int position, IReadOnlyList<IStatement> statements) {
			Position = position;
			Statements = statements;
		}
		public string FormattedString => "{\n" + string.Join("", Statements.Select(x => x.FormattedString)) + "}\n";
	}
}
