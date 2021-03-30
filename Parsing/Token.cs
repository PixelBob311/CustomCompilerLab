namespace Lab4.Parsing {
	sealed class Token {
		public readonly int Position;
		public readonly TokenType Type;
		public readonly string Lexeme;
		public Token(int position, TokenType type, string lexeme) {
			Regexes.Instance.CheckToken(type, lexeme);
			Position = position;
			Type = type;
			Lexeme = lexeme;
		}
		public override string ToString() => $"{Type} \"{Lexeme}\"";
	}
}
