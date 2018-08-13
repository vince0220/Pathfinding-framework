using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Tracker.Search;
using System.Linq;

namespace Tracker{
	public class TrackerManager : MonoBehaviour {
		#region Inspector variables
		[Header("Initialization Settings")]
		public bool InitializeOnStart = true;
		public int MaxThreads = 5;

		[Header("Node Tree Settings")]
		public List<ScriptableObject> TreeData = new List<ScriptableObject>();
		#endregion

		#region Private variables
		private INodeTree[] _NodeTrees;
		#endregion

		#region Input voids
		public void DebugNodeTrees(){
			for (int i = 0; i < NodeTrees.Length; i++) {NodeTrees [i].Debug ();} // debug node trees
		}
		#endregion

		#region Statics
		public static TrackerManager I;
		#endregion

		#region Unity Voids
		private void Awake(){
			I = this;
			ThreadPool.SetMaxThreads (MaxThreads, MaxThreads);
		}
		private void Start(){
			if (InitializeOnStart) {InitializeNodeTrees ();} // initialize node trees if should be done on start
		}
		private void OnDrawGizmos(){
			DebugNodeTrees (); // debug the node trees
		}
		private void Update(){
			Dispatcher.Instance.InvokePending();
		}
		#endregion

		#region Public voids
		// Treevoids
		/// <summary>
		/// Gets a tree at index with a certain type. If tree is not convertable to type null is returned
		/// </summary>
		/// <returns>The tree as.</returns>
		/// <param name="Index">Index.</param>
		/// <typeparam name="T">The 1st type parameter.</typeparam>
		public T GetTreeAs<T>(int Index) where T : class{
			if (HasTreeAtIndex (Index)) {
				INodeTree Tree = NodeTrees [Index];
				if (CheckTreeDirectType<T> (Tree)) {
					return (T)(object)Tree;
				}
			}
			return null;
		}

		/// <summary>
		/// Updates a node tree based on index and given boundaries
		/// </summary>
		/// <returns><c>true</c>, if node tree was updated, <c>false</c> otherwise.</returns>
		/// <param name="Index">Index.</param>
		/// <param name="UpdateBounds">Update bounds.</param>
		public bool UpdateNodeTree(int Index,Bounds UpdateBounds){
			if (Index < TreeData.Count) {
				NodeTrees [Index].UpdateTree (UpdateBounds); // update tree
				return true;
			}
			return false; // couldent update
		}

		/// <summary>
		/// Initializes the node trees
		/// </summary>
		public void InitializeNodeTrees(){
			for (int i = 0; i < NodeTrees.Length; i++) {NodeTrees [i].Initialize ();} // Initialize node trees
		}

		/// <summary>
		/// Determines whether this instance has tree at index the specified Index.
		/// </summary>
		/// <returns><c>true</c> if this instance has tree at index the specified Index; otherwise, <c>false</c>.</returns>
		/// <param name="Index">Index.</param>
		public bool HasTreeAtIndex(int Index){
			return (Index < TreeData.Count && TreeData[Index] != null);
		}

		// search voids
		/// <summary>
		/// Searches a path from A to B with a given type
		/// </summary>
		/// <param name="TreeIndex">Index of the node tree to use for the search</param>
		/// <param name="From">From</param>
		/// <param name="To">To</param>
		/// <param name="OnCalculated">Callback that is invoked when the path is found</param>
		/// <param name="OnError">Callback that is invoked when an error has occured during the search</param>
		/// <param name="Modifiers">Modifiers that should be used on this path</param>
		/// <typeparam name="T">The type of the path you want to search</typeparam>
		public TrackerSearch<T> SearchPath<T>(int TreeIndex, T From, T To, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError,IModifier<T>[] Modifiers,int CurrentRegion = -1,int? LastVisitedNode = null){
            if (HasTreeAtIndex (TreeIndex)) { // check if tree exists
				INodeTree Tree = NodeTrees [TreeIndex];
				if (CheckTreeType<T>(Tree)) {
					TrackerSearch<T> SearchInstance = new TrackerSearch<T> (Tree); // initialize new 
					SearchInstance.FindPathAsync(From, To, (Stack<T> CalculatedPath)=>{
						if(Modifiers != null){ // check if there are any modifiers
							for(int i = 0; i < Modifiers.Length; i++){
                                if (Modifiers[i] != null){ // null check modifier
									CalculatedPath = Modifiers[i].ModifyPath(CalculatedPath);}
                            } // modify path
						}

						Dispatcher.Instance.Invoke(()=>{ // invoke to main thread
							if(!SearchInstance.CancelationPending){
								OnCalculated.Invoke(CalculatedPath); // invoke calculated path
							}
						});
					}, (string Error)=>{
						Dispatcher.Instance.Invoke(()=>{ // invoke to main thread
							OnError.Invoke (Error);
						});
					},CurrentRegion,LastVisitedNode);
					return SearchInstance;
				} else {
					Dispatcher.Instance.Invoke(()=>{ // invoke to main thread
						OnError.Invoke ("Tree at index: "+TreeIndex+" could NOT be converted to an node tree of type ("+typeof(T)+")");
					});
					return null; // end void
				}
			} else {
				Dispatcher.Instance.Invoke(()=>{ // invoke to main thread
					OnError.Invoke ("There is no tree at index: "+TreeIndex);
				});
				return null; // end void
			}
			return null;
		}

