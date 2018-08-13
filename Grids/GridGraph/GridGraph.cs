using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System;

namespace Tracker.Grids{
	[CreateAssetMenu(fileName = "GridGraph", menuName = "A*/Grid Graph", order = 1)]
	public class GridGraph : ANodeTree<Vector2> {
		#region Inspector variables
		// Grid settings
		public Vector3 GridExtends;
		public Vector3 GridCenter;
		public float NodeSize = 5;

		// obstacle settings
		public float MaxSlope = 45;
		public float ObstaclePadding = 1f;
		public LayerMask TerrainLayer;
		public LayerMask ObstacleLayer;

		// debug settings
		public bool DrawUnwalkableNodes = true;
		public Color WalkableColor = Color.cyan;
		public Color CeilingWalkableColor = Color.yellow;
		public Color NoneWalkableColor = Color.red;
		#endregion

		#region Statics
		public const int MaxNodeDebugCount = 200000;
		#endregion

		#region Private variables
		// grid data
		private GridNode[] GridNodes = new GridNode[0];

		// temp variables
		private int TotalNodeCount;
		private Vector2 GridNodeCount;
		private Vector2 GridNodeScale;
		private Vector2 GridActualScale;
		private Vector2 CastExtends;
		private Vector2 GridBasePosition;
		private Terrain _Terrain;
		private bool _RegionUpdateLocked;

		// Regions
		private int _RegionCount;
		private Color[] RegionColors = new Color[0];
		#endregion

		#region implemented abstract members of ANodeTree
		protected override int NeighborsCountCustom {
			get {
				return 8; // always 8 neighbors
			}
		}
		public override float HeuristicCustom (int A, int B)
		{
			Vector2 AVector = GridNodes [A].Position;
			Vector2 BVector = GridNodes [B].Position;

			float dx = AbsoluteValue (AVector.x - BVector.x);
			float dy = AbsoluteValue (AVector.y - BVector.y);

            return (dx + dy) * 10;
		}


        // un-used.
		public override float CostCustom (int A, int B)
		{
			Vector2 AVector = GridNodes [A].Position;
			Vector2 BVector = GridNodes [B].Position;

			float dx = AbsoluteValue (AVector.x - BVector.x);
			float dy = AbsoluteValue (AVector.y - BVector.y);

            return (dx + dy) * 10;
        }

		public override void NeighborsCustom (ref int[] CachedArray,int CurrentIndex,bool AlwaysReturn = false)
		{
			int CurrentRow = (int)((float)CurrentIndex / (float)GridNodeCount.x);
			CachedArray[0] = ReturnNeighborsIndex(CurrentIndex + 1,CurrentRow, AlwaysReturn);  // return right
			CachedArray[1] = ReturnNeighborsIndex(CurrentIndex - 1,CurrentRow, AlwaysReturn); // return left
			CachedArray[2] = ReturnNeighborsIndex(CurrentIndex + (int)(GridNodeCount.x),CurrentRow+1, AlwaysReturn); // return bottom
			CachedArray[3] = ReturnNeighborsIndex(CurrentIndex - (int)(GridNodeCount.x),CurrentRow-1, AlwaysReturn);// return top
			CachedArray[4] = ReturnNeighborsIndex(CurrentIndex - (int)(GridNodeCount.x - 1f),CurrentRow-1, AlwaysReturn);// return top / left
			CachedArray[5] = ReturnNeighborsIndex(CurrentIndex - (int)(GridNodeCount.x + 1f),CurrentRow-1, AlwaysReturn); // return top / right
			CachedArray[6] = ReturnNeighborsIndex((CurrentIndex + (int)(GridNodeCount.x - 1f)),CurrentRow+1, AlwaysReturn);// return bottom / left
			CachedArray[7] = ReturnNeighborsIndex((CurrentIndex + (int)(GridNodeCount.x + 1f)),CurrentRow+1, AlwaysReturn);// return bottom / right
		}
        protected override void UpdateCustom ()
		{
			UpdateAllNodes ();
		}

		protected override void UpdateCustom (Bounds UpdateBounds)
		{
			UpdateNodes (new Vector2(UpdateBounds.center.x,UpdateBounds.center.z),new Vector2(UpdateBounds.extents.x,UpdateBounds.extents.z));
		}

