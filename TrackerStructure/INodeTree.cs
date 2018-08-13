using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tracker{
	public interface INodeTree {
		#region Search Functions 
		float Heuristic(int A, int B);
		float Cost(int A, int B);
		void Neighbors(ref int[] CachedArray,int CurrentIndex,bool AlwaysReturn = false);
		int GetClosedAvailable(int CurrentIndex, object PointB, int CurrentRegion);
		bool ValidAndEnabled(int Index,int CurrentRegion = -1);  // add by JG
        #endregion

        #region Request Functions
        int NodeIndex(object A);
		object this[int i]{get;}
		#endregion

		#region Tree functions
		void Initialize();
		void UpdateTree();
		void UpdateTree(Bounds UpdateBounds);
		void Debug();
		#endregion

		#region Tree properties
		int NeighborsCount {get;}
		bool DebugEnabled{get;set;}
		int NodeCount{ get;}
		#endregion
	}
}
