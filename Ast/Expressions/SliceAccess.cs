namespace Lab4.Ast.Expressions {
	sealed class SliceAccess : IExpression {
		public int Position { get; }
		public readonly IExpression Obj;
		public readonly IExpression BeginIndex;
		public readonly IExpression EndIndex;
		public SliceAccess(int position, IExpression obj, IExpression beginIndex, IExpression endIndex) {
			Position = position;
			Obj = obj;
			BeginIndex = beginIndex;
			EndIndex = endIndex;
		}
		public string FormattedString {
			get {
				var beginIndex = BeginIndex == null ? "" : BeginIndex.FormattedString;
				var endIndex = EndIndex == null ? "" : EndIndex.FormattedString;
				return $"{Obj.FormattedString}[{beginIndex}:{endIndex}]";
			}
		}
		public void Accept(IExpressionVisitor visitor) => visitor.VisitSliceAccess(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitSliceAccess(this);
	}
}
