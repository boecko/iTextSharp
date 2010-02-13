using System;
using System.Collections;
using System.IO;
using System.Text;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Agreement.Srp;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Date;

namespace Org.BouncyCastle.Crypto.Tls
{
	/// <remarks>An implementation of all high level protocols in TLS 1.0.</remarks>
	public class TlsProtocolHandler
	{
		private const short RL_CHANGE_CIPHER_SPEC = 20;
		private const short RL_ALERT = 21;
		private const short RL_HANDSHAKE = 22;
		private const short RL_APPLICATION_DATA = 23;

		/*
		hello_request(0), client_hello(1), server_hello(2),
		certificate(11), server_key_exchange (12),
		certificate_request(13), server_hello_done(14),
		certificate_verify(15), client_key_exchange(16),
		finished(20), (255)
		*/

		private const short HP_HELLO_REQUEST = 0;
		private const short HP_CLIENT_HELLO = 1;
		private const short HP_SERVER_HELLO = 2;
		private const short HP_CERTIFICATE = 11;
		private const short HP_SERVER_KEY_EXCHANGE = 12;
		private const short HP_CERTIFICATE_REQUEST = 13;
		private const short HP_SERVER_HELLO_DONE = 14;
		private const short HP_CERTIFICATE_VERIFY = 15;
		private const short HP_CLIENT_KEY_EXCHANGE = 16;
		private const short HP_FINISHED = 20;

		/*
		* Our Connection states
		*/

		private const short CS_CLIENT_HELLO_SEND = 1;
		private const short CS_SERVER_HELLO_RECEIVED = 2;
		private const short CS_SERVER_CERTIFICATE_RECEIVED = 3;
		private const short CS_SERVER_KEY_EXCHANGE_RECEIVED = 4;
		private const short CS_CERTIFICATE_REQUEST_RECEIVED = 5;
		private const short CS_SERVER_HELLO_DONE_RECEIVED = 6;
		private const short CS_CLIENT_KEY_EXCHANGE_SEND = 7;
		private const short CS_CERTIFICATE_VERIFY_SEND = 8;
		private const short CS_CLIENT_CHANGE_CIPHER_SPEC_SEND = 9;
		private const short CS_CLIENT_FINISHED_SEND = 10;
		private const short CS_SERVER_CHANGE_CIPHER_SPEC_RECEIVED = 11;
		private const short CS_DONE = 12;

		internal const short AP_close_notify = 0;
		internal const short AP_unexpected_message = 10;
		internal const short AP_bad_record_mac = 20;
		internal const short AP_decryption_failed = 21;
		internal const short AP_record_overflow = 22;
		internal const short AP_decompression_failure = 30;
		internal const short AP_handshake_failure = 40;
		internal const short AP_bad_certificate = 42;
		internal const short AP_unsupported_certificate = 43;
		internal const short AP_certificate_revoked = 44;
		internal const short AP_certificate_expired = 45;
		internal const short AP_certificate_unknown = 46;
		internal const short AP_illegal_parameter = 47;
		internal const short AP_unknown_ca = 48;
		internal const short AP_access_denied = 49;
		internal const short AP_decode_error = 50;
		internal const short AP_decrypt_error = 51;
		internal const short AP_export_restriction = 60;
		internal const short AP_protocol_version = 70;
		internal const short AP_insufficient_security = 71;
		internal const short AP_internal_error = 80;
		internal const short AP_user_canceled = 90;
		internal const short AP_no_renegotiation = 100;

		internal const short AL_warning = 1;
		internal const short AL_fatal = 2;

		private static readonly byte[] emptybuf = new byte[0];

		private static readonly string TLS_ERROR_MESSAGE = "Internal TLS error, this could be an attack";

		/*
		* Queues for data from some protocols.
		*/

		private ByteQueue applicationDataQueue = new ByteQueue();
		private ByteQueue changeCipherSpecQueue = new ByteQueue();
		private ByteQueue alertQueue = new ByteQueue();
		private ByteQueue handshakeQueue = new ByteQueue();

		/*
		* The Record Stream we use
		*/
		private RecordStream rs;

		private SecureRandom random;

		/*
		 * The public key of the server.
		 */
		private AsymmetricKeyParameter serverPublicKey = null;

		/*
		 * The private key of the client (if provided)
		 */
		private AsymmetricKeyParameter clientPrivateKey = null;

		private TlsInputStream tlsInputStream = null;
		private TlsOuputStream tlsOutputStream = null;

		private bool closed = false;
		private bool failedWithError = false;
		private bool appDataReady = false;
		private bool extendedClientHello;

		private byte[] clientRandom;
		private byte[] serverRandom;
		private byte[] ms;

		private TlsCipherSuite chosenCipherSuite = null;

		private BigInteger SRP_A;
		private byte[] SRP_identity = null, SRP_password = null;
		private BigInteger Yc;
		private byte[] pms;

		private ICertificateVerifyer verifyer = null;
		private Certificate clientCert = null;
		private TlsSigner clientSigner = null;

		/*
		* Both streams can be the same object
		*/
		public TlsProtocolHandler(
			Stream	inStr,
			Stream	outStr)
		{
			/*
			 * We use a threaded seed generator to generate a good random
			 * seed. If the user has a better random seed, he should use
			 * the constructor with a SecureRandom.
			 * 
			 * Hopefully, 20 bytes in fast mode are good enough.
			 */
			byte[] seed = new ThreadedSeedGenerator().GenerateSeed(20, true);

			this.random = new SecureRandom(seed);
			this.rs = new RecordStream(this, inStr, outStr);
		}

