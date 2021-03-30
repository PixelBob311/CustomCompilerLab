namespace Lab4.Ast.ClassMembers {
	sealed class ClassField : IClassMember {
		public int Position { get; }
		public readonly TypeNode Type;
		public readonly string Name;
		public readonly bool IsReadOnly;
		public ClassField(int position, TypeNode type, string name, bool isReadOnly) {
			Position = position;
			Type = type;
			Name = name;
			IsReadOnly = isReadOnly;
		}
		public string FormattedString {
			get {
				return (IsReadOnly ? "readonly " : "") + $"{Type.FormattedString} {Name};\n";
			}
		}
	}
}
