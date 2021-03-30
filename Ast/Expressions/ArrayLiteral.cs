using System.Collections.Generic;
using System.Linq;
namespace Lab4.Ast.Expressions {
	sealed class ArrayLiteral : IExpression {
		public int Position { get; }
		public readonly IReadOnlyList<IExpression> Elements;
		public ArrayLiteral(int position, IReadOnlyList<IExpression> elements) {
			Position = position;
			Elements = elements;
		}
		public string FormattedString => $"[{string.Join(", ", Elements.Select(x => x.FormattedString))}]";
		public void Accept(IExpressionVisitor visitor) => visitor.VisitArrayLiteral(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitArrayLiteral(this);
	}
}