		public TlsProtocolHandler(
			Stream			inStr,
			Stream			outStr,
			SecureRandom	sr)
		{
			this.random = sr;
			this.rs = new RecordStream(this, inStr, outStr);
		}

		internal SecureRandom Random
		{
			get { return random; }
		}

		private short connection_state;

		internal void ProcessData(
			short	protocol,
			byte[]	buf,
			int		offset,
			int		len)
		{
			/*
			* Have a look at the protocol type, and add it to the correct queue.
			*/
			switch (protocol)
			{
				case RL_CHANGE_CIPHER_SPEC:
					changeCipherSpecQueue.AddData(buf, offset, len);
					processChangeCipherSpec();
					break;
				case RL_ALERT:
					alertQueue.AddData(buf, offset, len);
					processAlert();
					break;
				case RL_HANDSHAKE:
					handshakeQueue.AddData(buf, offset, len);
					processHandshake();
					break;
				case RL_APPLICATION_DATA:
					if (!appDataReady)
					{
						this.FailWithError(AL_fatal, AP_unexpected_message);
					}
					applicationDataQueue.AddData(buf, offset, len);
					processApplicationData();
					break;
				default:
					/*
					* Uh, we don't know this protocol.
					*
					* RFC2246 defines on page 13, that we should ignore this.
					*/
					break;
			}
		}

