namespace Lab4.Ast.Expressions {
	sealed class SliceWithStepAccess : IExpression {
		public int Position { get; }
		public readonly IExpression Obj;
		public readonly IExpression BeginIndex;
		public readonly IExpression EndIndex;
		public readonly IExpression Step;
		public SliceWithStepAccess(int position, IExpression obj, IExpression beginIndex, IExpression endIndex, IExpression step) {
			Position = position;
			Obj = obj;
			BeginIndex = beginIndex;
			EndIndex = endIndex;
			Step = step;
		}
		public string FormattedString {
			get {
				var beginIndex = BeginIndex == null ? "" : BeginIndex.FormattedString;
				var endIndex = EndIndex == null ? " " : EndIndex.FormattedString;
				var step = Step == null ? "" : Step.FormattedString;
				return $"{Obj.FormattedString}[{beginIndex}:{endIndex}:{step}]";
			}
		}
		public void Accept(IExpressionVisitor visitor) => visitor.VisitSliceWithStepAccess(this);
		public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.VisitSliceWithStepAccess(this);
	}
}
