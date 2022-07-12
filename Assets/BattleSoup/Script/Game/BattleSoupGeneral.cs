using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AngeliaFramework;
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }


namespace BattleSoup {




	public enum ActionType {

		None,

		// Position Buffer
		Pick,

		// Tile
		Attack,
		Reveal,
		Unreveal,
		Sonar,
		Expand,
		Shrink,

		// Ship
		SunkShip,
		RevealShip,
		ExposeShip,

		// Cooldown
		AddCooldown,
		ReduceCooldown,
		AddMaxCooldown,
		ReduceMaxCooldown,

		// Other
		PerformLastUsedAbility,

	}



	public enum EntranceType {

		OnAbilityUsed,
		OnAbilityUsedOvercharged,

		OnOpponentGetAttack,
		OnSelfGetAttack,

		OnOpponentShipGetHit,
		OnSelfShipGetHit,
		OnCurrentShipGetHit,

		OnOpponentShipGetRevealed,
		OnSelfShipGetRevealed,
		OnCurrentShipGetRevealed,

		OnOpponentShipGetSunk,
		OnSelfShipGetSunk,
		OnCurrentShipGetSunk,

	}



	[System.Flags]
	public enum ActionKeyword : long {

		None = 0,

		// Tile
		NormalWater = 1L << 0,
		RevealedWater = 1L << 1,

		Stone = 1L << 2,
		NoStone = 1L << 3,

		Ship = 1L << 4,
		NoShip = 1L << 5,

		Hittable = 1L << 6,

		ExposedShip = 1L << 7,
		UnexposedShip = 1L << 8,

		RevealedShip = 1L << 9,
		UnrevealedShip = 1L << 10,

		HitShip = 1L << 11,
		SunkShip = 1L << 12,

		// Target
		This = 1L << 13,
		Self = 1L << 14,
		Opponent = 1L << 15,

		// Break
		BreakIfMiss = 1L << 16,
		BreakIfHit = 1L << 17,
		BreakIfReveal = 1L << 18,
		BreakIfSunk = 1L << 19,

		// Trigger
		TriggerIfMiss = 1L << 20,
		TriggerIfHit = 1L << 21,
		TriggerIfReveal = 1L << 22,
		TriggerIfSunk = 1L << 23,

	}



	[System.Flags]
	public enum ActionResult : long {

		None = 0,

		Hit = 1L << 0,
		Sunk = 1L << 1,
		RevealWater = 1L << 2,
		RevealShip = 1L << 3,
		Sonar = 1L << 4,
		ExposeShip = 1L << 5,

	}



	[System.Serializable]
	public struct Int2 {
		public int x;
		public int y;
		public Int2 (int x, int y) {
			this.x = x;
			this.y = y;
		}
	}


	public static class SoupConst {

		public const int ISO_WIDTH = 32 * 7;
		public const int ISO_HEIGHT = 16 * 7;
		public const int ISO_SIZE = 64 * 7;

		public static BattleSoup.Turn Opponent (this BattleSoup.Turn turn) => 1 - turn;

		public static bool Check (this ActionKeyword keyword, Cell cell) {

			if (keyword == ActionKeyword.None) return true;

			// One-Vote-Off
			if (keyword.HasFlag(ActionKeyword.Stone)) {
				if (!cell.HasStone) return false;
			}
			if (keyword.HasFlag(ActionKeyword.NoStone)) {
				if (cell.HasStone) return false;
			}

			if (keyword.HasFlag(ActionKeyword.Ship)) {
				if (cell.ShipIndex < 0) return false;
			}
			if (keyword.HasFlag(ActionKeyword.NoShip)) {
				if (cell.ShipIndex >= 0) return false;
			}

			if (keyword.HasFlag(ActionKeyword.ExposedShip)) {
				if (!cell.HasExposedShip) return false;
			}
			if (keyword.HasFlag(ActionKeyword.UnexposedShip)) {
				if (cell.ShipIndex < 0 || cell.HasExposedShip) return false;
			}

			// One-Vote-In
			if (keyword.HasFlag(ActionKeyword.NormalWater)) {
				if (cell.State == CellState.Normal) return true;
			}
			if (keyword.HasFlag(ActionKeyword.RevealedWater)) {
				if (cell.State == CellState.Revealed && cell.ShipIndex < 0) return true;
			}
			if (keyword.HasFlag(ActionKeyword.RevealedShip)) {
				if (cell.State == CellState.Revealed && cell.ShipIndex >= 0) return true;
			}
			if (keyword.HasFlag(ActionKeyword.UnrevealedShip)) {
				if (cell.State == CellState.Normal && cell.ShipIndex >= 0) return true;
			}
			if (keyword.HasFlag(ActionKeyword.HitShip)) {
				if (cell.State == CellState.Hit && cell.ShipIndex >= 0) return true;
			}
			if (keyword.HasFlag(ActionKeyword.SunkShip)) {
				if (cell.State == CellState.Sunk && cell.ShipIndex >= 0) return true;
			}
			if (keyword.HasFlag(ActionKeyword.Hittable)) {
				if (!cell.HasStone && cell.State == CellState.Normal) return true;
				if (!cell.HasStone && cell.State == CellState.Revealed) return true;
			}

			return false;
		}

	}


