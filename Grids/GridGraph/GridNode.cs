using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tracker.Grids{
	[System.Serializable]
	public class GridNode {
		#region public variables
		public Vector2 Position;
		public int Region;
		#endregion

		#region Private variables
		private float _CeilDistance;
		private bool _Enabled = true;
		private float _Slope;
		private float _YPosition;
		private float _CeilTerrainDistance;
		#endregion

		#region Constructor
		public GridNode(Vector2 Position){
			this.Position = Position;
		}
		#endregion

		#region Public inputs
		public void UpdateNode(Terrain SearchTerrain,Vector3 TerrainPosition,Vector3 TerrainSize,float NodeSize,float ObstaclePadding,float MaxSlope,Vector2 CastExtends,LayerMask TerrainLayer, LayerMask ObstacleLayer,System.Func<GameObject,bool> CustomFilter = null){
			UpdateTerrainValues(SearchTerrain,TerrainPosition,TerrainSize,MaxSlope,CastExtends);
			_CeilTerrainDistance = 0f; // calculate terrain ceil distance
			Vector3 NodePosition = new Vector3 (Position.x, _YPosition, Position.y);

			if (_Enabled) { // only check if is still enabled
				_Enabled = !SphereOverlap (ObstacleLayer, Mathf.Max (NodeSize / 2f, ObstaclePadding), NodePosition, CustomFilter); // check for overlapping objects
			}

			if (_Enabled) { // only check if is still enabled
				// Thick Raycast for obstacles
				ThickRaycastFor (ObstacleLayer, NodeSize / 2f, NodePosition, new Vector3(0,1,0), TerrainSize.y, (bool HitSomething, RaycastHit Hit) => {
					if (HitSomething && CustomFilter (Hit.collider.gameObject)) { // filter gameobject
						HitSomething = false;
					} // Remove hit cause it isnt allowed by the custom filter 
			
					_CeilDistance = (HitSomething) ? Hit.distance : _CeilTerrainDistance; // set the distance from the ground to the ceiling or the above obstacle
				});
			}
		}
		public void UpdateTerrainValues(Terrain SearchTerrain,Vector3 TerrainPosition,Vector3 TerrainSize,float MaxSlope,Vector2 CastExtends){
			_Enabled = true; // set default state
			Vector3 CeilPosition = new Vector3 (Position.x, CastExtends.y, Position.y);
			float MaxDistance = CastExtends.x - CastExtends.y;
			if (MaxDistance < 0) {
				MaxDistance = -MaxDistance;
			}

			// calculate terrain values
			TerrainData ActiveTerrainData = SearchTerrain.terrainData;

			Vector3 NormalizedPosition = CeilPosition - TerrainPosition; // calculate normalized position
			NormalizedPosition = new Vector3 (
				NormalizedPosition.x / TerrainSize.x,
				0f,
				NormalizedPosition.z / TerrainSize.z
			);

			// calculate values
			if (NormalizedPosition.x < 0 || NormalizedPosition.x > 1 || NormalizedPosition.z < 0 || NormalizedPosition.z > 1) { // is outside of the active terrain
				_Slope = 0f;
				_YPosition = 0f;
			} else { // is inside of the current terrain
				_Slope = ActiveTerrainData.GetSteepness (NormalizedPosition.x, NormalizedPosition.z); // calculate steepness
				_YPosition = SearchTerrain.SampleHeight (CeilPosition) + TerrainPosition.y; // calculate Y pos
			}

			// Final Results
			if (_Slope >= MaxSlope) { // check if results make node unwalkable
				_Enabled = false;
			} else { // Node should be enabled, no checks make it unwalkable
				_Enabled = true;
			}
		}
		public Vector3 GetPosition(Vector2 Position){
			return new Vector3 (Position.x,_YPosition,Position.y);
		}
		#endregion

		#region Private voids
		private void RaycastFor(LayerMask Layer,Vector3 From, Vector3 Direction, float Distance,System.Action<bool,RaycastHit,float> Results){
			RaycastHit Hit = new RaycastHit ();
			if (Physics.Raycast (From, Direction, out Hit,Distance, Layer)) {
				Results (true,Hit,CalculateAngle(Vector3.up,Hit.normal)); // return cast results
			} else {
				Results.Invoke (false,Hit,0); // nothing found
			}
		}
		private void ThickRaycastFor(LayerMask Layer, float Thickness, Vector3 From, Vector3 Direction, float Distance,System.Action<bool,RaycastHit> Results){
			RaycastHit Hit = new RaycastHit ();
			if (Physics.SphereCast (From,Thickness, Direction, out Hit,Distance, Layer)) {
				Results (true,Hit); // return cast results
			} else {
				Results.Invoke (false,Hit); // nothing found
			}
		}
		private bool SphereOverlap(LayerMask Layer, float Radius,Vector3 Position,System.Func<GameObject,bool> Filter){
			var Colliders = Physics.OverlapSphere (Position, Radius, Layer);
			for (int i = 0; i < Colliders.Length; i++) {
				if (!Filter.Invoke (Colliders [i].gameObject)) {return true;}
			}
			return false;
		}
		private float CalculateAngle(Vector3 A, Vector3 B){
			return Mathf.Acos ((A.x * B.x) + (A.y * B.y) + (A.z * B.z)) * Mathf.Rad2Deg;
		}
		#endregion

		#region Get / Set
		/// <summary>
		/// The distance from the ceiling to the terrain
		/// </summary>
		/// <value>The ceil terrain distance.</value>
		public float CeilTerrainDistance{
			get{
				return this._CeilTerrainDistance;
			}
		}
		/// <summary>
		/// Distance from the terrain to the ceiling or a obstacle object
		/// </summary>
		/// <value>The ceil distance.</value>
		public float CeilDistance{
			get{
				return this._CeilDistance;
			}
		}
		public bool Enabled{
			get{
				return this._Enabled;
			}
		}
		public float Slope{
			get{
				return this._Slope;
			}
		}
		public float YPosition{
			get{
				return this._YPosition;
			}
		}
		public float CeilPercentage{
			get{
				return Mathf.Clamp (CeilDistance / CeilTerrainDistance,0,1);
			}
		}
		#endregion
	}
}
