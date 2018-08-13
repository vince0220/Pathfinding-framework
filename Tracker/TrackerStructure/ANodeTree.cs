using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tracker{
    [System.Serializable]
    public abstract class ANodeTree<T> : ScriptableObject, INodeTree {
        #region Private variables
        [HideInInspector] [SerializeField] private bool _DebugEnabled;
        #endregion

        #region Abstract members
        public abstract float HeuristicCustom(int A, int B);
        public abstract float CostCustom(int A, int B);
        public abstract void NeighborsCustom(ref int[] CachedArray, int CurrentIndex,bool AlwaysReturn = false);
		public abstract bool ValidAndEnabled(int Index, int CurrentRegion = -1);  // added JG
        protected abstract void UpdateCustom();
        protected abstract void UpdateCustom(Bounds UpdateBounds);
        protected abstract void InitializeCustom();
        protected abstract void DebugCustom();
        protected abstract int GetNodeIndex(T A);
        protected abstract object GetObjectByIndex(int Index);
        protected abstract int GetNodeCount { get; }
        protected abstract int NeighborsCountCustom { get; }
		protected abstract int GetClosedsedAvailableCustom(int CurrentIndex, object PointB,int CurrentRegion);
        #endregion

        #region INodeTree implementation
        public float Heuristic(int A, int B)
        {
            return HeuristicCustom(A, B);
        }
        public float Cost(int A, int B)
        {
            return CostCustom(A, B);
        }
        public void Neighbors(ref int[] CachedArray, int CurrentIndex,bool AlwaysReturn = false)
        {
            NeighborsCustom(ref CachedArray, CurrentIndex,AlwaysReturn);
        }
		public void Initialize(){
			this.InitializeCustom ();
		}
		public void UpdateTree(Bounds UpdateBounds){
			this.UpdateCustom (UpdateBounds);
		}
		public void UpdateTree(){
			this.UpdateCustom ();
		}
		public void Debug(){
			if (DebugEnabled) { // only debug if is enabled
				this.DebugCustom ();
			}
		}
		public int NeighborsCount {
			get{
				return NeighborsCountCustom;
			}
		}
		public bool DebugEnabled{
			get{
				return _DebugEnabled;
			}
			set{
				_DebugEnabled = value;
			}
		}
		public int NodeIndex(object A){
			return GetNodeIndex ((T)A);
		}
		public object this[int i]{
			get{
				return GetObjectByIndex (i);
			}
		}
		public int NodeCount{
			get{
				return GetNodeCount;
			}
		}
		public bool Valid(int Index,int CurrentRegion = -1) // add by JG
        {
			return ValidAndEnabled(Index,CurrentRegion);
        }

		public int GetClosedAvailable(int CurrentIndex, object PointB, int CurrentRegion)
        {
			return GetClosedsedAvailableCustom(CurrentIndex, PointB,CurrentRegion);
        }
        #endregion
    }
}
