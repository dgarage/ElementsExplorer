using NBitcoin;
using System.Linq;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace ElementsExplorer
{
	public class UTXOChanges : IBitcoinSerializable
	{

		uint256 _BlockHash;
		public uint256 BlockHash
		{
			get
			{
				return _BlockHash;
			}
			set
			{
				_BlockHash = value;
			}
		}


		List<OutPoint> _SpentOutpoints = new List<OutPoint>();
		public List<OutPoint> SpentOutpoints
		{
			get
			{
				return _SpentOutpoints;
			}
			set
			{
				_SpentOutpoints = value;
			}
		}


		List<UTXO> _UTXOs = new List<UTXO>();
		public List<UTXO> UTXOs
		{
			get
			{
				return _UTXOs;
			}
			set
			{
				_UTXOs = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _BlockHash);
			stream.ReadWrite(ref _SpentOutpoints);
			stream.ReadWrite(ref _UTXOs);
		}

		public bool LoadChanges(Transaction tx, Func<Script, KeyPath> getKeyPath)
		{
			if(tx == null)
				throw new ArgumentNullException("tx");
			tx.CacheHashes();
			bool change = false;
			if(UTXOs.Count != 0)
			{
				var existingUTXOs = UTXOs.ToDictionary(u => u.Outpoint);
				foreach(var input in tx.Inputs)
				{
					if(existingUTXOs.Remove(input.PrevOut))
					{
						SpentOutpoints.Add(input.PrevOut);
						change = true;
					}
				}
			}
			int index = -1;
			foreach(var output in tx.Outputs)
			{
				index++;
				var keyPath = getKeyPath(output.ScriptPubKey);
				if(keyPath != null)
				{
					UTXOs.Add(new UTXO(new OutPoint(tx.GetHash(), index), output, keyPath));
					change = true;
				}
			}
			return change;
		}
	}

	public class UTXO : IBitcoinSerializable
	{
		public UTXO()
		{

		}
		OutPoint _Outpoint = new OutPoint();
		public OutPoint Outpoint
		{
			get
			{
				return _Outpoint;
			}
			set
			{
				_Outpoint = value;
			}
		}


		Script _ScriptPubKey;
		public Script ScriptPubKey
		{
			get
			{
				return _ScriptPubKey;
			}
			set
			{
				_ScriptPubKey = value;
			}
		}


		ConfidentialAsset _Asset;
		public ConfidentialAsset Asset
		{
			get
			{
				return _Asset;
			}
			set
			{
				_Asset = value;
			}
		}


		ConfidentialValue _Value;
		public ConfidentialValue Value
		{
			get
			{
				return _Value;
			}
			set
			{
				_Value = value;
			}
		}


		ConfidentialNonce _Nonce;
		public ConfidentialNonce Nonce
		{
			get
			{
				return _Nonce;
			}
			set
			{
				_Nonce = value;
			}
		}


		byte[] _RangeProof;
		public byte[] RangeProof
		{
			get
			{
				return _RangeProof;
			}
			set
			{
				_RangeProof = value;
			}
		}


		byte[] _SurjectionProof;
		public byte[] SurjectionProof
		{
			get
			{
				return _SurjectionProof;
			}
			set
			{
				_SurjectionProof = value;
			}
		}


		KeyPath _KeyPath;
		
		public UTXO(OutPoint outPoint, TxOut output, KeyPath keyPath)
		{
			Outpoint = outPoint;
			RangeProof = output.RangeProof;
			SurjectionProof = output.SurjectionProof;
			Nonce = output.Nonce;
			Asset = output.Asset;
			Value = output.ConfidentialValue;
			ScriptPubKey = output.ScriptPubKey;
			KeyPath = keyPath;
		}

		public KeyPath KeyPath
		{
			get
			{
				return _KeyPath;
			}
			set
			{
				_KeyPath = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _Outpoint);
			stream.ReadWrite(ref _ScriptPubKey);
			stream.ReadWrite(ref _Asset);
			stream.ReadWrite(ref _Value);
			stream.ReadWrite(ref _Nonce);
			stream.ReadWriteAsVarString(ref _RangeProof);
			stream.ReadWriteAsVarString(ref _SurjectionProof);

			uint[] indexes = _KeyPath?.Indexes ?? new uint[0];
			stream.ReadWrite(ref indexes);
			if(!stream.Serializing)
				_KeyPath = new KeyPath(indexes);
		}
	}
}
