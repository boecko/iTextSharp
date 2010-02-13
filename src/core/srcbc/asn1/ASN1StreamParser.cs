using System;
using System.IO;

namespace Org.BouncyCastle.Asn1
{
	public class Asn1StreamParser
	{
		private readonly Stream _in;
		private readonly int _limit;

		private static int findLimit(Stream inStream)
		{
			if (inStream is DefiniteLengthInputStream)
			{
				return ((DefiniteLengthInputStream)inStream).Remaining;
			}

			return int.MaxValue;
		}

		public Asn1StreamParser(
			Stream inStream)
			: this(inStream, findLimit(inStream))
		{
		}

		public Asn1StreamParser(
			Stream	inStream,
			int		limit)
		{
			if (!inStream.CanRead)
				throw new ArgumentException("Expected stream to be readable", "inStream");

			this._in = inStream;
			this._limit = limit;
		}

		public Asn1StreamParser(
			byte[] encoding)
			: this(new MemoryStream(encoding, false), encoding.Length)
		{
		}

		public virtual IAsn1Convertible ReadObject()
		{
			int tag = _in.ReadByte();
			if (tag == -1)
				return null;

			// turn of looking for "00" while we resolve the tag
			Set00Check(false);

			//
			// calculate tag number
			//
			int tagNo = Asn1InputStream.ReadTagNumber(_in, tag);

			bool isConstructed = (tag & Asn1Tags.Constructed) != 0;

			//
			// calculate length
			//
			int length = Asn1InputStream.ReadLength(_in, _limit);

			if (length < 0) // indefinite length method
			{
				if (!isConstructed)
					throw new IOException("indefinite length primitive encoding encountered");

				IndefiniteLengthInputStream indIn = new IndefiniteLengthInputStream(_in);

				if ((tag & Asn1Tags.Application) != 0)
				{
					Asn1StreamParser sp2 = new Asn1StreamParser(indIn, _limit);

					return new BerApplicationSpecificParser(tagNo, sp2);
				}

				if ((tag & Asn1Tags.Tagged) != 0)
				{
					// TODO Investigate passing an Asn1StreamParser into this constructor
					return new BerTaggedObjectParser(tag, tagNo, indIn);
				}

				Asn1StreamParser sp = new Asn1StreamParser(indIn, _limit);

				// TODO There are other tags that may be constructed (e.g. BitString)
				switch (tagNo)
				{
					case Asn1Tags.OctetString:
						return new BerOctetStringParser(sp);
					case Asn1Tags.Sequence:
						return new BerSequenceParser(sp);
					case Asn1Tags.Set:
						return new BerSetParser(sp);
					case Asn1Tags.External:
						return new DerExternalParser(sp);
					default:
						throw new IOException("unknown BER object encountered: 0x"
							+ tagNo.ToString("X"));
				}
			}
			else
			{
				DefiniteLengthInputStream defIn = new DefiniteLengthInputStream(_in, length);

				if ((tag & Asn1Tags.Application) != 0)
				{
					return new DerApplicationSpecific(isConstructed, tagNo, defIn.ToArray());
				}

				if ((tag & Asn1Tags.Tagged) != 0)
				{
					return new BerTaggedObjectParser(tag, tagNo, defIn);
				}

				if (isConstructed)
				{
					// TODO There are other tags that may be constructed (e.g. BitString)
					switch (tagNo)
					{
						case Asn1Tags.OctetString:
							//
							// yes, people actually do this...
							//
							return new BerOctetStringParser(new Asn1StreamParser(defIn));
						case Asn1Tags.Sequence:
							return new DerSequenceParser(new Asn1StreamParser(defIn));
						case Asn1Tags.Set:
							return new DerSetParser(new Asn1StreamParser(defIn));
						case Asn1Tags.External:
							return new DerExternalParser(new Asn1StreamParser(defIn));
						default:
							// TODO Add DerUnknownTagParser class?
							return new DerUnknownTag(true, tagNo, defIn.ToArray());
					}
				}

				// Some primitive encodings can be handled by parsers too...
				switch (tagNo)
				{
					case Asn1Tags.OctetString:
						return new DerOctetStringParser(defIn);
				}

				return Asn1InputStream.CreatePrimitiveDerObject(tagNo, defIn.ToArray());
			}
		}

		private void Set00Check(
			bool enabled)
		{
			if (_in is IndefiniteLengthInputStream)
			{
				((IndefiniteLengthInputStream) _in).SetEofOn00(enabled);
			}
		}

		internal Asn1EncodableVector ReadVector()
		{
			Asn1EncodableVector v = new Asn1EncodableVector();

			IAsn1Convertible obj;
			while ((obj = ReadObject()) != null)
			{
				v.Add(obj.ToAsn1Object());
			}

			return v;
		}
	}
}
