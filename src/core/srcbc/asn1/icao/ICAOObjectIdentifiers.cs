using System;

namespace Org.BouncyCastle.Asn1.Icao
{
	public abstract class IcaoObjectIdentifiers
	{
		//
		// base id
		//
		public const string IdIcao = "2.23.136";

		public static readonly DerObjectIdentifier IdIcaoMrtd				= new DerObjectIdentifier(IdIcao + ".1");
		public static readonly DerObjectIdentifier IdIcaoMrtdSecurity		= new DerObjectIdentifier(IdIcaoMrtd + ".1");
		public static readonly DerObjectIdentifier IdIcaoLdsSecurityObject	= new DerObjectIdentifier(IdIcaoMrtdSecurity + ".1");
	}
}
