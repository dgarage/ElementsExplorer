using NBitcoin;
using System.Linq;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin.Crypto;

namespace ElementsExplorer
{
	public class UTXOChanges : IBitcoinSerializable
	{
		byte _IsDiff;
		public bool Reset
		{
			get
			{
				return _IsDiff == 0;
			}
			set
			{
				_IsDiff = (byte)(value ? 0 : 1);
			}
		}

		uint256 _BlockHash = uint256.Zero;
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


		uint256 _UnconfirmedHash = uint256.Zero;
		public uint256 UnconfirmedHash
		{
			get
			{
				return _UnconfirmedHash;
			}
			set
			{
				_UnconfirmedHash = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _IsDiff);
			stream.ReadWrite(ref _BlockHash);
			stream.ReadWrite(ref _UnconfirmedHash);
			stream.ReadWrite(ref _Unconfirmed);
			stream.ReadWrite(ref _Confirmed);
		}

		UTXOChange _Unconfirmed = new UTXOChange();
		public UTXOChange Unconfirmed
		{
			get
			{
				return _Unconfirmed;
			}
			set
			{
				_Unconfirmed = value;
			}
		}


		UTXOChange _Confirmed = new UTXOChange();
		public UTXOChange Confirmed
		{
			get
			{
				return _Confirmed;
			}
			set
			{
				_Confirmed = value;
			}
		}

		public bool HasChange
		{
			get
			{
				return Confirmed.HasChanges || Unconfirmed.HasChanges;
			}
		}
	}
	public class UTXOChange : IBitcoinSerializable
	{

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

		public bool HasChanges
		{
			get
			{
				return UTXOs.Count != 0 || SpentOutpoints.Count != 0;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _SpentOutpoints);
			stream.ReadWrite(ref _UTXOs);
		}

		public void LoadChanges(Transaction tx, Func<Script, KeyPath> getKeyPath)
		{
			if(tx == null)
				throw new ArgumentNullException("tx");
			tx.CacheHashes();

			
			var existingUTXOs = new HashSet<OutPoint>(UTXOs.Select(u => u.Outpoint));
			var spentOutpoints = new HashSet<OutPoint>(SpentOutpoints);

			foreach(var input in tx.Inputs)
			{
				if(existingUTXOs.Remove(input.PrevOut))
				{
					SpentOutpoints.Add(input.PrevOut);
				}
			}

			int index = -1;
			foreach(var output in tx.Outputs)
			{
				index++;
				if(!existingUTXOs.Contains(new OutPoint(tx.GetHash(), index)))
				{
					var keyPath = getKeyPath(output.ScriptPubKey);
					if(keyPath != null)
					{
						UTXOs.Add(new UTXO(new OutPoint(tx.GetHash(), index), output, keyPath));
					}
				}
			}
		}

		public bool HasConflict(Transaction tx)
		{
			var existingUTXOs = new HashSet<OutPoint>(UTXOs.Select(u => u.Outpoint));
			var spentOutpoints = new HashSet<OutPoint>(SpentOutpoints);

			//Check for conflicts
			foreach(var input in tx.Inputs)
			{
				if(spentOutpoints.Contains(input.PrevOut))
					return true;
				spentOutpoints.Add(input.PrevOut);
			}

			var index = -1;
			foreach(var output in tx.Outputs)
			{
				index++;
				var outpoint = new OutPoint(tx.GetHash(), index);
				if(existingUTXOs.Contains(outpoint))
					return true;
				existingUTXOs.Add(outpoint);
			}
			return false;
		}

		public UTXOChange Diff(UTXOChange previousChange)
		{
			var previousUTXOs = previousChange.UTXOs.ToDictionary(u => u.Outpoint);
			var currentUTXOs = UTXOs.ToDictionary(u => u.Outpoint);

			var deletedUTXOs = previousChange.UTXOs.Where(utxo => !currentUTXOs.ContainsKey(utxo.Outpoint));
			var addedUTXOs = UTXOs.Where(utxo => !previousUTXOs.ContainsKey(utxo.Outpoint));

			var diff = new UTXOChange();
			foreach(var deleted in deletedUTXOs)
			{
				diff.SpentOutpoints.Add(deleted.Outpoint);
			}
			foreach(var added in addedUTXOs)
			{
				diff.UTXOs.Add(added);
			}
			return diff;
		}

		public uint256 GetHash()
		{
			return Hashes.Hash256(this.ToBytes());
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