		private void processHandshake()
		{
			bool read;
			do
			{
				read = false;

				/*
				* We need the first 4 bytes, they contain type and length of
				* the message.
				*/
				if (handshakeQueue.Available >= 4)
				{
					byte[] beginning = new byte[4];
					handshakeQueue.Read(beginning, 0, 4, 0);
					MemoryStream bis = new MemoryStream(beginning, false);
					short type = TlsUtilities.ReadUint8(bis);
					int len = TlsUtilities.ReadUint24(bis);

					/*
					* Check if we have enough bytes in the buffer to read
					* the full message.
					*/
					if (handshakeQueue.Available >= (len + 4))
					{
						/*
						* Read the message.
						*/
						byte[] buf = new byte[len];
						handshakeQueue.Read(buf, 0, len, 4);
						handshakeQueue.RemoveData(len + 4);

						/*
						* If it is not a finished message, update our hashes
						* we prepare for the finish message.
						*/
						if (type != HP_FINISHED)
						{
							rs.UpdateHandshakeData(beginning, 0, 4);
							rs.UpdateHandshakeData(buf, 0, len);
						}

						/*
						* Now, parse the message.
						*/
						MemoryStream inStr = new MemoryStream(buf, false);

						/*
						* Check the type.
						*/
						switch (type)
						{
							case HP_CERTIFICATE:
							{
								switch (connection_state)
								{
									case CS_SERVER_HELLO_RECEIVED:
									{
										/*
										* Parse the certificates.
										*/
										Certificate cert = Certificate.Parse(inStr);
										AssertEmpty(inStr);

										X509CertificateStructure x509Cert = cert.certs[0];
										SubjectPublicKeyInfo keyInfo = x509Cert.SubjectPublicKeyInfo;

										try
										{
											this.serverPublicKey = PublicKeyFactory.CreateKey(keyInfo);
										}
										catch (Exception)
										{
											this.FailWithError(AL_fatal, AP_unsupported_certificate);
										}

										// Sanity check the PublicKeyFactory
										if (this.serverPublicKey.IsPrivate)
										{
											this.FailWithError(AL_fatal, AP_internal_error);
										}

										/*
										* Perform various checks per RFC2246 7.4.2
										* TODO "Unless otherwise specified, the signing algorithm for the certificate
										* must be the same as the algorithm for the certificate key."
										*/
										switch (this.chosenCipherSuite.KeyExchangeAlgorithm)
										{
											case TlsCipherSuite.KE_RSA:
												if (!(this.serverPublicKey is RsaKeyParameters))
												{
													this.FailWithError(AL_fatal, AP_certificate_unknown);
												}
												validateKeyUsage(x509Cert, KeyUsage.KeyEncipherment);
												break;
											case TlsCipherSuite.KE_DHE_RSA:
											case TlsCipherSuite.KE_SRP_RSA:
												if (!(this.serverPublicKey is RsaKeyParameters))
												{
													this.FailWithError(AL_fatal, AP_certificate_unknown);
												}
												validateKeyUsage(x509Cert, KeyUsage.DigitalSignature);
												break;
											case TlsCipherSuite.KE_DHE_DSS:
											case TlsCipherSuite.KE_SRP_DSS:
												if (!(this.serverPublicKey is DsaPublicKeyParameters))
												{
													this.FailWithError(AL_fatal, AP_certificate_unknown);
												}
												break;
											default:
												this.FailWithError(AL_fatal, AP_unsupported_certificate);
												break;
										}

										/*
										* Verify them.
										*/
										if (!this.verifyer.IsValid(cert.GetCerts()))
										{
											this.FailWithError(AL_fatal, AP_user_canceled);
										}

										break;
									}
									default:
										this.FailWithError(AL_fatal, AP_unexpected_message);
										break;
								}

								connection_state = CS_SERVER_CERTIFICATE_RECEIVED;
								read = true;
								break;
							}
							case HP_FINISHED:
								switch (connection_state)
								{
									case CS_SERVER_CHANGE_CIPHER_SPEC_RECEIVED:
										/*
										* Read the checksum from the finished message,
										* it has always 12 bytes.
										*/
										byte[] receivedChecksum = new byte[12];
										TlsUtilities.ReadFully(receivedChecksum, inStr);
										AssertEmpty(inStr);

										/*
										* Calculate our own checksum.
										*/
										byte[] checksum = new byte[12];
										byte[] md5andsha1 = new byte[16 + 20];
										rs.hash2.DoFinal(md5andsha1, 0);
										TlsUtilities.PRF(this.ms, "server finished", md5andsha1, checksum);

										/*
										* Compare both checksums.
										*/
										for (int i = 0; i < receivedChecksum.Length; i++)
										{
											if (receivedChecksum[i] != checksum[i])
											{
												/*
												* Wrong checksum in the finished message.
												*/
												this.FailWithError(AL_fatal, AP_handshake_failure);
											}
										}

										connection_state = CS_DONE;

										/*
										* We are now ready to receive application data.
										*/
										this.appDataReady = true;
										read = true;
										break;
									default:
										this.FailWithError(AL_fatal, AP_unexpected_message);
										break;
								}
								break;
							case HP_SERVER_HELLO:
								switch (connection_state)
								{
									case CS_CLIENT_HELLO_SEND:
										/*
										* Read the server hello message
										*/
										TlsUtilities.CheckVersion(inStr, this);

										/*
										* Read the server random
										*/
										this.serverRandom = new byte[32];
										TlsUtilities.ReadFully(this.serverRandom, inStr);

										/*
										* Currently, we don't support session ids
										*/
										byte[] sessionId = TlsUtilities.ReadOpaque8(inStr);

										/*
										* Find out which ciphersuite the server has
										* chosen. If we don't support this ciphersuite,
										* the TlsCipherSuiteManager will throw an
										* exception.
										*/
										this.chosenCipherSuite = TlsCipherSuiteManager.GetCipherSuite(
											TlsUtilities.ReadUint16(inStr), this);

										/*
										* We support only the null compression which
										* means no compression.
										*/
										short compressionMethod = TlsUtilities.ReadUint8(inStr);
										if (compressionMethod != 0)
										{
											this.FailWithError(TlsProtocolHandler.AL_fatal, TlsProtocolHandler.AP_illegal_parameter);
										}

	                                    /*
	                                     * RFC4366 2.2
	                                     * The extended server hello message format MAY be sent
	                                     * in place of the server hello message when the client
	                                     * has requested extended functionality via the extended
	                                     * client hello message specified in Section 2.1.
	                                     */
	                                    if (extendedClientHello && inStr.Position < inStr.Length)
	                                    {
	                                        // Process extensions from extended server hello
	                                        byte[] extBytes = TlsUtilities.ReadOpaque16(inStr);
	
	                                        // Int32 -> byte[]
	                                        Hashtable serverExtensions = new Hashtable();

	                                        MemoryStream ext = new MemoryStream(extBytes, false);
	                                        while (ext.Position < ext.Length)
	                                        {
	                                            int extType = TlsUtilities.ReadUint16(ext);
	                                            byte[] extValue = TlsUtilities.ReadOpaque16(ext);

	                                            serverExtensions[extType] = extValue;
	                                        }

	                                        // TODO Validate/process serverExtensions (via client?)
	                                        // TODO[SRP]
	                                    }

										/*
										* Process any extensions
										*/
										// TODO[SRP]
//										if (inStr.Position < inStr.Length)
//										{
//											int extensionsLength = TlsUtilities.ReadUint16(inStr);
//											byte[] extensions = new byte[extensionsLength];
//											TlsUtilities.ReadFully(extensions, inStr);
//
//											// TODO Validate/process
//										}

										AssertEmpty(inStr);

										connection_state = CS_SERVER_HELLO_RECEIVED;
										read = true;
										break;
									default:
										this.FailWithError(AL_fatal, AP_unexpected_message);
										break;
								}
								break;
							case HP_SERVER_HELLO_DONE:
								switch (connection_state)
								{
									case CS_SERVER_CERTIFICATE_RECEIVED:
									case CS_SERVER_KEY_EXCHANGE_RECEIVED:
									case CS_CERTIFICATE_REQUEST_RECEIVED:

										// NB: Original code used case label fall-through
										if (connection_state == CS_SERVER_CERTIFICATE_RECEIVED)
										{
											/*
											* There was no server key exchange message, check
											* that we are doing RSA key exchange.
											*/
											if (this.chosenCipherSuite.KeyExchangeAlgorithm != TlsCipherSuite.KE_RSA)
											{
												this.FailWithError(AL_fatal, AP_unexpected_message);
											}
										}

										AssertEmpty(inStr);
										bool isCertReq = (connection_state == CS_CERTIFICATE_REQUEST_RECEIVED);
										connection_state = CS_SERVER_HELLO_DONE_RECEIVED;

										if (isCertReq)
										{
											sendClientCertificate();
										}

										/*
										* Send the client key exchange message, depending
										* on the key exchange we are using in our
										* ciphersuite.
										*/
										switch (this.chosenCipherSuite.KeyExchangeAlgorithm)
										{
											case TlsCipherSuite.KE_RSA:
											{
												/*
												* We are doing RSA key exchange. We will
												* choose a pre master secret and send it
												* rsa encrypted to the server.
												*
												* Prepare pre master secret.
												*/
												pms = new byte[48];
												pms[0] = 3;
												pms[1] = 1;
												random.NextBytes(pms, 2, 46);

												/*
												* Encode the pms and send it to the server.
												*
												* Prepare an Pkcs1Encoding with good random
												* padding.
												*/
												RsaBlindedEngine rsa = new RsaBlindedEngine();
												Pkcs1Encoding encoding = new Pkcs1Encoding(rsa);
												encoding.Init(true, new ParametersWithRandom(this.serverPublicKey, this.random));
												byte[] encrypted = null;
												try
												{
													encrypted = encoding.ProcessBlock(pms, 0, pms.Length);
												}
												catch (InvalidCipherTextException)
												{
													/*
													* This should never happen, only during decryption.
													*/
													this.FailWithError(AL_fatal, AP_internal_error);
												}

												/*
												* Send the encrypted pms.
												*/
												sendClientKeyExchange(encrypted);
												break;
											}
											case TlsCipherSuite.KE_DHE_DSS:
											case TlsCipherSuite.KE_DHE_RSA:
											{
												/*
												* Send the Client Key Exchange message for
												* DHE key exchange.
												*/
												byte[] YcByte = BigIntegers.AsUnsignedByteArray(this.Yc);

												sendClientKeyExchange(YcByte);

												break;
											}
											case TlsCipherSuite.KE_SRP:
											case TlsCipherSuite.KE_SRP_RSA:
											case TlsCipherSuite.KE_SRP_DSS:
											{
												/*
												* Send the Client Key Exchange message for
												* SRP key exchange.
												*/
												byte[] bytes = BigIntegers.AsUnsignedByteArray(this.SRP_A);

												sendClientKeyExchange(bytes);

												break;
											}
											default:
												/*
												* Problem during handshake, we don't know
												* how to handle this key exchange method.
												*/
												this.FailWithError(AL_fatal, AP_unexpected_message);
												break;

										}

										connection_state = CS_CLIENT_KEY_EXCHANGE_SEND;

										if (isCertReq && this.clientPrivateKey != null)
										{
										    sendCertificateVerify();

										    connection_state = CS_CERTIFICATE_VERIFY_SEND;
										}

										/*
										* Now, we send change cipher state
										*/
										byte[] cmessage = new byte[1];
										cmessage[0] = 1;
										rs.WriteMessage(RL_CHANGE_CIPHER_SPEC, cmessage, 0, cmessage.Length);

										connection_state = CS_CLIENT_CHANGE_CIPHER_SPEC_SEND;

										/*
										* Calculate the ms
										*/
										this.ms = new byte[48];
										byte[] randBytes = new byte[clientRandom.Length + serverRandom.Length];
										Array.Copy(clientRandom, 0, randBytes, 0, clientRandom.Length);
										Array.Copy(serverRandom, 0, randBytes, clientRandom.Length, serverRandom.Length);
										TlsUtilities.PRF(pms, "master secret", randBytes, this.ms);

										/*
										* Initialize our cipher suite
										*/
										rs.writeSuite = this.chosenCipherSuite;
										rs.writeSuite.Init(this, this.ms, clientRandom, serverRandom);

										/*
										* Send our finished message.
										*/
										byte[] checksum = new byte[12];
										byte[] md5andsha1 = new byte[16 + 20];
										rs.hash1.DoFinal(md5andsha1, 0);
										TlsUtilities.PRF(this.ms, "client finished", md5andsha1, checksum);

										MemoryStream bos2 = new MemoryStream();
										TlsUtilities.WriteUint8(HP_FINISHED, bos2);
										TlsUtilities.WriteUint24(12, bos2);
										bos2.Write(checksum, 0, checksum.Length);
										byte[] message2 = bos2.ToArray();

										rs.WriteMessage(RL_HANDSHAKE, message2, 0, message2.Length);

										this.connection_state = CS_CLIENT_FINISHED_SEND;
										read = true;
										break;
									default:
										this.FailWithError(AL_fatal, AP_handshake_failure);
										break;
								}
								break;
							case HP_SERVER_KEY_EXCHANGE:
							{
								switch (connection_state)
								{
									case CS_SERVER_HELLO_RECEIVED:
									case CS_SERVER_CERTIFICATE_RECEIVED:
									{
										// NB: Original code used case label fall-through
										if (connection_state == CS_SERVER_HELLO_RECEIVED)
										{
											/*
											* There was no server certificate message, check
											* that we are doing SRP key exchange.
											*/
											if (this.chosenCipherSuite.KeyExchangeAlgorithm != TlsCipherSuite.KE_SRP)
											{
												this.FailWithError(AL_fatal, AP_unexpected_message);
											}
										}

										/*
										* Check that we are doing DHE key exchange
										*/
										switch (this.chosenCipherSuite.KeyExchangeAlgorithm)
										{
											case TlsCipherSuite.KE_DHE_RSA:
											{
												processDHEKeyExchange(inStr, new TlsRsaSigner());
												break;
											}
											case TlsCipherSuite.KE_DHE_DSS:
											{
												processDHEKeyExchange(inStr, new TlsDssSigner());
												break;
											}
											case TlsCipherSuite.KE_SRP:
											{
												processSRPKeyExchange(inStr, null);
												break;
											}
											case TlsCipherSuite.KE_SRP_RSA:
											{
												processSRPKeyExchange(inStr, new TlsRsaSigner());
												break;
											}
											case TlsCipherSuite.KE_SRP_DSS:
											{
												processSRPKeyExchange(inStr, new TlsDssSigner());
												break;
											}
											default:
												this.FailWithError(AL_fatal, AP_unexpected_message);
												break;
										}
										break;
									}
									default:
										this.FailWithError(AL_fatal, AP_unexpected_message);
										break;
								}

								this.connection_state = CS_SERVER_KEY_EXCHANGE_RECEIVED;
								read = true;
								break;
							}
							case HP_CERTIFICATE_REQUEST:
								switch (connection_state)
								{
									case CS_SERVER_CERTIFICATE_RECEIVED:
									case CS_SERVER_KEY_EXCHANGE_RECEIVED:
									{
										// NB: Original code used case label fall-through
										if (connection_state == CS_SERVER_CERTIFICATE_RECEIVED)
										{
											/*
											* There was no server key exchange message, check
											* that we are doing RSA key exchange.
											*/
											if (this.chosenCipherSuite.KeyExchangeAlgorithm != TlsCipherSuite.KE_RSA)
											{
												this.FailWithError(AL_fatal, AP_unexpected_message);
											}
										}

										byte[] types = TlsUtilities.ReadOpaque8(inStr);
										byte[] auths = TlsUtilities.ReadOpaque16(inStr);

										// TODO Validate/process

										AssertEmpty(inStr);
										break;
									}
									default:
										this.FailWithError(AL_fatal, AP_unexpected_message);
										break;
								}

								this.connection_state = CS_CERTIFICATE_REQUEST_RECEIVED;
								read = true;
								break;
							case HP_HELLO_REQUEST:
							case HP_CLIENT_KEY_EXCHANGE:
							case HP_CERTIFICATE_VERIFY:
							case HP_CLIENT_HELLO:
							default:
								// We do not support this!
								this.FailWithError(AL_fatal, AP_unexpected_message);
								break;
						}
					}
				}
			}
			while (read);
		}

