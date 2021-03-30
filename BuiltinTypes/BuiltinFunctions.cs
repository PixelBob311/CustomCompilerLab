using System;
namespace BuiltinTypes {
	public static class BuiltinFunctions {
		public static void dump() {
			Console.WriteLine();
		}
		public static void dump(int v) {
			Console.WriteLine(v);
		}
		public static void dump(bool v) {
			Console.WriteLine(v ? "true" : "false");
		}
		public static void dump(string v) {
			if (v == null) {
				Console.WriteLine("null");
				return;
			}
			Console.WriteLine($"'{v.Replace("'", "''")}'");
		}
		public static int trace(int v) {
			Console.Write($">> ");
			dump(v);
			return v;
		}
		public static bool trace(bool v) {
			Console.Write($">> ");
			dump(v);
			return v;
		}
		public static string trace(string v) {
			Console.Write($">> ");
			dump(v);
			return v;
		}
		public static string chr(int v) {
			return char.ToString(Convert.ToChar(v));
		}
		public static int len(string v) {
			return v.Length;
		}
		public static int ord(string v) {
			return Convert.ToChar(v);
		}
		public static string slice(string s, int beginIndex, int endIndex) {
			return s.Substring(beginIndex, endIndex - beginIndex);
		}
		public class _test_Indexer {
			private int[] arr;
			public _test_Indexer(int[] ar) {
				arr = new int[ar.Length];
				for (int i = 0; i < ar.Length; i++) {
					arr[i] = ar[i];
				}
			}
			public int this[int i] {
				get { return arr[i]; }
				set { arr[i] = value; }
			}
			public int this[string i] {
				get { return arr[int.Parse(i)]; }
				set { arr[int.Parse(i)] = value; }
			}
		}
		public static _test_Indexer _test_makeIndexer(int[] v) {
			return new _test_Indexer(v);
		}
		public static System.Collections.ArrayList _test_makeArrayList(int[] v) {
			return new System.Collections.ArrayList(v);
		}
		public static void _test_dumpObject(object o) {
			Console.WriteLine(o);
		}
		public static object _test_toObject(int v) {
			return v;
		}
		public sealed class _test_TupleIntString {
			public _test_TupleIntString(int item1, string item2) {
				Item1 = item1;
				Item2 = item2;
			}
			public int Item1 { get; }
			public string Item2 { get; }
		}
		public static _test_TupleIntString _test_makeTuple(int item1, string item2) {
			return new _test_TupleIntString(item1, item2);
		}
		public sealed class _test_Readonly {
			public _test_Readonly(int item1, int item2) {
				field1 = item1;
				field2 = item2;
			}
			public readonly int field1;
			public int field2;
		}
		public static _test_Readonly _test_ReadonlyClass() {
			return new _test_Readonly(1, 2);
		}
	}
}
