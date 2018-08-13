using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Priority_Queue;

namespace Tracker.Search{
	public class TrackerSearchNode : FastPriorityQueueNode {
		#region Public variables
		public float Cost;
		public float Heuristic;
		public int Index;
		public int Parent;
		#endregion

		#region Constructor
		public TrackerSearchNode(int Index){
			this.Index = Index;
		}
		#endregion

		#region Get / Set
		public float TotalCost{
			get{
				return Cost + Heuristic;
			}
		}
		#endregion
	}
}