		private void processApplicationData()
		{
			/*
			* There is nothing we need to do here.
			* 
			* This function could be used for callbacks when application
			* data arrives in the future.
			*/
		}

		private void processAlert()
		{
			while (alertQueue.Available >= 2)
			{
				/*
				* An alert is always 2 bytes. Read the alert.
				*/
				byte[] tmp = new byte[2];
				alertQueue.Read(tmp, 0, 2, 0);
				alertQueue.RemoveData(2);
				short level = tmp[0];
				short description = tmp[1];
				if (level == AL_fatal)
				{
					/*
					* This is a fatal error.
					*/
					this.failedWithError = true;
					this.closed = true;
					/*
					* Now try to Close the stream, ignore errors.
					*/
					try
					{
						rs.Close();
					}
					catch (Exception)
					{
					}
					throw new IOException(TLS_ERROR_MESSAGE);
				}
				else
				{
					/*
					* This is just a warning.
					*/
					if (description == AP_close_notify)
					{
						/*
						* Close notify
						*/
						this.FailWithError(AL_warning, AP_close_notify);
					}
					/*
					* If it is just a warning, we continue.
					*/
				}
			}
		}

		/**
		* This method is called, when a change cipher spec message is received.
		*
		* @throws IOException If the message has an invalid content or the
		*                     handshake is not in the correct state.
		*/
		private void processChangeCipherSpec()
		{
			while (changeCipherSpecQueue.Available > 0)
			{
				/*
				* A change cipher spec message is only one byte with the value 1.
				*/
				byte[] b = new byte[1];
				changeCipherSpecQueue.Read(b, 0, 1, 0);
				changeCipherSpecQueue.RemoveData(1);
				if (b[0] != 1)
				{
					/*
					* This should never happen.
					*/
					this.FailWithError(AL_fatal, AP_unexpected_message);

				}
				else
				{
					/*
					* Check if we are in the correct connection state.
					*/
					if (this.connection_state == CS_CLIENT_FINISHED_SEND)
					{
						rs.readSuite = rs.writeSuite;
						this.connection_state = CS_SERVER_CHANGE_CIPHER_SPEC_RECEIVED;
					}
					else
					{
						/*
						* We are not in the correct connection state.
						*/
						this.FailWithError(AL_fatal, AP_handshake_failure);
					}

				}
			}
		}

