using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using Priority_Queue;
using System.Linq;

namespace Tracker.Search{
	public class TrackerSearch<T> {
		#region Private variables
		private INodeTree Tree;
		public bool CancelationPending = false;
		#endregion

		#region Const variables
		public const int MaxIterations = 1000; // Maximal amount of iterations to be done
		#endregion

		#region Constructur
		/// <summary>
		/// Initializes a new instance of the <see cref="Tracker.Search.TrackerSearch`1"/> class.
		/// </summary>
		/// <param name="Tree">The node tree to search in</param>
		public TrackerSearch(INodeTree Tree){
			this.Tree = Tree;
		}
		#endregion

		#region Input Voids
		public void FindPath(T From, T To, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, int CurrentRegion = -1,int? LastVisitedNode = null){
			FindPathCore (new object[]{
				From,
				To,
				OnCalculated,
				OnError,
				null,
				CurrentRegion,
				LastVisitedNode
			});
		}
		public void FindPathAsync(T From, T To, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, int CurrentRegion = -1,int? LastVisitedNode = null){
			ThreadPool.QueueUserWorkItem (FindPathCore,new object[]{
				From,
				To,
                OnCalculated,
				OnError,
				null,
				CurrentRegion,
				LastVisitedNode
			});
		}

        // Used to see if a path to a target is in range.
		public void FindPathTo(T From, T To, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, int CurrentRegion = -1,int? LastVisitedNode = null)
        {
			float Dist = Tree.Heuristic(Tree.NodeIndex(From), Tree.NodeIndex(To));
            FindPathCore(new object[]{
                From,
                To,
                OnCalculated,
                OnError,
				(object) new System.Func<float,bool> (delegate(float CurrentNodeHeuristic) {
					return (CurrentNodeHeuristic >= Dist);
				}),
				CurrentRegion,
				LastVisitedNode
            });
        }
		public void FindPathToAsync(T From, T To, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, int CurrentRegion = -1,int? LastVisitedNode = null)
        {
			float Dist = Tree.Heuristic(Tree.NodeIndex(From), Tree.NodeIndex(To));
            ThreadPool.QueueUserWorkItem(FindPathCore, new object[]{
                From,
                To,
                OnCalculated,
				OnError,
				(object) new System.Func<float,bool> (delegate(float CurrentNodeHeuristic) {
					return (CurrentNodeHeuristic >= Dist);
				}),
				CurrentRegion,
				LastVisitedNode
            });
        }

		public void FindDirectionPath(T From, Vector2 Direction, float Distance, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, System.Action<T> Point, int CurrentRegion = -1,int? LastVisitedNode = null)
        {
			EscapePoint(new object[]{
                From,
                Direction,
                Distance,
                OnCalculated,
                OnError,
                Point,
				CurrentRegion,
				LastVisitedNode
            });
        }
		public void FindDirectionPathAsync(T From, Vector2 Direction, float Distance, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, System.Action<T> Point, int CurrentRegion = -1,int? LastVisitedNode = null)
        {
			ThreadPool.QueueUserWorkItem(EscapePoint, new object[]{
                From,
                Direction,
                Distance,
                OnCalculated,
                OnError,
                Point,
				CurrentRegion,
				LastVisitedNode
            });
        }

		public void DestroySearcher(){
			CancelationPending = true;
		}
        #endregion

