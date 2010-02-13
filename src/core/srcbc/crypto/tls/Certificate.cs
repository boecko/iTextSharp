using System;
using System.Collections;
using System.IO;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;

namespace Org.BouncyCastle.Crypto.Tls
{
	/**
	* A representation for a certificate chain as used by an tls server.
	*/
	public class Certificate
	{
		/**
		* The certificates.
		*/
		internal X509CertificateStructure[] certs;

		/**
		* Parse the ServerCertificate message.
		*
		* @param inStr The stream where to parse from.
		* @return A Certificate object with the certs, the server has sended.
		* @throws IOException If something goes wrong during parsing.
		*/
		internal static Certificate Parse(
			Stream inStr)
		{
			X509CertificateStructure[] certs;
			int left = TlsUtilities.ReadUint24(inStr);
			ArrayList tmp = new ArrayList();
			while (left > 0)
			{
				int size = TlsUtilities.ReadUint24(inStr);
				left -= 3 + size;
				byte[] buf = new byte[size];
				TlsUtilities.ReadFully(buf, inStr);
				MemoryStream bis = new MemoryStream(buf, false);
				Asn1Object o = Asn1Object.FromStream(bis);
				tmp.Add(X509CertificateStructure.GetInstance(o));
				if (bis.Position < bis.Length)
				{
					throw new ArgumentException("Sorry, there is garbage data left after the certificate");
				}
			}
			certs = (X509CertificateStructure[]) tmp.ToArray(typeof(X509CertificateStructure));
			return new Certificate(certs);
		}

		/**
		 * Encodes version of the ClientCertificate message
		 * 
		 * @param outStr stream to write the message to
		 * @throws IOException If something goes wrong
		 */
		internal void Encode(
			Stream outStr)
		{
			ArrayList encCerts = new ArrayList();
			int totalSize = 0;
			foreach (X509CertificateStructure cert in certs)
			{
				byte[] encCert = cert.GetEncoded(Asn1Encodable.Der);
				encCerts.Add(encCert);
				totalSize += encCert.Length + 3;
			}

			TlsUtilities.WriteUint24(totalSize + 3, outStr);
			TlsUtilities.WriteUint24(totalSize, outStr);

			foreach (byte[] encCert in encCerts)
			{
				TlsUtilities.WriteOpaque24(encCert, outStr);
			}
		}
		
		/**
		* Private constructor from a cert array.
		*
		* @param certs The certs the chain should contain.
		*/
		// TODO Make public to enable client certificate support
		internal Certificate(
			X509CertificateStructure[] certs)
		{
			this.certs = certs;
		}

		/// <returns>An array which contains the certs, this chain contains.</returns>
		public X509CertificateStructure[] GetCerts()
		{
			return (X509CertificateStructure[]) certs.Clone();
		}
	}
}