		private void processDHEKeyExchange(
			MemoryStream	inStr,
			TlsSigner		tlsSigner)
		{
			Stream sigIn = inStr;
			ISigner signer = null;
			if (tlsSigner != null)
			{
				signer = tlsSigner.CreateSigner();
				signer.Init(false, this.serverPublicKey);
				signer.BlockUpdate(this.clientRandom, 0, this.clientRandom.Length);
				signer.BlockUpdate(this.serverRandom, 0, this.serverRandom.Length);

				sigIn = new SignerStream(inStr, signer, null);
			}

			/*
			* Parse the Structure
			*/
			byte[] pByte = TlsUtilities.ReadOpaque16(sigIn);
			byte[] gByte = TlsUtilities.ReadOpaque16(sigIn);
			byte[] YsByte = TlsUtilities.ReadOpaque16(sigIn);

			if (signer != null)
			{
				byte[] sigByte = TlsUtilities.ReadOpaque16(inStr);

				/*
				* Verify the Signature.
				*/
				if (!signer.VerifySignature(sigByte))
				{
					this.FailWithError(AL_fatal, AP_bad_certificate);
				}
			}

			this.AssertEmpty(inStr);

			/*
			* Do the DH calculation.
			*/
			BigInteger p = new BigInteger(1, pByte);
			BigInteger g = new BigInteger(1, gByte);
			BigInteger Ys = new BigInteger(1, YsByte);

			/*
			* Check the DH parameter values
			*/
			if (!p.IsProbablePrime(10))
			{
				this.FailWithError(AL_fatal, AP_illegal_parameter);
			}
			if (g.CompareTo(BigInteger.Two) < 0 || g.CompareTo(p.Subtract(BigInteger.Two)) > 0)
			{
				this.FailWithError(AL_fatal, AP_illegal_parameter);
			}
			// TODO For static DH public values, see additional checks in RFC 2631 2.1.5 
			if (Ys.CompareTo(BigInteger.Two) < 0 || Ys.CompareTo(p.Subtract(BigInteger.One)) > 0)
			{
				this.FailWithError(AL_fatal, AP_illegal_parameter);
			}

			/*
			* Diffie-Hellman basic key agreement
			*/
			DHParameters dhParams = new DHParameters(p, g);

			// Generate a keypair
			DHBasicKeyPairGenerator dhGen = new DHBasicKeyPairGenerator();
			dhGen.Init(new DHKeyGenerationParameters(random, dhParams));

			AsymmetricCipherKeyPair dhPair = dhGen.GenerateKeyPair();

			// Store the public value to send to server
			this.Yc = ((DHPublicKeyParameters)dhPair.Public).Y;

			// Calculate the shared secret
			DHBasicAgreement dhAgree = new DHBasicAgreement();
			dhAgree.Init(dhPair.Private);

			BigInteger agreement = dhAgree.CalculateAgreement(new DHPublicKeyParameters(Ys, dhParams));

			this.pms = BigIntegers.AsUnsignedByteArray(agreement);
		}

