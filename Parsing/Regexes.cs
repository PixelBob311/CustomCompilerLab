using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
namespace Lab4.Parsing {
	sealed class Regexes {
		static readonly IReadOnlyList<Tuple<TokenType, string>> tokenPatterns = new TupleList<TokenType, string> {
			{ TokenType.Whitespaces, @"[ \t\r\n]+" },
			{ TokenType.SingleLineComment, @"//[^\r\n]*" },
			{ TokenType.MultiLineComment, @"/\*.*?\*/" },
			{ TokenType.Identifier, @"[a-zA-Z_][a-zA-Z_0-9]*" },
			{ TokenType.NumberLiteral, @"[0-9]+" },
			{ TokenType.StringLiteral, @"'(''|[^'])*'" },
			{ TokenType.OperatorOrPunctuator, @"<=|>=|==|!=|::|&&|\|\||[-+*/%.<>,=;:(){}?\[\]]" },
		};
		static Regexes instance;
		public static Regexes Instance => instance ?? (instance = new Regexes());
		public readonly Regex CombinedRegex;
		public readonly IEnumerable<Tuple<TokenType, string>> TokenGroupNames;
		public readonly IReadOnlyDictionary<TokenType, Regex> TokenRegexes;
		Regexes() {
			var tokenRegexes = new Dictionary<TokenType, Regex>();
			foreach (var tp in tokenPatterns) {
				var tokenType = tp.Item1;
				var pattern = tp.Item2;
				var regex = RegexUtils.CreateRegex(@"\A(" + pattern + @")\z");
				tokenRegexes.Add(tokenType, regex);
			}
			TokenRegexes = tokenRegexes;
			var combinedPattern = string.Join("\n|\n", tokenPatterns.Select(x => $"(?<{x.Item1}>{x.Item2})"));
			CombinedRegex = RegexUtils.CreateRegex(combinedPattern);
			TokenGroupNames = tokenPatterns.Select(x => Tuple.Create(x.Item1, x.Item1.ToString())).ToArray();
		}
		[Conditional("DEBUG")]
		public void CheckToken(TokenType type, string lexeme) {
			if (type == TokenType.EnfOfFile) {
				return;
			}
			var regex = TokenRegexes[type];
			if (!regex.IsMatch(lexeme)) {
				throw new Exception($"Лексема \"{lexeme}\" не подходит под регулярку {regex}");
			}
		}
	}
}
