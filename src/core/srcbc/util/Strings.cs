using System;
using System.Text;

namespace Org.BouncyCastle.Utilities
{
	/// <summary> General string utilities.</summary>
	public sealed class Strings
	{
		private Strings()
		{
		}

		public static string FromByteArray(
			byte[] bs)
		{
			char[] cs = new char[bs.Length];
			for (int i = 0; i < cs.Length; ++i)
			{
				cs[i] = Convert.ToChar(bs[i]);
			}
			return new string(cs);
		}

		public static byte[] ToByteArray(
			string s)
		{
			byte[] bs = new byte[s.Length];
			for (int i = 0; i < bs.Length; ++i)
			{
				bs[i] = Convert.ToByte(s[i]);
			}
			return bs;
		}

		public static string FromUtf8ByteArray(
			byte[] bytes)
		{
			return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
		}

		public static byte[] ToUtf8ByteArray(
			string s)
		{
			return Encoding.UTF8.GetBytes(s);
		}

		public static byte[] ToUtf8ByteArray(
			char[] cs)
		{
			return Encoding.UTF8.GetBytes(cs);
		}
	}
}