		private void processSRPKeyExchange(
			MemoryStream	inStr,
			TlsSigner		tlsSigner)
		{
			Stream sigIn = inStr;
			ISigner signer = null;
			if (tlsSigner != null)
			{
				signer = tlsSigner.CreateSigner();
				signer.Init(false, this.serverPublicKey);
				signer.BlockUpdate(this.clientRandom, 0, this.clientRandom.Length);
				signer.BlockUpdate(this.serverRandom, 0, this.serverRandom.Length);

				sigIn = new SignerStream(inStr, signer, null);
			}

			/*
			* Parse the Structure
			*/
			byte[] NByte = TlsUtilities.ReadOpaque16(sigIn);
			byte[] gByte = TlsUtilities.ReadOpaque16(sigIn);
			byte[] sByte = TlsUtilities.ReadOpaque8(sigIn);
			byte[] BByte = TlsUtilities.ReadOpaque16(sigIn);

			if (signer != null)
			{
				byte[] sigByte = TlsUtilities.ReadOpaque16(inStr);

				/*
				* Verify the Signature.
				*/
				if (!signer.VerifySignature(sigByte))
				{
					this.FailWithError(AL_fatal, AP_bad_certificate);
				}
			}

			this.AssertEmpty(inStr);

			BigInteger N = new BigInteger(1, NByte);
			BigInteger g = new BigInteger(1, gByte);
			byte[] s = sByte;
			BigInteger B = new BigInteger(1, BByte);

			Srp6Client srpClient = new Srp6Client();
			srpClient.Init(N, g, new Sha1Digest(), random);

			this.SRP_A = srpClient.GenerateClientCredentials(s, this.SRP_identity,
				this.SRP_password);

			try
			{
				BigInteger S = srpClient.CalculateSecret(B);

				// TODO Check if this needs to be a fixed size
				this.pms = BigIntegers.AsUnsignedByteArray(S);
			}
			catch (CryptoException)
			{
				this.FailWithError(AL_fatal, AP_illegal_parameter);
			}
		}

		private void validateKeyUsage(
			X509CertificateStructure	c,
			int							keyUsageBits)
		{
			X509Extensions exts = c.TbsCertificate.Extensions;
			if (exts != null)
			{
				X509Extension ext = exts.GetExtension(X509Extensions.KeyUsage);
				if (ext != null)
				{
					DerBitString ku = KeyUsage.GetInstance(ext);
					int bits = ku.GetBytes()[0];
					if ((bits & keyUsageBits) != keyUsageBits)
					{
						this.FailWithError(AL_fatal, AP_certificate_unknown);
					}
				}
			}
		}

		private void sendClientCertificate()
		{
			MemoryStream bos = new MemoryStream();
			TlsUtilities.WriteUint8(HP_CERTIFICATE, bos);
			clientCert.Encode(bos);
			byte[] message = bos.ToArray();

			rs.WriteMessage(RL_HANDSHAKE, message, 0, message.Length);
		}

		private void sendClientKeyExchange(
			byte[] keData)
		{
			MemoryStream bos = new MemoryStream();
			TlsUtilities.WriteUint8(HP_CLIENT_KEY_EXCHANGE, bos);
			TlsUtilities.WriteUint24(keData.Length + 2, bos);
			TlsUtilities.WriteOpaque16(keData, bos);
			byte[] message = bos.ToArray();

			rs.WriteMessage(RL_HANDSHAKE, message, 0, message.Length);
		}

