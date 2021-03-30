using System;
using System.Collections.Generic;
namespace Lab4.Parsing {
	static class Lexer {
		public static IEnumerable<Token> GetTokens(SourceFile sourceFile) {
			var regex = Regexes.Instance.CombinedRegex;
			var groupNames = Regexes.Instance.TokenGroupNames;
			int lastPos = 0;
			Exception MakeError(string message) {
				return new Exception(sourceFile.MakeErrorMessage(lastPos, message));
			}
			var text = sourceFile.Text;
			for (var m = regex.Match(text); m.Success; m = m.NextMatch()) {
				if (lastPos < m.Index) {
					throw MakeError($"Пропустили '{text.Substring(lastPos, m.Index - lastPos)}'");
				}
				bool found = false;
				foreach (var kv in groupNames) {
					if (m.Groups[kv.Item2].Success) {
						if (found) {
							throw new Exception("Кривая регулярка нашла несколько вхождений");
						}
						found = true;
						yield return new Token(m.Index, kv.Item1, m.Value);
					}
				}
				if (!found) {
					throw new Exception("Кривая регулярка");
				}
				lastPos = m.Index + m.Length;
			}
			if (lastPos < text.Length) {
				throw MakeError($"Пропустили '{text.Substring(lastPos)}'");
			}
		}
	}
}