		protected override void InitializeCustom ()
		{
			RegenerateGrid (); // generate grid
			UpdateCustom (); // update nodes
			UpdateRegions(); // update regions
		}

		protected override void DebugCustom ()
		{
			// draw bounds
			Gizmos.color = new Color(0,0,0,0.6f);
			Gizmos.DrawWireCube (GridCenter,new Vector3(GridExtends.x * 2f,GridExtends.y * 2f,GridExtends.z * 2f));

			// draw grid points
			GizmosDrawPoints();
		}
		protected override int GetNodeIndex (Vector2 A)
		{
			return VectorToIndex (A);
		}
		protected override object GetObjectByIndex (int Index)
		{
			return GridNodes [Index].Position; 
		}
		protected override int GetNodeCount {
			get {
				return this.TotalNodeCount;
			}
		}
		protected override int GetClosedsedAvailableCustom(int CurrentIndex, object PointB,int CurrentRegion)
        {
			if (ValidAndEnabled(CurrentIndex,CurrentRegion))
            {
                return CurrentIndex;
            }

            int node = -1;
            int tick = 0;
            int walkables = 0;

            Search.TrackerSearchNode[] Closed = new Search.TrackerSearchNode[NodeCount];
            Search.TrackerSearchNode[] Open = new Search.TrackerSearchNode[NodeCount];
            List<int> OpenIndexies = new List<int>();
            List<int> ClosedIndexies = new List<int>();

            Vector2 CurrentPosition = IndexToVector(CurrentIndex);
            Search.TrackerSearchNode CurrentNode = new Search.TrackerSearchNode(CurrentIndex);
            Open[CurrentIndex] = CurrentNode;

            Vector2 Dir = (CurrentPosition - (Vector2)PointB).normalized;
            int[] NeighborsCache = new int[NeighborsCount];
			int BIndex = NodeIndex (PointB);
			HashSet<int> DiscoverdRegions = new HashSet<int> ();

			while (walkables < 50 && tick < 100)
            {
                ClosedIndexies.Add(CurrentNode.Index);
                Closed[CurrentNode.Index] = CurrentNode;
                Neighbors(ref NeighborsCache, CurrentNode.Index,true);

                for (int i = 0; i < NeighborsCache.Length; i++)
                {
					if (Open[NeighborsCache [i]] == null) {
						Open [NeighborsCache [i]] = new Search.TrackerSearchNode (NeighborsCache [i]);
						Open [NeighborsCache [i]].Cost = 10 + CurrentNode.Cost;
						Open [NeighborsCache [i]].Heuristic = Heuristic (NeighborsCache [i], BIndex) + 10;
						OpenIndexies.Add (NeighborsCache [i]);
					}
                }

                Search.TrackerSearchNode lowest = Open[OpenIndexies[0]];

                for (int i = 0; i < OpenIndexies.Count; i++)
                {
					if (lowest.Cost > Open[OpenIndexies[i]].Cost) { lowest = Open[OpenIndexies[i]]; }
                }

                CurrentNode = lowest;
				OpenIndexies.Remove(CurrentNode.Index);

				if (ValidAndEnabled(CurrentNode.Index))
                {
					if (!DiscoverdRegions.Contains (CurrentNode.Index)) {
						DiscoverdRegions.Add (GridNodes[CurrentNode.Index].Region);
					}
                    walkables++;
                }

                tick++;
            }
				
            OpenIndexies.Clear();

			bool ContainsCurrentRegion = DiscoverdRegions.Contains (CurrentRegion); // check if found current region as surrounding region

			for (int i = 0; i < ClosedIndexies.Count; i++)
            {
				Vector2 Vecc = IndexToVector (ClosedIndexies[i]);
				if (ValidAndEnabled(ClosedIndexies[i],(ContainsCurrentRegion)?CurrentRegion:-1)) { OpenIndexies.Add (ClosedIndexies[i]); }
            }

			if (OpenIndexies.Count > 0) {
				Search.TrackerSearchNode low = Closed [OpenIndexies [0]];
				for (int i = 0; i < OpenIndexies.Count; i++) {
				
					if (low.TotalCost > Closed [OpenIndexies [i]].TotalCost) {
						low = Closed [OpenIndexies [i]];
					}
				}

				node = low.Index;
				return node;
			}

			return CurrentIndex;
        }
        #endregion