		private void sendCertificateVerify()
		{
			/*
			 * Send signature of handshake messages so far to prove we are the owner of
			 * the cert See RFC 2246 sections 4.7, 7.4.3 and 7.4.8
			 */

			try
			{
				byte[] md5andsha1 = new byte[16 + 20];
				rs.hash3.DoFinal(md5andsha1, 0);
            	byte[] data = clientSigner.CalculateRawSignature(clientPrivateKey, md5andsha1);

				MemoryStream bos = new MemoryStream();
				TlsUtilities.WriteUint8(HP_CERTIFICATE_VERIFY, bos);
				TlsUtilities.WriteUint24(data.Length + 2, bos);
				TlsUtilities.WriteOpaque16(data, bos);
				byte[] message = bos.ToArray();

				rs.WriteMessage(RL_HANDSHAKE, message, 0, message.Length);
			}
			catch (CryptoException)
			{
				this.FailWithError(AL_fatal, AP_handshake_failure);
			}
		}
		
		/// <summary>Connects to the remote system.</summary>
		/// <param name="verifyer">Will be used when a certificate is received to verify
		/// that this certificate is accepted by the client.</param>
		/// <exception cref="IOException">If handshake was not successful</exception>
		public virtual void Connect(
			ICertificateVerifyer verifyer)
		{
	        this.Connect(verifyer, null, null);
	    }

		/// <summary>Connects to the remote system using client authentication</summary>
		/// <param name="verifyer">Will be used when a certificate is received to verify
		/// that this certificate is accepted by the client.</param>
		/// <param name="clientCertificate">The client's certificate to be provided to
		/// the remote system.</param>
		/// <param name="clientPrivateKey">The client's private key for the certificate
		/// to authenticate to the remote system (RSA or DSA).</param>
		// TODO Make public to enable client certificate support
		internal virtual void Connect(
			ICertificateVerifyer	verifyer,
			Certificate				clientCertificate,
			AsymmetricKeyParameter	clientPrivateKey)
		{
			if (clientCertificate == null)
			{
				clientCertificate = new Certificate(new X509CertificateStructure[0]);
			}

			if (clientPrivateKey == null)
			{
				if (clientCertificate.certs.Length != 0)
				{
					throw new ArgumentException("key not specified for certificate", "clientPrivateKey");
				}
			}
			else
			{
	            if (clientCertificate.certs.Length == 0)
	            {
					throw new ArgumentException("key specified without certificate", "clientPrivateKey");
	            }
				else if (!clientPrivateKey.IsPrivate)
				{
					throw new ArgumentException("must be private", "clientPrivateKey");
				}
				else if (clientPrivateKey is RsaKeyParameters)
				{
					clientSigner = new TlsRsaSigner();
				}
				else if (clientPrivateKey is DsaPrivateKeyParameters)
				{
					clientSigner = new TlsDssSigner();
				}
				else
				{
					throw new ArgumentException("type not supported", "clientPrivateKey");
				}
			}

			this.verifyer = verifyer;
			this.clientCert = clientCertificate;
			this.clientPrivateKey = clientPrivateKey;

			/*
			* Send Client hello
			*
			* First, generate some random data.
			*/
			this.clientRandom = new byte[32];

			/*
			* TLS 1.0 requires a unix-timestamp in the first 4 bytes
			*/
			int t = (int)(DateTimeUtilities.CurrentUnixMs() / 1000L);
			this.clientRandom[0] = (byte)(t >> 24);
			this.clientRandom[1] = (byte)(t >> 16);
			this.clientRandom[2] = (byte)(t >> 8);
			this.clientRandom[3] = (byte)t;

			random.NextBytes(this.clientRandom, 4, 28);


			MemoryStream outStr = new MemoryStream();
			TlsUtilities.WriteVersion(outStr);
			outStr.Write(this.clientRandom, 0, this.clientRandom.Length);

			/*
			* Length of Session id
			*/
			TlsUtilities.WriteUint8((short)0, outStr);

			/*
			* Cipher suites
			*/
			TlsCipherSuiteManager.WriteCipherSuites(outStr);

			/*
			* Compression methods, just the null method.
			*/
			byte[] compressionMethods = new byte[]{0x00};
			TlsUtilities.WriteOpaque8(compressionMethods, outStr);

			/*
			* Extensions
			*/
			// TODO Collect extensions from client
			// Int32 -> byte[]
			Hashtable clientExtensions = new Hashtable();

			// TODO[SRP]
//			{
//				MemoryStream srpData = new MemoryStream();
//				TlsUtilities.WriteOpaque8(SRP_identity, srpData);
//
//				// TODO[SRP] RFC5054 2.8.1: ExtensionType.srp = 12
//				clientExtensions[12] = srpData.ToArray();
//			}

			this.extendedClientHello = (clientExtensions.Count > 0);

			if (extendedClientHello)
			{
				MemoryStream ext = new MemoryStream();

				foreach (int extType in clientExtensions.Keys)
				{
					byte[] extValue = (byte[])clientExtensions[extType];

					TlsUtilities.WriteUint16(extType, ext);
					TlsUtilities.WriteOpaque16(extValue, ext);
				}

				TlsUtilities.WriteOpaque16(ext.ToArray(), outStr);
			}

			MemoryStream bos = new MemoryStream();
			TlsUtilities.WriteUint8(HP_CLIENT_HELLO, bos);
			TlsUtilities.WriteUint24((int) outStr.Length, bos);
			byte[] outBytes = outStr.ToArray();
			bos.Write(outBytes, 0, outBytes.Length);
			byte[] message = bos.ToArray();
			rs.WriteMessage(RL_HANDSHAKE, message, 0, message.Length);
			connection_state = CS_CLIENT_HELLO_SEND;

			/*
			* We will now read data, until we have completed the handshake.
			*/
			while (connection_state != CS_DONE)
			{
				rs.ReadData();
			}

			this.tlsInputStream = new TlsInputStream(this);
			this.tlsOutputStream = new TlsOuputStream(this);
		}