        #region Private voids
        private void FindPathCore(object Data)
        {
            // Fetching Initial data
            object[] DataArray = (object[])Data; // Convert data to data array
            T Start = (T)DataArray[0]; // From node
            T End = (T)DataArray[1]; // To node
            System.Action<Stack<T>> OnCalculated = (System.Action<Stack<T>>)DataArray[2]; // Convert calculated callback
            System.Action<string> OnError = (System.Action<string>)DataArray[3]; // Convert error callback
            System.Func<float, bool> ExitFilter = null;
			int CurrentRegion = -1;
			int? LastVisitedNode = null;
			if (DataArray.Length >= 5 && DataArray[4] != null) { ExitFilter = (System.Func<float, bool>)DataArray[4]; }
			if (DataArray.Length >= 6) {CurrentRegion = (int)DataArray[5];}
			if (DataArray.Length >= 7) {LastVisitedNode = (int?)DataArray [6];}

            int StartIndex = Tree.NodeIndex(Start); // find index of start node
            int EndIndex = Tree.NodeIndex(End); // find index of end node
            int[] NeighborsCache = new int[Tree.NeighborsCount];

			if (LastVisitedNode != null && Tree.ValidAndEnabled ((int)LastVisitedNode)) {;
				StartIndex = (int)LastVisitedNode;
			} else {
				StartIndex = Tree.GetClosedAvailable (StartIndex, (object)End, CurrentRegion);
			}
			
			EndIndex = Tree.GetClosedAvailable(EndIndex, (object)Start,CurrentRegion);

            if (StartIndex > Tree.NodeCount - 1 || StartIndex < 0 || !Tree.ValidAndEnabled(StartIndex))
            {
                if (CancelationPending) { return; }
                OnError.Invoke("Starting point is not part of Grid, or is not a valid walkable node");
                return;
            }
            else if (EndIndex > Tree.NodeCount - 1 || EndIndex < 0 || !Tree.ValidAndEnabled(EndIndex))
            {
                if (CancelationPending) { return; }
                OnError.Invoke("Desired destination is not part of Grid");
                return;
            }

            // Initialize base data
            List<TrackerSearchNode> Open = new List<TrackerSearchNode>();  //this list will hold the acive indexies of the Open list.
            List<TrackerSearchNode> Closed = new List<TrackerSearchNode>();
            TrackerSearchNode InitialNode = new TrackerSearchNode(EndIndex); // the initial node to start with
            int TickCount = 0; // tick counter

			Grids.GridGraph Graph = (Grids.GridGraph)Tree;

            // Edit base data
            InitialNode.Heuristic = Tree.Heuristic(StartIndex, InitialNode.Index); // calculate the heuristic of the initial node

            TrackerSearchNode CurrentNode = InitialNode;  // creates and assigns the currentnode variable as the initial node.

            Open.Add(CurrentNode);

            // start loop
            while (!CancelationPending && Open.Count > 0)
            { // tests to see if search to target is in the closed list.
                if (TickCount > MaxIterations) { break; } // check if the current iteration doesnt exceed the max iteration count

                Closed.Add(CurrentNode);

                if (CurrentNode.Index == StartIndex)
                { // checks if the current checking node is equal to the end index
                    Stack<T> Path = ReconstructPath(ref Closed, StartIndex, EndIndex); // Reconstruct the path
                    if (CancelationPending) { return; }
                    OnCalculated.Invoke(Path); // invoke the on calculated callback with the constructed path
                    return; // stop this void
                }

                if (!Tree.ValidAndEnabled(CurrentNode.Index))
                {
                    OnError.Invoke("Obstacle interupted search");
                    return;
                }

                Tree.Neighbors(ref NeighborsCache, CurrentNode.Index);
                for (int i = 0; i < NeighborsCache.Length; i++)
                {

                    if (!Tree.ValidAndEnabled(CurrentNode.Index))
                    {
                        OnError.Invoke("Obstacle interupted search");
                        return;
                    }

                    int Neighbor = NeighborsCache[i];
                    if (Neighbor < 0) { continue; } // continue since its not a valid neighbor

                    if (CancelationPending) { return; }
                    if (Neighbor < Tree.NodeCount - 1 && Neighbor > 0 && Tree.ValidAndEnabled(CurrentNode.Index))
                    {
                        TrackerSearchNode ClosedNode = null; // get neighbor out of closed array
                        TrackerSearchNode OpenNode = null; // get neighbor out of open array
                        foreach (TrackerSearchNode node in Closed)
                        {
                            if (node.Index == Neighbor)
                            {
                                ClosedNode = node;
                                break;
                            }
                        }

                        foreach (TrackerSearchNode node in Open)
                        {
                            if (node.Index == Neighbor)
                            {
                                OpenNode = node;
                                break;
                            }
                        }

                        float H = Tree.Heuristic(StartIndex, Neighbor); // calculate heuristic of neighbor
                        float G = CurrentNode.Cost + 10;
                        float F = H + G; // calculate f cost of neighbor

                        if (ExitFilter != null && ExitFilter.Invoke(H))
                        {
                            if (CancelationPending) { return; }
                            OnError.Invoke("Cant find a path. Path is broken by exit filter!");
                            return;
                        }

                        if (ClosedNode != null) { continue; } //checks to see if node exists in closed list and skips it if it is.
                        else
                        { // neighbor is not in closed array

                            if (OpenNode != null)  //if node is an open node.
                            {
                                if (OpenNode.TotalCost > F)  //checks to see if the this would make for a better parent or not.
                                {
                                    OpenNode.Heuristic = H; // set node heuristic
                                    OpenNode.Cost = G; // set node cost

                                    OpenNode.Parent = CurrentNode.Index;  //reassigns the parent 
                                }
                                else { continue; } // else skip
                            }
                            else
                            { // if node is a new node
                                TrackerSearchNode Node = new TrackerSearchNode(Neighbor); // create new node

                                Node.Heuristic = H; // set node heuristic
                                Node.Cost = G; // set node cost
                                Node.Parent = CurrentNode.Index; // set node parent

                                Open.Add(Node);  // add the index to a list for iteration later
                            }
                        }
                    }
                }

                if (!Tree.ValidAndEnabled(CurrentNode.Index))
                {
                    OnError.Invoke("Obstacle interupted search");
                    return;
                }

                if (CancelationPending) { return; }
                TrackerSearchNode lowest = Open[0];  //assigns a node at semi-random to be used for comparinsons to find the lowset cost node;

                for (int i = 0; i < Open.Count; i++) // iterate through the list of open indexies.
                {
                    if (lowest.TotalCost > Open[i].TotalCost) { lowest = Open[i]; } // checks this iterations open node against the assigned currentnode to see who costs less, the lowest cost one becomes the new current node
                }
                Open.Remove(CurrentNode);  // removes the current node from the open indexie list as it is the lowes cost node and will soon be added to the closed list.
                //Open[CurrentNode.Index] = null; // remove current checking node from the open array
                CurrentNode = lowest;  //assigns the new currentnode.

                if (!Tree.ValidAndEnabled(CurrentNode.Index))
                {
                    OnError.Invoke("Obstacle interupted search");
                    return;
                }

                TickCount++; // up tickcount
            }
            if (CancelationPending) { return; }
            OnError.Invoke("No path could be found from start to end"); // something has gone wrong cause you never reached the goal node. Invoke error callback
        }