        #region Publics
		public int GetClosestNode(Vector2 Position){
			return VectorToIndex (Position,true);
		}
		public void DrawNode(int Index,float Duration = 5f){
			UnityEngine.Debug.DrawRay (GridNodes[Index].GetPosition(GridNodes[Index].Position),Vector3.up * 5f, Color.yellow,Duration);
		}
		public override bool ValidAndEnabled(int Index,int RegionCheck = -1)
        {
			if (Index < GridNodes.Length - 1 && Index > 0 && GridNodes[Index].Enabled && (RegionCheck < 0 || GridNodes[Index].Region == RegionCheck))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
		public void UpdateRegions()
		{
			UpdateRegions (null);
		}
		public void UpdateRegionsAsync(){
			if (!_RegionUpdateLocked) {
				_RegionUpdateLocked = true;
				ThreadPool.QueueUserWorkItem (UpdateRegions);
			}
		}
		public int GetCurrentRegion(Vector3 Position,int LastRegion){
			int PosIndex = VectorToIndex (new Vector2(Position.x,Position.z));
			if (!GridNodes [PosIndex].Enabled) {return LastRegion;}
			return GridNodes [PosIndex].Region;
		}
		public bool IsReachable(Vector3 From, Vector3 To){
			int FromNode = VectorToIndex (new Vector2(From.x,From.z));
			int ToNode = VectorToIndex (new Vector2(To.x,To.z));
			return (GridNodes [FromNode].Region == GridNodes [ToNode].Region) && GridNodes[ToNode].Enabled;
		}
		#endregion

        #region Private voids
		// Region voids
		private void UpdateRegions(object Data){
			int[] Gridnodesindexes = new int[NodeCount];
			for (int i = 0; i < Gridnodesindexes.Length; i++) {
				Gridnodesindexes [i] = i;
			}

			FastIndexList Remaining = new FastIndexList (Gridnodesindexes);
			bool[] EvaluatedNodes = new bool[NodeCount];
			_RegionCount = 0;

			while (Remaining.Length > 0) {
				bool BreakOut = false;
				while (Remaining.Length > 0 && !GridNodes [Remaining.Peek ()].Enabled) {
					Remaining.Pop (); // pop first
					if (Remaining.Length <= 0) {BreakOut = true;}
				}

				if (BreakOut) {
					break;
				} else { // no break out
					Stack<int> Evaluated = new Stack<int> ();
					Evaluated.Push (Remaining.Pop ()); // get first enabled node

					while (Evaluated.Count != 0) {
						int temp = Evaluated.Pop ();
						int Row = (int)(temp / GridNodeCount.x);
						int RelativeX = temp - (int)(Row * GridNodeCount.x);

						while (Row >= 0 && !EvaluatedNodes [(int)(Row * GridNodeCount.x) + RelativeX] && GridNodes [(int)(Row * GridNodeCount.x) + RelativeX].Enabled) {
							Row--;
						}
						Row++;

						bool spanLeft = false;
						bool spanRight = false;
						while (Row < GridNodeCount.y && !EvaluatedNodes [(int)(Row * GridNodeCount.x) + RelativeX] && GridNodes [(int)(Row * GridNodeCount.x) + RelativeX].Enabled) {
							int CurrentIndex = (int)(Row * GridNodeCount.x) + RelativeX;
							Remaining.RemoveTrackIndex (CurrentIndex);
							EvaluatedNodes [CurrentIndex] = true;
							GridNodes [CurrentIndex].Region = _RegionCount;

							if (!spanLeft && RelativeX > 0 && !EvaluatedNodes [CurrentIndex - 1] && GridNodes [CurrentIndex - 1].Enabled) {
								Evaluated.Push (CurrentIndex - 1);
								spanLeft = true;
							} else if (spanLeft && (RelativeX - 1 <= 0 || (EvaluatedNodes [CurrentIndex - 1] || !GridNodes [CurrentIndex - 1].Enabled))) {
								spanLeft = false;
							}

							if (!spanRight && RelativeX < GridNodeCount.x - 1 && !EvaluatedNodes [CurrentIndex + 1] && GridNodes [CurrentIndex + 1].Enabled) {
								Evaluated.Push (CurrentIndex + 1);
								spanRight = true;
							} else if (spanRight && (RelativeX < GridNodeCount.x - 1 || (EvaluatedNodes [CurrentIndex + 1] || !GridNodes [CurrentIndex + 1].Enabled))) {
								spanRight = false;
							}
							Row++;
						}
					}
				}

				_RegionCount++; // add region
			}

			// set region count and colors
			RegionColors = new Color[_RegionCount];
			System.Random Random = new System.Random();
			for (int i = 0; i < RegionColors.Length; i++) {
				RegionColors [i] = new Color32 (
					(byte)Random.Next(0,255),
					(byte)Random.Next(0,255),
					(byte)Random.Next(0,255),
					(byte)255
				);
			}

			_RegionUpdateLocked = false;
		}

		// Interface functions
		private int ReturnNeighborsIndex(int Index,int CurrentRow,bool ReturnIfFailed = false){
			if((NodeValidationCheck(Index,CurrentRow) && IsEnabledNode(Index)) || ReturnIfFailed)
            {
				return Index;
			}
			return -1;
		}

        // generations voids
        private void RegenerateGrid(){
			RecalculateTempValues (); // calculate temp values
			GridNodes = new GridNode[TotalNodeCount];

			for (int i = 0; i < GridNodes.Length; i++) {
				GridNodes [i] = new GridNode (IndexToVector(i));
			}
		}
		private void RecalculateTempValues(){
			GridNodeCount = new Vector2 ( // calculate node count
				(int)((GridExtends.x * 2f) / NodeSize),
				(int)((GridExtends.z * 2f) / NodeSize)
			);

			GridNodeScale = new Vector2 ( // calculate node scale
				(GridExtends.x * 2f) / (GridNodeCount.x - 1),
				(GridExtends.z * 2f) / (GridNodeCount.y - 1)
			);

			GridActualScale = new Vector2 ( // calculate node scale
				(GridExtends.x * 2f) / (GridNodeCount.x),
				(GridExtends.z * 2f) / (GridNodeCount.y)
			);

			TotalNodeCount = (int)(GridNodeCount.x * GridNodeCount.y); // calculate total node count

			CastExtends = new Vector2 (GridCenter.y - GridExtends.y,GridCenter.z + GridExtends.z); // calculate cast extends

			GridBasePosition = new Vector2 (GridCenter.x - GridExtends.x,GridCenter.z - GridExtends.z);
		}

		// calculation voids
		private void IndexToRowColums(int Index, System.Action<int,int> Results){
			var Divide = (float)Index / (float)GridNodeCount.x;
			var Row = (int)Divide;
			var Colum = Mathf.RoundToInt((Divide - Row)* (float)GridNodeCount.x);
			Results.Invoke (Row,Colum);
		}
		private Vector2 IndexToRowColums(int Index){
			var Divide = (float)Index / (float)GridNodeCount.x;
			var Row = (int)Divide;
			var Colum = Mathf.RoundToInt((Divide - Row)* (float)GridNodeCount.x);
			return new Vector2 (Row,Colum); 
		}
		public Vector2 IndexToVector(int Index){
			Vector2 ReturnVal = new Vector2 (0,0);
			IndexToRowColums (Index,(int Row,int Colum)=>{
				ReturnVal = new Vector2(
					Colum * GridNodeScale.x,
					Row * GridNodeScale.y
				);
			});
			return GridBasePosition + ReturnVal;
		}
		private void VectorToRowColums(Vector2 Vector,System.Action<int,int> Results,bool ToNode = true){
			if (ToNode) {Vector = RoundToNode (Vector,true);} // round to node vector
			Vector -= GridBasePosition; // make relative position
			int Colum = (int)(Vector.x / GridActualScale.x);
			int Row = (int)(Vector.y / GridActualScale.y);
			Results.Invoke (Row,Colum);
		}
		private Vector2 VectorToRowColums(Vector2 Vector,bool ToNode = true){
			Vector2 Vec = Vector2.zero;
			VectorToRowColums (Vector, (int Row,int Colum) => {
				Vec.x = Row;
				Vec.y = Colum;
			}, ToNode);
			return Vec;
		}
		private int VectorToIndex(Vector2 Vector,bool ToNode = true){
			int Index = 0;
			VectorToRowColums (Vector, (int Row, int Colum) => {
				Index = (int)(Row * GridNodeCount.x) + Colum;
			});
			return Index;
		}

		// check
		private bool NodeValidationCheck(int Index, int Row){
			if (Index < 0 || Index >= TotalNodeCount) {return false;} // is no valid index falls outside array
			int IndexRow = (int)((float)Index / (float)GridNodeCount.x);
			return Row == IndexRow; // return wheter the given row is equal to the calculated index row
		}
		private bool IsEnabledNode(int Index){
			return GridNodes [Index].Enabled;
		}

		// Rounding
		private float RoundTo(float Value,float Round){
			return Mathf.Round (Value / Round) * Round;
		}
		private Vector2 RoundTo(Vector2 Value, float Round, Vector2 GridOffset = default(Vector2)){
			Value -= GridOffset;
			Value.x = RoundTo (Value.x,Round);
			Value.y = RoundTo (Value.y,Round);
			Value += GridOffset;
			return Value;
		}
		private Vector2 RoundTo(Vector2 Value, Vector2 Round, Vector2 GridOffset = default(Vector2)){
			Value -= GridOffset;
			Value.x = RoundTo (Value.x,Round.x);
			Value.y = RoundTo (Value.y,Round.y);
			Value += GridOffset;
			return Value;
		}
		private Vector2 RoundToNode(Vector2 Vector,bool Clamp = false){
			if (Clamp) { // if should clamp vector
				Vector.x = Mathf.Clamp (Vector.x,GridCenter.x - GridExtends.x,GridCenter.x + GridExtends.x);
				Vector.y = Mathf.Clamp (Vector.y,GridCenter.z - GridExtends.z,GridCenter.z + GridExtends.z);}

			return RoundTo (Vector,GridNodeScale,GridBasePosition);
		}

		// floor / ceil
		private Vector2 RoundToNodeFloorCeil(Vector2 Vector,bool Clamp = false, bool RoundDown = false){
			if (Clamp) { // if should clamp vector
				Vector.x = Mathf.Clamp (Vector.x,GridCenter.x - GridExtends.x,GridCenter.x + GridExtends.x);
				Vector.y = Mathf.Clamp (Vector.y,GridCenter.z - GridExtends.z,GridCenter.z + GridExtends.z);}

			return RoundToFloorCeil (Vector,GridNodeScale,GridBasePosition,RoundDown);
		}
		private Vector2 RoundToFloorCeil(Vector2 Value, float Round, Vector2 GridOffset = default(Vector2), bool RoundDown = false){
			Value -= GridOffset;
			Value.x = RoundToFloorCeil (Value.x,Round,RoundDown);
			Value.y = RoundToFloorCeil (Value.y,Round,RoundDown);
			Value += GridOffset;
			return Value;
		}
		private Vector2 RoundToFloorCeil(Vector2 Value, Vector2 Round, Vector2 GridOffset = default(Vector2), bool RoundDown = false){
			Value -= GridOffset;
			Value.x = RoundToFloorCeil (Value.x,Round.x,RoundDown);
			Value.y = RoundToFloorCeil (Value.y,Round.y,RoundDown);
			Value += GridOffset;
			return Value;
		}
		private float RoundToFloorCeil(float Value,float Round, bool RoundDown = false){
			if (RoundDown) {
				return Mathf.Floor (Value / Round) * Round;
			} else {
				return Mathf.Ceil (Value / Round) * Round;
			}
		}
		

		// Mathf
		private float AbsoluteValue(float val){
			if (val < 0) {
				return -val;
			}
			return val;
		}
		private void ClampCenterAndSize(ref Vector2 Center, ref Vector2 Extends){
			Vector2 TerrainMin = new Vector2 (GridCenter.x - GridExtends.x, GridCenter.z - GridExtends.z);
			Vector2 TerrainMax = new Vector2 (GridCenter.x + GridExtends.x, GridCenter.z + GridExtends.z);

			Vector2 Min = new Vector2 (Center.x - Extends.x, Center.y - Extends.y);
			Vector2 Max = new Vector2 (Center.x + Extends.x, Center.y + Extends.y);

			// X
			Min.x = Mathf.Clamp (Min.x,TerrainMin.x,TerrainMax.x);
			Max.x = Mathf.Clamp (Max.x,TerrainMin.x,TerrainMax.x);

			// Y
			Min.y = Mathf.Clamp (Min.y,TerrainMin.y,TerrainMax.y);
			Max.y = Mathf.Clamp (Max.y,TerrainMin.y,TerrainMax.y);

			Center = (Min + Max) * 0.5f;
			Extends = (Max - Center);
		}

		// loops
		private void ForEachIn(System.Action<int,GridNode> Callback,bool Wide = false){
			ForEachIn (new Vector2(GridCenter.x,GridCenter.z), new Vector2(GridExtends.x,GridExtends.z),Callback,Wide);
		}

		public void ForEachIn(Vector2 Center, Vector2 Extends,System.Action<int,GridNode> Callback,bool Wide = false){
			if (Wide) {
				ForEachInCore (Center, Extends, (Vector2 Pos, bool Min) => {
					return RoundToNodeFloorCeil (Pos, false, Min);
				}, Callback);
			} else {
				ForEachInCore (Center, Extends, (Vector2 Pos, bool Min) => {
					return RoundToNode (Pos, false);
				}, Callback);
			}
		}

		public void ForEach(System.Action<int,GridNode> Callback){
			for (int i = 0; i < GridNodes.Length; i++) {
				Callback.Invoke (i,GridNodes[i]);
			}
		}

		private void ForEachInCore(Vector2 Center, Vector2 Extends,System.Func<Vector2,bool,Vector2> RoundFunction,System.Action<int,GridNode> Callback){
			if (GridNodes != null && GridNodes.Length == TotalNodeCount) {
				ClampCenterAndSize (ref Center, ref Extends);
				if (Extends.x <= 0 || Extends.y <= 0) {return;}

				Vector2 Min = RoundFunction (new Vector2 (Center.x - Extends.x, Center.y - Extends.y),true);
				Vector2 Max = RoundFunction (new Vector2 (Center.x + Extends.x, Center.y + Extends.y),false);
				Vector2 MinIndexes = VectorToRowColums (Min,false);
				Vector2 MaxIndexes = VectorToRowColums (Max,false);

				for (int x = (int)MinIndexes.x; x <= MaxIndexes.x; x++) {       // x < GridNodeCount.x
					for (int y = (int)MinIndexes.y; y <= MaxIndexes.y; y++) {   // y < GridNodeCount.y
						Vector2 IndexData = new Vector2(x, y);
						int Index = (int)((IndexData.x * GridNodeCount.x) + IndexData.y);
						if (Index >= 0 && Index <= GridNodes.Length - 1) { Callback.Invoke(Index, GridNodes[Index]); }
					}
				}
			}
		}
			
		// update nodes full
		private void UpdateNodes(bool Wide = false){
			UpdateNodes (new Vector2(GridCenter.x,GridCenter.z), new Vector2(GridExtends.x,GridExtends.z),Wide);
		}
		public void UpdateNodes(Vector3 Center,Vector3 Extends,bool Wide = false){
			UpdateNodes(new Vector2(Center.x,Center.z),new Vector2(Extends.x,Extends.z),Wide);
		}
		public void UpdateNodes(Vector2 Center, Vector2 Extends,bool Wide = false){
			UpdateNodesCore (new object[]{Center,Extends},Wide);
		}
		public void UpdateNodesCore(object Data,bool Wide = false){
            lock (GridNodes)
            {
                if (Terrain != null)
                {
                    Vector3 TerrainPosition = Terrain.transform.position;
                    Vector3 TerrainSize = Terrain.terrainData.size;
                    var ArrayData = Data as object[];
                    Vector2 Center = (Vector2)ArrayData[0];
                    Vector2 Size = (Vector2)ArrayData[1];

                    ForEachIn(Center, Size, (int NodeIndex, GridNode Node) =>
                    {
                        Node.UpdateNode(Terrain, TerrainPosition, TerrainSize, NodeSize, ObstaclePadding, MaxSlope, CastExtends, TerrainLayer, ObstacleLayer, (GameObject FoundObject) =>
                        {
                            IDynamicObstacle Dynamic = FoundObject.GetComponent<IDynamicObstacle>(); // check if there is a dynamic obstacle
                            if (Dynamic != null)
                            {
                                return !Dynamic.IsObstacle;
                            } // check if object is dynamic
                            return false;
                        });
                    }, Wide);
                }
                else
                {
                    UnityEngine.Debug.LogError("There is no terrain assigned to this grid!");
                }
            }
		}

		// update terrain nodes
		public void UpdateTerrainNodes(){
			UpdateTerrainNodes (new Vector2(GridCenter.x,GridCenter.z), new Vector2(GridExtends.x,GridExtends.z));
		}
		public void UpdateTerrainNodes(Vector2 Center, Vector2 Extends){
			UpdateTerrainNodesCore (new object[]{Center,Extends});
		}
		public void UpdateTerrainNodesCore(object Data){
            lock (GridNodes)
            {
                if (Terrain != null)
                {
                    Vector3 TerrainPosition = Terrain.transform.position;
                    Vector3 TerrainSize = Terrain.terrainData.size;
                    var ArrayData = Data as object[];
                    Vector2 Center = (Vector2)ArrayData[0];
                    Vector2 Size = (Vector2)ArrayData[1];

                    ForEachIn(Center, Size, (int NodeIndex, GridNode Node) =>
                    {
                        Node.UpdateTerrainValues(Terrain, TerrainPosition, TerrainSize, MaxSlope, CastExtends);
                    });
                }
                else
                {
                    UnityEngine.Debug.LogError("There is no terrain assigned to this grid!");
                }
            }
		}

		// update all nodes
		private void UpdateAllNodes(){
			UpdateAllNodes (new object[]{new Vector2(GridCenter.x,GridCenter.z),new Vector2(GridExtends.x,GridExtends.z)});
		}
		public void UpdateAllNodes(object Data){
            lock (GridNodes)
            {
                if (Terrain != null)
                {
                    Vector3 TerrainPosition = Terrain.transform.position;
                    Vector3 TerrainSize = Terrain.terrainData.size;

                    var ArrayData = Data as object[];
                    ForEach((int NodeIndex, GridNode Node) =>
                    {
                        Node.UpdateNode(Terrain, TerrainPosition, TerrainSize, NodeSize, ObstaclePadding, MaxSlope, CastExtends, TerrainLayer, ObstacleLayer, (GameObject FoundObject) =>
                        {
                            IDynamicObstacle Dynamic = FoundObject.GetComponent<IDynamicObstacle>(); // check if there is a dynamic obstacle
                            if (Dynamic != null)
                            {
                                return !Dynamic.IsObstacle;
                            } // check if object is dynamic
                            return false;
                        });
                    });
                }
                else
                {
                    UnityEngine.Debug.LogError("There is no terrain assigned to this grid!");
                }
            }
		}

		// Debug voids
		private void GizmosDrawPoints(){
			GizmosDrawPoints (new Vector2(GridExtends.x,GridExtends.z));
		}
		private void GizmosDrawPoints(Vector2 Extends){
			if (TotalNodeCount < GridGraph.MaxNodeDebugCount) {
				float DrawNodeSize = NodeSize / 3f;
				for (int i = 0; i < GridNodes.Length; i++) {
					var node = GridNodes [i];
					if (!node.Enabled && !DrawUnwalkableNodes) {continue;} // skip this itteration
					if (!node.Enabled && node.CeilDistance == 0f) {
						Gizmos.color = NoneWalkableColor;
					} else if (node.Enabled && (node.CeilDistance != node.CeilTerrainDistance)) {
						Gizmos.color = CeilingWalkableColor;
					} else {
						if (RegionColors.Length > node.Region) {
							Gizmos.color = RegionColors [node.Region];
						} else {
							Gizmos.color = WalkableColor;
						}
					}

					Vector3 Position = node.GetPosition(node.Position);
					Gizmos.DrawCube (Position, new Vector3 (DrawNodeSize, DrawNodeSize, DrawNodeSize));
				}
			}
		}

		// Get / Set Inner
		private Terrain Terrain{
			get{
				if (_Terrain == null) {
					_Terrain = Terrain.activeTerrain;
				}
				return _Terrain;
			}
		}
        #endregion

		#region Get / Set
		public bool RegionUpdateLock{
			get{
				return _RegionUpdateLocked;
			}
		}
		public int RegionCount{
			get{
				return _RegionCount;
			}
		}
		#endregion
    }
}