        // search voids
        /// <summary>
        /// Searches a path from A to a point along the given Stack of Vectors
        /// </summary>
        /// <param name="TreeIndex">Index of the node tree to use for the search</param>
        /// <param name="From">From</param>
        /// <param name="Direction"> The direction in wich you wish to find a route</param>
        /// <param name="Distance"> the Distance from the starting point you wish to find a path to</param>
        /// <param name="OnCalculated">Callback that is invoked when the path is found</param>
        /// <param name="OnError">Callback that is invoked when an error has occured during the search</param>
        /// <param name="Modifiers">Modifiers that should be used on this path</param>
        /// <typeparam name="T">The type of the path you want to search</typeparam>
		public TrackerSearch<T> SearchByDirection<T>(int TreeIndex, T From, Vector2 Direction, float Distance, System.Action<Stack<T>> OnCalculated, System.Action<string> OnError, IModifier<T>[] Modifiers, System.Action<T> Point,int CurrentRegion = -1,int? LastVisitedNode = null)
        {
            if (HasTreeAtIndex(TreeIndex))
            { // check if tree exists
                INodeTree Tree = NodeTrees[TreeIndex];
                if (CheckTreeType<T>(Tree))
                {
                    TrackerSearch<T> Search = new TrackerSearch<T>(Tree); // initialize new 
                    Search.FindDirectionPathAsync(From, Direction, Distance, (Stack<T> CalculatedPath) => {
                        if (Modifiers != null)
                        { // check if there are any modifiers
                            for (int i = 0; i < Modifiers.Length; i++)
                            {
                                if (Modifiers[i] != null)
                                { // null check modifier
									CalculatedPath = Modifiers[i].ModifyPath(CalculatedPath);
                                }
                            } // modify path
                        }

                        Dispatcher.Instance.Invoke(() => { // invoke to main thread
                            OnCalculated.Invoke(CalculatedPath); // invoke calculated path
                        });
					}, (string Error)=>{
						Dispatcher.Instance.Invoke(() => { // invoke to main thread
							OnError.Invoke(Error);
						});
					}, Point,CurrentRegion,LastVisitedNode);
					return Search;
                }
                else
                {
                    Dispatcher.Instance.Invoke(() => { // invoke to main thread
                        OnError.Invoke("Tree at index: " + TreeIndex + " could NOT be converted to an node tree of type (" + typeof(T) + ")");
                    });
					return null; // end void
                }
            }
            else
            {
                Dispatcher.Instance.Invoke(() => { // invoke to main thread
                    OnError.Invoke("There is no tree at index: " + TreeIndex);
                });
				return null; // end void
            }
        }
        #endregion

        #region Private voids
        // Tree voids
        private INodeTree[] DataToNodeTrees(List<ScriptableObject> Data){
			List<INodeTree> TempTrees = new List<INodeTree>();
			for (int i = 0; i < Data.Count; i++) {
				if(Data[i] != null){ // check for null data
					var Converted = (INodeTree)Data [i]; // try converting
					if (Converted != null) {TempTrees.Add (Converted); // add converted
					} else {Debug.LogError ("Trying to convert a NONE INodeTree to a INodeTree. Remove none INodeTree's from data.");} // log error
				}
			}
			return TempTrees.ToArray ();
		}
		private bool CheckTreeType<T>(int Index){
			if (HasTreeAtIndex (Index)) {
				return CheckTreeType<T>(NodeTrees[Index]);
			}
			return false;
		}
		private bool CheckTreeDirectType<T> (INodeTree Tree){
			return typeof(T).IsAssignableFrom (Tree.GetType());
		}
		private bool CheckTreeType<T>(INodeTree Tree){
			return typeof(ANodeTree<T>).IsAssignableFrom (Tree.GetType());
		}
        #endregion

        #region Get / Set
        private INodeTree[] NodeTrees{
			get{
				if (Application.isPlaying) { // is playing only convert once
					if (_NodeTrees == null) {_NodeTrees = DataToNodeTrees (TreeData);} // convert if isnt set
					return _NodeTrees; // return node trees
				} else {
					return DataToNodeTrees (TreeData); // is in editor always live convert
				}
			}
		}
		#endregion
	}
}
