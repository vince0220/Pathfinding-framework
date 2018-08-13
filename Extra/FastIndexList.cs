using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FastIndexList {
	#region variables
	private int[] Indexes;
	private int[] KeepTrackOf;
	public int Length;
	#endregion

	#region Constructor
	public FastIndexList(int[] Indexes){
		this.Indexes = Indexes;
		this.KeepTrackOf = new int[this.Indexes.Length];
		System.Array.Copy (this.Indexes,this.KeepTrackOf,this.Indexes.Length);
		this.Length = Indexes.Length;
	}
	#endregion

	#region public voids
	public int Peek(){
		return Indexes [0];
	}
	public int Pop(){
		int Index = Indexes [0];
		RemoveAt (0);
		return Index;
	}
	public void RemoveTrackIndex(int Index){
		RemoveAt (KeepTrackOf[Index]);
	}
	public void RemoveAt(int Index){
		if (Length - 1 >= 0) {
			Length -= 1; // min length
			int CurrentIndex = Indexes [Index];

			KeepTrackOf [Indexes [Length]] = Index;
			KeepTrackOf [Indexes [Index]] = Length;

			Indexes [Index] = Indexes [Length]; // set current
			Indexes[Length] = CurrentIndex; // set last
		}
	}
	public int GetIndexAt(int Index){
		return Indexes [Index];
	}
	public int GetIndexAtTracked(int Index){
		return Indexes [KeepTrackOf [Index]];
	}
	#endregion
	
}
