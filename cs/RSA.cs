﻿using System;

/*
Licensed to the Apache Software Foundation (ASF) under one
or more contributor license agreements.  See the NOTICE file
distributed with this work for additional information
regarding copyright ownership.  The ASF licenses this file
to you under the Apache License, Version 2.0 (the
"License"); you may not use this file except in compliance
with the License.  You may obtain a copy of the License at

  http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing,
software distributed under the License is distributed on an
"AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, either express or implied.  See the License for the
specific language governing permissions and limitations
under the License.
*/

/* RSA API high-level functions  */

public sealed class rsa_private_key
{
	public FF p, q, dp, dq, c;

	public rsa_private_key(int n)
	{
		p = new FF(n);
		q = new FF(n);
		dp = new FF(n);
		dq = new FF(n);
		c = new FF(n);
	}
}

public sealed class rsa_public_key
{
	public int e;
	public FF n;

	public rsa_public_key(int m)
	{
		e = 0;
		n = new FF(m);
	}
}

public sealed class RSA
{

	public static readonly int RFS = ROM.MODBYTES * ROM.FFLEN;

/* generate an RSA key pair */

	public static void KEY_PAIR(RAND rng, int e, rsa_private_key PRIV, rsa_public_key PUB)
	{ // IEEE1363 A16.11/A16.12 more or less
		int n = PUB.n.getlen() / 2;
		FF t = new FF(n);
		FF p1 = new FF(n);
		FF q1 = new FF(n);

		for (;;)
		{

			PRIV.p.random(rng);
			while (PRIV.p.lastbits(2) != 3)
			{
				PRIV.p.inc(1);
			}
			while (!FF.prime(PRIV.p,rng))
			{
				PRIV.p.inc(4);
			}

			p1.copy(PRIV.p);
			p1.dec(1);

			if (p1.cfactor(e))
			{
				continue;
			}
			break;
		}

		for (;;)
		{
			PRIV.q.random(rng);
			while (PRIV.q.lastbits(2) != 3)
			{
				PRIV.q.inc(1);
			}
			while (!FF.prime(PRIV.q,rng))
			{
				PRIV.q.inc(4);
			}

			q1.copy(PRIV.q);
			q1.dec(1);

			if (q1.cfactor(e))
			{
				continue;
			}

			break;
		}

		PUB.n = FF.mul(PRIV.p,PRIV.q);
		PUB.e = e;

		t.copy(p1);
		t.shr();
		PRIV.dp.set(e);
		PRIV.dp.invmodp(t);
		if (PRIV.dp.parity() == 0)
		{
			PRIV.dp.add(t);
		}
		PRIV.dp.norm();

		t.copy(q1);
		t.shr();
		PRIV.dq.set(e);
		PRIV.dq.invmodp(t);
		if (PRIV.dq.parity() == 0)
		{
			PRIV.dq.add(t);
		}
		PRIV.dq.norm();

		PRIV.c.copy(PRIV.p);
		PRIV.c.invmodp(PRIV.q);

		return;
	}

/* Mask Generation Function */

	public static void MGF1(sbyte[] Z, int olen, sbyte[] K)
	{
		HASH H = new HASH();
		int hlen = HASH.len;
		sbyte[] B = new sbyte[hlen];

		int counter , cthreshold , k = 0;
		for (int i = 0;i < K.Length;i++)
		{
			K[i] = 0;
		}

		cthreshold = olen / hlen;
		if (olen % hlen != 0)
		{
			cthreshold++;
		}
		for (counter = 0;counter < cthreshold;counter++)
		{
			H.process_array(Z);
			H.process_num(counter);
			B = H.hash();

			if (k + hlen > olen)
			{
				for (int i = 0;i < olen % hlen;i++)
				{
					K[k++] = B[i];
				}
			}
			else
			{
				for (int i = 0;i < hlen;i++)
				{
					K[k++] = B[i];
				}
			}
		}
	}

	public static void printBinary(sbyte[] array)
	{
		int i;
		for (i = 0;i < array.Length;i++)
		{
			Console.Write("{0:x2}", array[i]);
		}
		Console.WriteLine();
	}

	/* OAEP Message Encoding for Encryption */
	public static sbyte[] OAEP_ENCODE(sbyte[] m, RAND rng, sbyte[] p)
	{
		int i , slen , olen = RFS - 1;
		int mlen = m.Length;
		int hlen, seedlen;
		sbyte[] f = new sbyte[RFS];

		HASH H = new HASH();
		hlen = HASH.len;
		sbyte[] SEED = new sbyte[hlen];
		seedlen = hlen;
		if (mlen > olen - hlen - seedlen - 1)
		{
			return new sbyte[0];
		}

		sbyte[] DBMASK = new sbyte[olen - seedlen];

		if (p != null)
		{
			H.process_array(p);
		}
		sbyte[] h = H.hash();
		for (i = 0;i < hlen;i++)
		{
			f[i] = h[i];
		}

		slen = olen - mlen - hlen - seedlen - 1;

		for (i = 0;i < slen;i++)
		{
			f[hlen + i] = 0;
		}
		f[hlen + slen] = 1;
		for (i = 0;i < mlen;i++)
		{
			f[hlen + slen + 1 + i] = m[i];
		}

		for (i = 0;i < seedlen;i++)
		{
			SEED[i] = (sbyte)rng.Byte;
		}
		MGF1(SEED,olen - seedlen,DBMASK);

		for (i = 0;i < olen - seedlen;i++)
		{
			DBMASK[i] ^= f[i];
		}
		MGF1(DBMASK,seedlen,f);

		for (i = 0;i < seedlen;i++)
		{
			f[i] ^= SEED[i];
		}

		for (i = 0;i < olen - seedlen;i++)
		{
			f[i + seedlen] = DBMASK[i];
		}

		/* pad to length RFS */
		int d = 1;
		for (i = RFS - 1;i >= d;i--)
		{
			f[i] = f[i - d];
		}
		for (i = d - 1;i >= 0;i--)
		{
			f[i] = 0;
		}

		return f;
	}