        private Stack<T> ReconstructPath(ref List<TrackerSearchNode> ClosedArray, int StartIndex, int EndIndex)
        {
            Stack<T> Path = new Stack<T>(); // init new stack
            TrackerSearchNode Current = null; // set current exploring node to start

            foreach (TrackerSearchNode node in ClosedArray)
            {
                if (StartIndex == node.Index)
                {
                    Current = node; // set current to the parent of the current exploring
                    break;
                }
            }

            int pointCount = 0;
            while (Current.Index != EndIndex)
            { // loop until you reached the end index
                pointCount++;

                if (pointCount != 1)
                {
                    Path.Push((T)Tree[Current.Index]); // add current to the stack
                }

                foreach (TrackerSearchNode node in ClosedArray)
                {
                    if (Current.Parent == node.Index)
                    {
                        Current = node; // set current to the parent of the current exploring
                        break;
                    }
                }
            }
            Path.Push((T)Tree[EndIndex]); // add end point.
            Stack<T> path = new Stack<T>();

            foreach (T node in Path) {
                path.Push(node);
            } // next we reverse the stack so that it is in the correct order.

            return path; // return path
        }

        private void EscapePoint(object Data)
        {
            object[] DataArray = (object[])Data; // Convert data to data array
            T Position = (T)DataArray[0]; // From node
            Vector2 Direction = (Vector2)DataArray[1]; // Direction to look in
            float Distance = (float)DataArray[2]; // To node
            System.Action<Stack<T>> OnCalculated = (System.Action<Stack<T>>)DataArray[3]; // Convert calculated callback
            System.Action<string> OnError = (System.Action<string>)DataArray[4]; // Convert error callback
            System.Action<T> ReturnPoint = (System.Action<T>)DataArray[5];
			int CurrentRegion = (int)DataArray [6];
			int? LastVisitedNode = null;
			if (DataArray.Length >= 8) {LastVisitedNode = (int?)DataArray [7];}

            int start = Tree.NodeIndex(Position);
			if (LastVisitedNode != null && Tree.ValidAndEnabled ((int)LastVisitedNode)) {
				start = (int)LastVisitedNode;
			} else {
				start = Tree.GetClosedAvailable(start, (object)Tree[start],CurrentRegion);
			}

            // Initialize base data
            List<TrackerSearchNode> Closed = new List<TrackerSearchNode>(); // the closed array
            List<TrackerSearchNode> Open = new List<TrackerSearchNode>(); // the open array
            TrackerSearchNode InitialNode = new TrackerSearchNode(start); // the initial node to start with
            Vector2 StartVect = (Vector2)Tree[start];
            int Goal = 0; // the holder of the int to pass when done
            int TickCount = 0; // tick counter
            int[] NeighborsCache = new int[Tree.NeighborsCount];
			int MaxIt = MaxIterations;

			// Highest
			float HighestCost = 0;
			int HighestCostIndex = start;

            // Edit base data
            TrackerSearchNode CurrentNode = InitialNode;  // creates and assigns the currentnode variable as the initial node.
            Open.Add(CurrentNode);

            // start loop
            while (CurrentNode.Cost < Distance && !CancelationPending && Open.Count > 0)
            { // tests to see if search to target is in the closed list.

                if (!Tree.ValidAndEnabled(CurrentNode.Index))
                {
                    OnError.Invoke("Obstacle interupted search");
                    return;
                }

                if (TickCount > MaxIt) { break; } // check if the current iteration doesnt exceed the max iteration count

                if (CurrentNode.Cost >= Distance)
                { // checks if the current checking node is far enough away.
                    break; // stop this void
                }

                Vector2 CurrentVect = (Vector2)Tree[CurrentNode.Index];

                Tree.Neighbors(ref NeighborsCache, CurrentNode.Index);
                for (int i = 0; i < NeighborsCache.Length; i++)
                {

                    if (!Tree.ValidAndEnabled(CurrentNode.Index))
                    {
                        OnError.Invoke("Obstacle interupted search");
                        return;
                    }

                    int Neighbor = NeighborsCache[i];
                    if (Neighbor < 0) { continue; } // continue since its not a valid neighbor

                    if (CancelationPending) { return; }
                    if (Neighbor < Tree.NodeCount - 1 && Neighbor > 0)  // checks to make sure the neighbor is within range.
                    {
                        TrackerSearchNode ClosedNode = null; // get neighbor out of closed array
                        TrackerSearchNode OpenNode = null; // get neighbor out of open array
                        foreach (TrackerSearchNode node in Closed)
                        {
                            if (node.Index == Neighbor)
                            {
                                ClosedNode = node;
                                break;
                            }
                        }

                        foreach (TrackerSearchNode node in Open)
                        {
                            if (node.Index == Neighbor)
                            {
                                OpenNode = node;
                                break;
                            }
                        }

                        Vector2 NH = (Vector2)Tree[Neighbor]; // converts out neighbor to a vector 2 for math reasons.

                        float difX = Mathf.Abs((NH.x - CurrentVect.x) - (Direction.x)); // x differences
                        float difY = Mathf.Abs((NH.y - CurrentVect.y) - (Direction.y)); // y differences
                        float dif = difX + difY;

                        if (!Tree.ValidAndEnabled(CurrentNode.Index))
                        {
                            OnError.Invoke("Obstacle interupted search");
                            return;
                        }
							
						float G = (int)(Tree.Cost (Neighbor,start) / 10f);  // cost is distance from neighbor to start
                        float H = dif;  // sees how far off the vector directions plot line the current neighbor is.

						if (G > HighestCost) {
							HighestCost = G;
							HighestCostIndex = Neighbor;
						} // set new highest


                        if (ClosedNode != null) { continue; } //checks to see if node exists in closed list and skips it if it is.
                        else
                        { // neighbor is not in closed array

                            if (OpenNode != null)  //if node is an open node.
                            {
                                if (OpenNode.Heuristic > H)  //checks to see if the this would make for a better parent or not.
                                {
                                    OpenNode.Cost = G; // set node cost
                                    OpenNode.Heuristic = H;
                                }
                                else { continue; } // else skip
                            }
                            else
                            { // if node is a new node

                                TrackerSearchNode Node = new TrackerSearchNode(Neighbor); // create new node
                                
                                Node.Cost = G; // set node cost
                                Node.Heuristic = H;

                                Open.Add(Node);  // add the index to a list for iteration later
                            }
                        }
                    }
                }

                if (CancelationPending) { return; }
                TrackerSearchNode lowest = Open[0];  //assigns a node at semi-random to be used for comparinsons to find the lowset cost node;

                for (int i = 0; i < Open.Count - 1; i++) // iterate through the list of open indexies.
                {
                    if (lowest.Heuristic > Open[i].Heuristic) { lowest = Open[i]; } // checks this iterations open node against the assigned currentnode to see who costs less, the lowest cost one becomes the new current node
                }
                Open.Remove(CurrentNode);  // removes the current node from the open indexie list as it is the lowes cost node and will soon be added to the closed list.
                CurrentNode = lowest;  //assigns the new currentnode.
                Closed.Add(CurrentNode); // add current checking to the closed array

                if (!Tree.ValidAndEnabled(CurrentNode.Index))
                {
                    OnError.Invoke("Obstacle interupted search");
                    return;
                }

                TickCount++; // up tickcount
            }
            if (CancelationPending) { return; }
			Goal = HighestCostIndex;

            if (ReturnPoint != null)
            {
                ReturnPoint.Invoke((T)Tree[Goal]);
            }
            else
            {
                FindPathCore(new object[]{
                        Position,
                        (T)Tree[Goal],
                        OnCalculated,
                        OnError,
						null,
						CurrentRegion,
						LastVisitedNode
                    });
            }
        }
        #endregion
    }
}
