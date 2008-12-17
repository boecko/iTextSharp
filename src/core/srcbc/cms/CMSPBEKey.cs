using System;

using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities;

//import javax.crypto.interfaces.PBEKey;

namespace Org.BouncyCastle.Cms
{
	public abstract class CmsPbeKey
		// TODO Create an equivalent interface somewhere?
		//	: PBEKey
		: ICipherParameters
	{
		private readonly string	password;
		private readonly byte[]	salt;
		private readonly int	iterationCount;

		public CmsPbeKey(
			string	password,
			byte[]	salt,
			int		iterationCount)
		{
			this.password = password;
			this.salt = Arrays.Clone(salt);
			this.iterationCount = iterationCount;
		}

		public CmsPbeKey(
			string				password,
			AlgorithmIdentifier keyDerivationAlgorithm)
		{
			if (!keyDerivationAlgorithm.ObjectID.Equals(PkcsObjectIdentifiers.IdPbkdf2))
				throw new ArgumentException("Unsupported key derivation algorithm: "
					+ keyDerivationAlgorithm.ObjectID);

			Pbkdf2Params kdfParams = Pbkdf2Params.GetInstance(
				keyDerivationAlgorithm.Parameters.ToAsn1Object());

			this.password = password;
			this.salt = kdfParams.GetSalt();
			this.iterationCount = kdfParams.IterationCount.IntValue;
		}

		public string Password
		{
			get { return password; }
		}

		public byte[] Salt
		{
			get { return Arrays.Clone(salt); }
		}

		[Obsolete("Use 'Salt' property instead")]
		public byte[] GetSalt()
		{
			return Salt;
		}

		public int IterationCount
		{
			get { return iterationCount; }
		}

		public string Algorithm
		{
			get { return "PKCS5S2"; }
		}

		public string Format
		{
			get { return "RAW"; }
		}

		public byte[] GetEncoded()
		{
			return null;
		}

		internal abstract KeyParameter GetEncoded(string algorithmOid);
	}
}