	/* OAEP Message Decoding for Decryption */
	public static sbyte[] OAEP_DECODE(sbyte[] p, sbyte[] f)
	{
		int x, t;
		bool comp;
		int i , k , olen = RFS - 1;
		int hlen, seedlen;

		HASH H = new HASH();
		hlen = HASH.len;
		sbyte[] SEED = new sbyte[hlen];
		seedlen = hlen;
		sbyte[] CHASH = new sbyte[hlen];

		if (olen < seedlen + hlen + 1)
		{
			return new sbyte[0];
		}
		sbyte[] DBMASK = new sbyte[olen - seedlen];
		for (i = 0;i < olen - seedlen;i++)
		{
			DBMASK[i] = 0;
		}

		if (f.Length < RFS)
		{
			int d = RFS - f.Length;
			for (i = RFS - 1;i >= d;i--)
			{
				f[i] = f[i - d];
			}
			for (i = d - 1;i >= 0;i--)
			{
				f[i] = 0;
			}

		}

		if (p != null)
		{
			H.process_array(p);
		}
		sbyte[] h = H.hash();
		for (i = 0;i < hlen;i++)
		{
			CHASH[i] = h[i];
		}

		x = f[0];

		for (i = seedlen;i < olen;i++)
		{
			DBMASK[i - seedlen] = f[i + 1];
		}

		MGF1(DBMASK,seedlen,SEED);
		for (i = 0;i < seedlen;i++)
		{
			SEED[i] ^= f[i + 1];
		}
		MGF1(SEED,olen - seedlen,f);
		for (i = 0;i < olen - seedlen;i++)
		{
			DBMASK[i] ^= f[i];
		}

		comp = true;
		for (i = 0;i < hlen;i++)
		{
			if (CHASH[i] != DBMASK[i])
			{
				comp = false;
			}
		}

		for (i = 0;i < olen - seedlen - hlen;i++)
		{
			DBMASK[i] = DBMASK[i + hlen];
		}

		for (i = 0;i < hlen;i++)
		{
			SEED[i] = CHASH[i] = 0;
		}

		for (k = 0;;k++)
		{
			if (k >= olen - seedlen - hlen)
			{
				return new sbyte[0];
			}
			if (DBMASK[k] != 0)
			{
				break;
			}
		}

		t = DBMASK[k];
		if (!comp || x != 0 || t != 0x01)
		{
			for (i = 0;i < olen - seedlen;i++)
			{
				DBMASK[i] = 0;
			}
			return new sbyte[0];
		}

		sbyte[] r = new sbyte[olen - seedlen - hlen - k - 1];

		for (i = 0;i < olen - seedlen - hlen - k - 1;i++)
		{
			r[i] = DBMASK[i + k + 1];
		}

		for (i = 0;i < olen - seedlen;i++)
		{
			DBMASK[i] = 0;
		}

		return r;
	}

	/* destroy the Private Key structure */
	public static void PRIVATE_KEY_KILL(rsa_private_key PRIV)
	{
		PRIV.p.zero();
		PRIV.q.zero();
		PRIV.dp.zero();
		PRIV.dq.zero();
		PRIV.c.zero();
	}

	/* RSA encryption with the public key */
	public static void ENCRYPT(rsa_public_key PUB, sbyte[] F, sbyte[] G)
	{
		int n = PUB.n.getlen();
		FF f = new FF(n);

		FF.fromBytes(f,F);
		f.power(PUB.e,PUB.n);
		f.toBytes(G);
	}

	/* RSA decryption with the private key */
	public static void DECRYPT(rsa_private_key PRIV, sbyte[] G, sbyte[] F)
	{
		int n = PRIV.p.getlen();
		FF g = new FF(2 * n);

		FF.fromBytes(g,G);
		FF jp = g.dmod(PRIV.p);
		FF jq = g.dmod(PRIV.q);

		jp.skpow(PRIV.dp,PRIV.p);
		jq.skpow(PRIV.dq,PRIV.q);

		g.zero();
		g.dscopy(jp);
		jp.mod(PRIV.q);
		if (FF.comp(jp,jq) > 0)
		{
			jq.add(PRIV.q);
		}
		jq.sub(jp);
		jq.norm();

		FF t = FF.mul(PRIV.c,jq);
		jq = t.dmod(PRIV.q);

		t = FF.mul(jq,PRIV.p);
		g.add(t);
		g.norm();

		g.toBytes(F);
	}
}