		/**
		* Read data from the network. The method will return immed, if there is
		* still some data left in the buffer, or block untill some application
		* data has been read from the network.
		*
		* @param buf    The buffer where the data will be copied to.
		* @param offset The position where the data will be placed in the buffer.
		* @param len    The maximum number of bytes to read.
		* @return The number of bytes read.
		* @throws IOException If something goes wrong during reading data.
		*/
		internal int ReadApplicationData(byte[] buf, int offset, int len)
		{
			while (applicationDataQueue.Available == 0)
			{
				if (this.closed)
				{
					/*
					* We need to read some data.
					*/
					if (this.failedWithError)
					{
						/*
						* Something went terribly wrong, we should throw an IOException
						*/
						throw new IOException(TLS_ERROR_MESSAGE);
					}

					/*
					* Connection has been closed, there is no more data to read.
					*/
					return 0;
				}

				try
				{
					rs.ReadData();
				}
				catch (IOException e)
				{
					if (!this.closed)
					{
						this.FailWithError(AL_fatal, AP_internal_error);
					}
					throw e;
				}
				catch (Exception e)
				{
					if (!this.closed)
					{
						this.FailWithError(AL_fatal, AP_internal_error);
					}
					throw e;
				}
			}
			len = System.Math.Min(len, applicationDataQueue.Available);
			applicationDataQueue.Read(buf, offset, len, 0);
			applicationDataQueue.RemoveData(len);
			return len;
		}

		/**
		* Send some application data to the remote system.
		* <p/>
		* The method will handle fragmentation internally.
		*
		* @param buf    The buffer with the data.
		* @param offset The position in the buffer where the data is placed.
		* @param len    The length of the data.
		* @throws IOException If something goes wrong during sending.
		*/
		internal void WriteData(byte[] buf, int offset, int len)
		{
			if (this.closed)
			{
				if (this.failedWithError)
					throw new IOException(TLS_ERROR_MESSAGE);

				throw new IOException("Sorry, connection has been closed, you cannot write more data");
			}

			/*
			* Protect against known IV attack!
			*
			* DO NOT REMOVE THIS LINE, EXCEPT YOU KNOW EXACTLY WHAT
			* YOU ARE DOING HERE.
			*/
			rs.WriteMessage(RL_APPLICATION_DATA, emptybuf, 0, 0);

			do
			{
				/*
				* We are only allowed to write fragments up to 2^14 bytes.
				*/
				int toWrite = System.Math.Min(len, 1 << 14);

				try
				{
					rs.WriteMessage(RL_APPLICATION_DATA, buf, offset, toWrite);
				}
				catch (IOException e)
				{
					if (!closed)
					{
						this.FailWithError(AL_fatal, AP_internal_error);
					}
					throw e;
				}
				catch (Exception e)
				{
					if (!closed)
					{
						this.FailWithError(AL_fatal, AP_internal_error);
					}
					throw e;
				}

				offset += toWrite;
				len -= toWrite;
			}
			while (len > 0);
		}

		[Obsolete("Use 'OutputStream' property instead")]
		public TlsOuputStream TlsOuputStream
		{
			get { return this.tlsOutputStream; }
		}

		/// <summary>A Stream which can be used to send data.</summary>
		public virtual Stream OutputStream
		{
			get { return this.tlsOutputStream; }
		}

		[Obsolete("Use 'InputStream' property instead")]
		public TlsInputStream TlsInputStream
		{
			get { return this.tlsInputStream; }
		}

		/// <summary>A Stream which can be used to read data.</summary>
		public virtual Stream InputStream
		{
			get { return this.tlsInputStream; }
		}

		/**
		* Terminate this connection with an alert.
		* <p/>
		* Can be used for normal closure too.
		*
		* @param alertLevel       The level of the alert, an be AL_fatal or AL_warning.
		* @param alertDescription The exact alert message.
		* @throws IOException If alert was fatal.
		*/
		internal void FailWithError(
			short	alertLevel,
			short	alertDescription)
		{
			/*
			* Check if the connection is still open.
			*/
			if (!closed)
			{
				/*
				* Prepare the message
				*/
				byte[] error = new byte[2];
				error[0] = (byte)alertLevel;
				error[1] = (byte)alertDescription;
				this.closed = true;

				if (alertLevel == AL_fatal)
				{
					/*
					* This is a fatal message.
					*/
					this.failedWithError = true;
				}
				rs.WriteMessage(RL_ALERT, error, 0, 2);
				rs.Close();
				if (alertLevel == AL_fatal)
				{
					throw new IOException(TLS_ERROR_MESSAGE);
				}
			}
			else
			{
				throw new IOException(TLS_ERROR_MESSAGE);
			}
		}

		/// <summary>Closes this connection</summary>
		/// <exception cref="IOException">If something goes wrong during closing.</exception>
		public virtual void Close()
		{
			if (!closed)
			{
				this.FailWithError((short)1, (short)0);
			}
		}

		/**
		* Make sure the Stream is now empty. Fail otherwise.
		*
		* @param is The Stream to check.
		* @throws IOException If is is not empty.
		*/
		internal void AssertEmpty(
			MemoryStream inStr)
		{
			if (inStr.Position < inStr.Length)
			{
				this.FailWithError(AL_fatal, AP_decode_error);
			}
		}

		internal void Flush()
		{
			rs.Flush();
		}
	}
}