	public static class SoupUtil {


		public static Vector2Int GetPickedPosition (Vector2Int pickingPos, Direction4 pickingDir, int localX, int localY) =>
			pickingPos + pickingDir switch {
				Direction4.Down => new(-localX, -localY),
				Direction4.Left => new(-localY, localX),
				Direction4.Right => new(localY, -localX),
				_ or Direction4.Up => new(localX, localY),
			};


	}



	[System.Serializable]
	public class Ship : ISerializationCallbackReceiver {


		// Ser-Api
		public string GlobalName = "";
		public string DisplayName = "";
		public string Discription = "";
		public int DefaultCooldown = 1;
		public int MaxCooldown = 1;
		public string Body = "1";

		// Data-Api
		[System.NonSerialized] public int GlobalCode = 0;
		[System.NonSerialized] public int FieldX = 0;
		[System.NonSerialized] public int FieldY = 0;
		[System.NonSerialized] public bool Flip = false;
		[System.NonSerialized] public int CurrentCooldown = 1;
		[System.NonSerialized] public bool Visible = false;
		[System.NonSerialized] public bool Valid = true;
		[System.NonSerialized] public bool Alive = true;
		[System.NonSerialized] public Vector2Int[] BodyNodes = null;
		[System.NonSerialized] public Sprite Icon = null;



		// MSG
		public void OnBeforeSerialize () { }


		public void OnAfterDeserialize () {
			GlobalCode = GlobalName.AngeHash();
			FieldX = 0;
			FieldY = 0;
			Flip = false;
			BodyNodes = GetBodyNode(Body);
		}


		// API
		public Vector2Int GetFieldNodePosition (int nodeIndex) {
			var node = BodyNodes[nodeIndex];
			return new(
				FieldX + (Flip ? node.y : node.x),
				FieldY + (Flip ? node.x : node.y)
			); ;
		}


		public Ship CreateDataCopy () {
			var newShip = JsonUtility.FromJson<Ship>(JsonUtility.ToJson(this, false));
			newShip.Icon = Icon;
			return newShip;
		}


		// LGC
		private static Vector2Int[] GetBodyNode (string body) {
			var result = new List<Vector2Int>();
			int x = 0;
			int y = 0;
			for (int i = 0; i < body.Length; i++) {
				char c = body[i];
				switch (c) {
					case '0':
						x++;
						break;
					case '1':
						result.Add(new(x, y));
						x++;
						break;
					case ',':
						y++;
						x = 0;
						break;
				}
			}
			return result.ToArray();
		}


	}



	[System.Serializable]
	public class Map {

		public int this[int x, int y] => Content[y * Size + x];
		public int Size = 8;
		public int[] Content = new int[64]; // 0:Water 1:Stone

	}



	public enum CellState {
		Normal = 0,
		Revealed = 1,
		Hit = 2,
		Sunk = 3,
	}



	public class Cell {

		public int ShipIndex => ShipIndexs.Count > 0 ? ShipIndexs[0] : -1;

		public CellState State = CellState.Normal;
		public bool HasStone = false;
		public bool HasExposedShip = false;
		public int Sonar = 0;
		public readonly List<int> ShipIndexs = new(16);
		public readonly List<int> ShipRenderIDs = new(16);
		public readonly List<int> ShipRenderIDsAdd = new(16);
		public readonly List<int> ShipRenderIDsSunk = new(16);

		public void AddShip (int shipIndex, Ship ship, int bodyX, int bodyY) {
			ShipIndexs.Add(shipIndex);
			ShipRenderIDs.Add($"{ship.GlobalName} {bodyX}.{bodyY}".AngeHash());
			ShipRenderIDsAdd.Add($"{ship.GlobalName}_Add {bodyX}.{bodyY}".AngeHash());
			ShipRenderIDsSunk.Add($"{ship.GlobalName}_Sunk {bodyX}.{bodyY}".AngeHash());
		}

		public void ClearShip () {
			ShipIndexs.Clear();
			ShipRenderIDs.Clear();
			ShipRenderIDsAdd.Clear();
			ShipRenderIDsSunk.Clear();
		}


	}


}