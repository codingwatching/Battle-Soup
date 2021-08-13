﻿using System.Collections;
using System.Collections.Generic;



namespace BattleSoupAI {



	public class BattleInfo {

		// Api
		public int AliveShipCount {
			get {
				int result = 0;
				foreach (var alive in ShipsAlive) {
					if (alive) { result++; }
				}
				return result;
			}
		}

		// Api-Ser
		public int MapSize;
		public Tile[,] Tiles;
		public Ship[] Ships;
		public int[] Cooldowns;
		public bool[] ShipsAlive;
		public ShipPosition?[] KnownPositions;
		public List<SonarPosition> Sonars;

	}



	public struct AnalyseResult {
		public static readonly AnalyseResult None = new AnalyseResult() {
			TargetPosition = default,
			AbilityDirection = default,
			AbilityIndex = -1,
			ErrorMessage = "",
		};
		public static readonly AnalyseResult NotPerformed = new AnalyseResult() {
			TargetPosition = default,
			AbilityDirection = default,
			AbilityIndex = -1,
			ErrorMessage = "Task not performed",
		};
		public bool Success => string.IsNullOrEmpty(ErrorMessage);
		public string ErrorMessage;
		public Int2 TargetPosition;
		public int AbilityIndex;
		public AbilityDirection AbilityDirection;
		public override string ToString () => Success ? $"pos.x:{TargetPosition.x}, pos.y:{TargetPosition.y} abi:{AbilityIndex}, dir:{AbilityDirection}" : $"ERROR: {ErrorMessage}";
	}



	public abstract class SoupStrategy {



		#region --- SUB ---


		public delegate void MessageHandler (string msg);


		#endregion




		#region --- VAR ---


		// Api
		public static MessageHandler LogMessage { get; set; } = null;
		public string FinalDisplayName => !string.IsNullOrEmpty(DisplayName) ? DisplayName : GetType().Name;
		public virtual string DisplayName { get; } = "";
		public virtual string Description { get; } = "";
		public virtual string[] FleetID { get; } = { "Sailboat", "SeaMonster", "Longboat", "MiniSub", };

		// Data
		private int[,] RIP_ValuesCache = null;
		private int[,,] RIP_ValuesCacheAlt = null;
		protected static System.Random Random = new System.Random();


		#endregion




		#region --- API ---


		public abstract AnalyseResult Analyse (BattleInfo ownInfo, BattleInfo opponentInfo, int usingAbilityIndex);


		public virtual void OnBattleStart (BattleInfo ownInfo, BattleInfo opponentInfo) {
			Random = new System.Random((int)System.DateTime.Now.Ticks);
		}


		public virtual void OnBattleEnd (BattleInfo ownInfo, BattleInfo opponentInfo) { }


		public virtual bool PositionShips (int mapSize, Ship[] ships, Int2[] stones, out ShipPosition[] result) => PositionShips_Random(mapSize, ships, stones, out result);


		public static bool PositionShips_Random (int mapSize, Ship[] ships, Int2[] stones, out ShipPosition[] result) {

			result = null;
			if (ships == null || ships.Length == 0) { return false; }
			bool success = true;

			// Get Hash
			var hash = new HashSet<Int2>();
			if (stones != null) {
				foreach (var stone in stones) {
					if (!hash.Contains(stone)) {
						hash.Add(stone);
					}
				}
			}

			// Get Result
			result = new ShipPosition[ships.Length];
			for (int index = 0; index < ships.Length; index++) {
				var ship = ships[index];
				var sPos = new ShipPosition();
				var basicPivot = new Int2(Random.Next(0, mapSize), Random.Next(0, mapSize));
				bool shipSuccess = false;
				// Try Fix Overlap
				for (int j = 0; j < mapSize; j++) {
					for (int i = 0; i < mapSize; i++) {
						sPos.Pivot = new Int2(
							(basicPivot.x + i) % mapSize,
							(basicPivot.y + j) % mapSize
						);
						sPos.Flip = false;
						if (PositionAvailable(ship, sPos)) {
							AddShipIntoHash(ship, sPos);
							shipSuccess = true;
							j = mapSize;
							break;
						}
						sPos.Flip = true;
						if (PositionAvailable(ship, sPos)) {
							AddShipIntoHash(ship, sPos);
							shipSuccess = true;
							j = mapSize;
							break;
						}
					}
				}
				if (!shipSuccess) { success = false; }
				result[index] = sPos;
			}
			if (!success) {
				result = null;
			}
			return success;
			// Func
			bool PositionAvailable (Ship _ship, ShipPosition _pos) {
				// Border Check
				var (min, max) = _ship.GetBounds(_pos.Flip);
				if (_pos.Pivot.x < -min.x || _pos.Pivot.x > mapSize - max.x - 1 ||
					_pos.Pivot.y < -min.y || _pos.Pivot.y > mapSize - max.y - 1
				) {
					return false;
				}
				// Overlap Check
				foreach (var v in _ship.Body) {
					if (hash.Contains(new Int2(
						_pos.Pivot.x + (_pos.Flip ? v.y : v.x),
						_pos.Pivot.y + (_pos.Flip ? v.x : v.y)
					))) {
						return false;
					}
				}
				return true;
			}
			void AddShipIntoHash (Ship _ship, ShipPosition _pos) {
				foreach (var v in _ship.Body) {
					var shipPosition = new Int2(
						_pos.Pivot.x + (_pos.Flip ? v.y : v.x),
						_pos.Pivot.y + (_pos.Flip ? v.x : v.y)
					);
					if (!hash.Contains(shipPosition)) {
						hash.Add(shipPosition);
					}
				}
			}
		}


		public bool CalculatePotentialPositions (BattleInfo info, Tile mustContains, Tile onlyContains, ref List<ShipPosition>[] positions) {

			if (info == null) { return false; }

			// Check
			if (positions == null || positions.Length != info.Ships.Length) {
				positions = new List<ShipPosition>[info.Ships.Length];
			}

			int size = info.Tiles.GetLength(1);
			int shipCount = info.Ships.Length;

			// Add Potential Ships
			for (int shipIndex = 0; shipIndex < shipCount; shipIndex++) {
				if (positions[shipIndex] == null) {
					positions[shipIndex] = new List<ShipPosition>();
				}
				var posList = positions[shipIndex];
				posList.Clear();
				if (!info.ShipsAlive[shipIndex]) { continue; }
				if (info.KnownPositions[shipIndex].HasValue) { continue; }
				for (int y = 0; y < size; y++) {
					for (int x = 0; x < size; x++) {
						if (!info.Ships[shipIndex].Symmetry && CheckAvailable(shipIndex, x, y, true)) {
							posList.Add(new ShipPosition(x, y, true));
						}
						if (CheckAvailable(shipIndex, x, y, false)) {
							posList.Add(new ShipPosition(x, y, false));
						}
					}
				}
			}

			return true;
			// Func
			bool CheckAvailable (int _shipIndex, int _x, int _y, bool _flip) {
				var body = info.Ships[_shipIndex].Body;
				int _hitCount = 0;
				bool containsTarget = false;
				foreach (var v in body) {
					int _i = _x + (_flip ? v.y : v.x);
					int _j = _y + (_flip ? v.x : v.y);
					if (_i < 0 || _j < 0 || _i >= size || _j >= size) { return false; }
					var tile = info.Tiles[_i, _j];
					if (!onlyContains.HasFlag(tile)) { return false; }
					if (mustContains.HasFlag(tile)) {
						containsTarget = true;
					}
					if (tile == Tile.HittedShip) { _hitCount++; }
				}
				return containsTarget && _hitCount < body.Length;
			}
		}


		public void RemoveImpossiblePositions (BattleInfo info, ref List<ShipPosition>[] hiddenPos, ref List<ShipPosition>[] exposedPos) {

			if (info == null) { return; }
			if (hiddenPos == null || hiddenPos.Length != info.Ships.Length) { return; }
			if (exposedPos == null || exposedPos.Length != info.Ships.Length) { return; }

			// Init RIP Value Cache
			if (RIP_ValuesCache == null || RIP_ValuesCache.GetLength(0) != info.MapSize || RIP_ValuesCache.GetLength(1) != info.MapSize) {
				RIP_ValuesCache = new int[info.MapSize, info.MapSize];
			}
			if (RIP_ValuesCacheAlt == null || RIP_ValuesCacheAlt.GetLength(0) != info.Ships.Length || RIP_ValuesCacheAlt.GetLength(1) != info.MapSize || RIP_ValuesCacheAlt.GetLength(2) != info.MapSize) {
				RIP_ValuesCacheAlt = new int[info.Ships.Length, info.MapSize, info.MapSize];
			}
			System.Array.Clear(RIP_ValuesCache, 0, RIP_ValuesCache.Length);
			System.Array.Clear(RIP_ValuesCacheAlt, 0, RIP_ValuesCacheAlt.Length);
			for (int shipIndex = 0; shipIndex < info.Ships.Length; shipIndex++) {
				AddShipToAltValue(shipIndex, exposedPos, 1);
			}

			bool needToDoTheThing = true;
			while (needToDoTheThing) {
				needToDoTheThing = false;
				// 1, Ship have only one position so other ships can't be same place
				for (int _index = 0; _index < info.Ships.Length; _index++) {
					var listA = hiddenPos[_index];
					var listB = exposedPos[_index];
					int countAB = listA.Count + listB.Count;
					if (countAB == 1) {
						var sPos = listA.Count > 0 ? listA[0] : listB[0];
						SetValues(_index, sPos, _index + 1);
						RemoveByValues(_index, hiddenPos, exposedPos);
					}
				}
				// 2, If exposed tile can only be specific ship,
				//    that ship can not be in other place
				for (int y = 0; y < info.MapSize; y++) {
					for (int x = 0; x < info.MapSize; x++) {
						if (SumOfAltValues(x, y, out int index) == 1) {
							var ship = info.Ships[index];
							var hdList = hiddenPos[index];
							for (int i = 0; i < hdList.Count; i++) {
								var sPos = hdList[i];
								if (!ship.Contains(x, y, sPos)) {
									hdList.RemoveAt(i);
									i--;
									needToDoTheThing = true;
								}
							}
							var exList = exposedPos[index];
							for (int i = 0; i < exList.Count; i++) {
								var sPos = exList[i];
								if (!ship.Contains(x, y, sPos)) {
									AddShipPosToAltValue(index, sPos, -1);
									exList.RemoveAt(i);
									i--;
									needToDoTheThing = true;
								}
							}
						}
					}
				}
			}

			// Func
			void RemoveByValues (int _index, List<ShipPosition>[] _hiddenPos, List<ShipPosition>[] _expsedPos) {
				for (int _shipIndex = 0; _shipIndex < info.Ships.Length; _shipIndex++) {
					if (_shipIndex == _index) { continue; }
					var checkListA = _hiddenPos[_shipIndex];
					var checkListB = _expsedPos[_shipIndex];
					bool moreThanOneBefore = checkListA.Count + checkListB.Count > 1;
					for (int i = 0; i < checkListA.Count; i++) {
						if (!CheckValues(_shipIndex, checkListA[i], _shipIndex + 1)) {
							checkListA.RemoveAt(i);
							i--;
						}
					}
					for (int i = 0; i < checkListB.Count; i++) {
						if (!CheckValues(_shipIndex, checkListB[i], _shipIndex + 1)) {
							AddShipToAltValue(_shipIndex, _expsedPos, -1);
							checkListB.RemoveAt(i);
							i--;
						}
					}
					if (moreThanOneBefore && checkListA.Count + checkListB.Count == 1) {
						needToDoTheThing = true;
					}
				}
			}
			void SetValues (int _index, ShipPosition _sPos, int _value) {
				foreach (var v in info.Ships[_index].Body) {
					var _pos = _sPos.GetPosition(v);
					RIP_ValuesCache[_pos.x, _pos.y] = _value;
				}
			}
			bool CheckValues (int _index, ShipPosition _sPos, int _value) {
				var ship = info.Ships[_index];
				Int2 _pos;
				foreach (var v in ship.Body) {
					_pos = _sPos.GetPosition(v);
					int tileValue = RIP_ValuesCache[_pos.x, _pos.y];
					if (tileValue != 0 && tileValue != _value) {
						return false;
					}
				}
				return true;
			}
			int SumOfAltValues (int _x, int _y, out int _index) {
				int sum = 0;
				_index = -1;
				for (int i = 0; i < info.Ships.Length; i++) {
					bool add = RIP_ValuesCacheAlt[i, _x, _y] > 0;
					sum += add ? 1 : 0;
					if (add) {
						_index = i;
					}
				}
				return sum;
			}
			void AddShipToAltValue (int _shipIndex, List<ShipPosition>[] _exposedPos, int _add) {
				var sPosList = _exposedPos[_shipIndex];
				for (int i = 0; i < sPosList.Count; i++) {
					AddShipPosToAltValue(_shipIndex, sPosList[i], _add);
				}
			}
			void AddShipPosToAltValue (int _shipIndex, ShipPosition _sPos, int _add) {
				foreach (var v in info.Ships[_shipIndex].Body) {
					var pos = _sPos.GetPosition(v);
					var tile = info.Tiles[pos.x, pos.y];
					if (tile == Tile.RevealedShip || tile == Tile.HittedShip) {
						RIP_ValuesCacheAlt[_shipIndex, pos.x, pos.y] += _add;
					}
				}
			}
		}


		public bool CalculatePotentialValues (BattleInfo info, List<ShipPosition>[] positions, ref float[,,] values, out float minValue, out float maxValue) {

			minValue = maxValue = 0f;

			if (info == null) { return false; }

			if (
				values == null ||
				values.GetLength(0) != info.Ships.Length + 1 ||
				values.GetLength(1) != info.MapSize ||
				values.GetLength(2) != info.MapSize
			) {
				values = new float[info.Ships.Length + 1, info.MapSize, info.MapSize];
			}

			int shipCount = info.Ships.Length;

			// Init
			System.Array.Clear(values, 0, values.Length);

			// Calculate Values
			minValue = float.MaxValue;
			maxValue = float.MinValue;
			int _x, _y;
			float _newValue;
			for (int shipIndex = 0; shipIndex < shipCount; shipIndex++) {
				foreach (var pos in positions[shipIndex]) {
					(minValue, maxValue) = AddToValues(
						values, pos, shipIndex, minValue, maxValue
					);
				}
			}

			return true;
			// Func
			(float min, float max) AddToValues (float[,,] _values, ShipPosition _pos, int _shipIndex, float _minValue, float _maxValue) {
				var _ship = info.Ships[_shipIndex];
				foreach (var v in _ship.Body) {
					_x = _pos.Pivot.x + (_pos.Flip ? v.y : v.x);
					_y = _pos.Pivot.y + (_pos.Flip ? v.x : v.y);
					_newValue = System.Math.Abs(_values[_shipIndex, _x, _y]) + 1f;
					_values[_shipIndex, _x, _y] = _newValue;
					_values[shipCount, _x, _y]++;
					if (_newValue >= 0) {
						_minValue = System.Math.Min(_newValue, _minValue);
						_maxValue = System.Math.Max(_newValue, _maxValue);
					}
				}
				return (_minValue, _maxValue);
			}
		}


		public void CalculateSlimeValues (BattleInfo info, Tile filter, ref int[,] values) {
			if (values == null || values.GetLength(0) != info.MapSize || values.GetLength(1) != info.MapSize) {
				values = new int[info.MapSize, info.MapSize];
			}
			for (int y = 0; y < info.MapSize; y++) {
				for (int x = 0; x < info.MapSize; x++) {
					values[x, y] = -1;
				}
			}
			var tiles = info.Tiles;
			var queue = new Queue<Int2>();
			int size = info.MapSize;
			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					values[x, y] = GetSlimeValue(x, y);
				}
			}
			// Func
			int GetSlimeValue (int x, int y) {
				var pivot = tiles[x, y];
				if (pivot != Tile.HittedShip && pivot != Tile.RevealedShip) { return 0; }
				int result = 0;
				queue.Clear();
				queue.Enqueue(new Int2(x, y));
				var hash = new HashSet<Int2> { new Int2(x, y) };
				while (queue.Count > 0) {
					var pos = queue.Dequeue();
					var tile = tiles[pos.x, pos.y];
					if (tile == Tile.HittedShip || tile == Tile.RevealedShip) {
						if (filter.HasFlag(tile)) {
							result++;
						}
					}
					for (int _j = pos.y - 1; _j <= pos.y + 1; _j++) {
						for (int _i = pos.x - 1; _i <= pos.x + 1; _i++) {
							if (_i < 0 || _j < 0 || _i >= size || _j >= size) { continue; }
							if (_i == pos.x && _j == pos.y) { continue; }
							if (hash.Contains(new Int2(_i, _j))) { continue; }
							var _tile = tiles[_i, _j];
							if (_tile == Tile.RevealedShip || _tile == Tile.HittedShip) {
								queue.Enqueue(new Int2(_i, _j));
								hash.Add(new Int2(_i, _j));
							}
						}
					}
				}
				return result;
			}
		}


		// Util
		public int GetMostExposedPositionIndex (Ship ship, Tile[,] tiles, List<ShipPosition> positions) {
			if (ship == null || positions == null || positions.Count == 0) { return -1; }
			if (positions.Count == 1) { return 0; }
			int maxEx = 0;
			int maxIndex = -1;
			for (int i = 0; i < positions.Count; i++) {
				int ex = Exposure(ship, positions[i]);
				if (ex > maxEx || (ex == maxEx && Random.NextDouble() > 0.5f)) {
					maxEx = ex;
					maxIndex = i;
				}
			}
			return maxIndex;
			// Func
			int Exposure (Ship _ship, ShipPosition _sPos) {
				int result = 0;
				foreach (var v in _ship.Body) {
					var pos = _sPos.GetPosition(v);
					switch (tiles[pos.x, pos.y]) {
						case Tile.RevealedShip:
						case Tile.HittedShip:
							result++;
							break;
					}
				}
				return result;
			}
		}


		public int GetShipWithMinimalPotentialPosCount (BattleInfo info, List<ShipPosition>[] hiddenPositions, List<ShipPosition>[] exposedPositions) => GetShipWithMinimalPotentialPosCount(info, hiddenPositions, exposedPositions, out _);


		public int GetShipWithMinimalPotentialPosCount (BattleInfo info, List<ShipPosition>[] hiddenPositions, List<ShipPosition>[] exposedPositions, out bool exposed) {
			int bestTargetIndex = -1;
			int bestHiddenPosLeft = int.MaxValue;
			int bestExposedPosLeft = int.MaxValue;
			int bestExTargetIndex = -1;
			int bestHdTargetIndex = -1;
			exposed = false;
			for (int i = 0; i < info.Ships.Length; i++) {
				var alive = info.ShipsAlive[i];
				if (!alive) { continue; }
				int exCount = exposedPositions[i].Count;
				if (exCount > 0 && exCount < bestExposedPosLeft) {
					bestExposedPosLeft = exCount;
					bestExTargetIndex = i;
				}
				int hdCount = hiddenPositions[i].Count;
				if (hdCount > 0 && hdCount < bestHiddenPosLeft) {
					bestHiddenPosLeft = hdCount;
					bestHdTargetIndex = i;
				}
			}
			if (bestExTargetIndex >= 0) {
				exposed = true;
				bestTargetIndex = bestExTargetIndex;
			} else if (bestHdTargetIndex >= 0) {
				exposed = false;
				bestTargetIndex = bestHdTargetIndex;
			}
			return bestTargetIndex;
		}


		public (Int2 pos, float max) GetMaxValue (float[,,] values, int index) {
			Int2 pos = default;
			float max = 0;
			int mapSize = values.GetLength(1);
			for (int j = 0; j < mapSize; j++) {
				for (int i = 0; i < mapSize; i++) {
					float v0 = values[index, i, j];
					if (v0 > max || (v0 == max && Random.NextDouble() > 0.5f)) {
						max = v0;
						pos.x = i;
						pos.y = j;
					}
				}
			}
			return (pos, max);
		}


		public bool ContainsTile (Ship ship, ShipPosition position, Tile[,] tiles, Tile filter) {
			foreach (var v in ship.Body) {
				var pos = position.GetPosition(v);
				if (filter.HasFlag(tiles[pos.x, pos.y])) {
					return true;
				}
			}
			return false;
		}


		public bool GetFirstTile (Tile[,] tiles, Tile filter, out Int2 result) {
			result = default;
			int size = tiles.GetLength(0);
			for (int j = 0; j < size; j++) {
				for (int i = 0; i < size; i++) {
					if (filter.HasFlag(tiles[i, j])) {
						result = new Int2(i, j);
						return true;
					}
				}
			}
			return false;
		}


		public bool GetFirstTileInShip (Tile[,] tiles, Tile filter, Ship ship, ShipPosition shipPos, out Int2 result) {
			result = default;
			foreach (var v in ship.Body) {
				var pos = shipPos.GetPosition(v);
				if (filter.HasFlag(tiles[pos.x, pos.y])) {
					result = pos;
					return true;
				}
			}
			return false;
		}


		public bool GetMVTInShip (Tile[,] tiles, Ship ship, List<Attack> attacks, ShipPosition shipPos, out Int2 position, out AbilityDirection direction) {
			position = default;
			direction = default;
			var hash = new HashSet<Int2>();
			foreach (var v in ship.Body) {
				var pos = shipPos.GetPosition(v);
				if (!hash.Contains(pos)) {
					hash.Add(pos);
				}
			}
			int maxValue = 0;
			int mapSize = tiles.GetLength(0);
			var filter = Tile.RevealedShip | Tile.GeneralWater;
			for (int j = 0; j < mapSize; j++) {
				for (int i = 0; i < mapSize; i++) {
					for (int dirIndex = 0; dirIndex < 4; dirIndex++) {
						var dir = (AbilityDirection)dirIndex;
						int value = GetValue(i, j, dir);
						if (value > maxValue) {
							maxValue = value;
							position.x = i;
							position.y = j;
							direction = dir;
						}
					}
				}
			}
			return false;
			// Func
			int GetValue (int _i, int _j, AbilityDirection _dir) {
				int value = 0;
				foreach (var att in attacks) {
					if (att.Trigger == AttackTrigger.PassiveRandom || att.Trigger == AttackTrigger.Random) { continue; }
					var (x, y) = att.GetPosition(_i, _j, _dir);
					if (x < 0 || y < 0 || x >= mapSize || y >= mapSize) { continue; }
					if (hash.Contains(new Int2(x, y)) && filter.HasFlag(tiles[x, y])) {
						value++;
					}
				}
				return value;
			}
		}


		public int GetTileCountInShip (Tile[,] tiles, Tile filter, Ship ship, ShipPosition shipPos) {
			int result = 0;
			foreach (var v in ship.Body) {
				var pos = shipPos.GetPosition(v);
				if (filter.HasFlag(tiles[pos.x, pos.y])) {
					result++;
				}
			}
			return result;
		}


		public int CountNeighborTile (Tile[,] tiles, int x, int y, Tile filter) {
			int result = 0;
			int size = tiles.GetLength(0);
			int l = System.Math.Max(x - 1, 0);
			int r = System.Math.Min(x + 1, size - 1);
			int d = System.Math.Max(y - 1, 0);
			int u = System.Math.Min(y + 1, size - 1);
			for (int j = d; j <= u; j++) {
				for (int i = l; i <= r; i++) {
					if (filter.HasFlag(tiles[i, j])) {
						result++;
					}
				}
			}
			return result;
		}


		public bool AveragePosition (Tile[,] tiles, Tile filter, out Float2 pos) {
			Float2? resultPos = null;
			int size = tiles.GetLength(0);
			float count = 0f;
			pos = default;
			for (int j = 0; j < size; j++) {
				for (int i = 0; i < size; i++) {
					var tile = tiles[i, j];
					if (!filter.HasFlag(tile)) { continue; }
					if (!resultPos.HasValue) {
						resultPos = new Float2(i, j);
					} else {
						resultPos += new Float2(i, j);
					}
					count++;
				}
			}
			if (resultPos.HasValue && count > 0) {
				resultPos /= count;
				pos = resultPos.Value;
				return true;
			} else {
				return false;
			}
		}


		public bool NearestPosition (Tile[,] tiles, int x, int y, Tile filter, out Int2 pos, out float sqrtDistance) {
			pos = default;
			sqrtDistance = float.MaxValue;
			int size = tiles.GetLength(0);
			bool success = false;
			for (int j = 0; j < size; j++) {
				for (int i = 0; i < size; i++) {
					var tile = tiles[i, j];
					if (!filter.HasFlag(tile)) { continue; }
					float a = System.Math.Abs(x - i);
					float b = System.Math.Abs(y - j);
					float sqrtDis = a * a + b * b;
					if (sqrtDis < sqrtDistance) {
						sqrtDistance = sqrtDis;
						pos.x = i;
						pos.y = j;
						success = true;
					}
				}
			}
			return success;
		}


		public bool ContainsTile (Tile[,] tiles, Tile filter) {
			int size = tiles.GetLength(0);
			for (int j = 0; j < size; j++) {
				for (int i = 0; i < size; i++) {
					if (filter.HasFlag(tiles[i, j])) {
						return true;
					}
				}
			}
			return false;
		}


		#endregion




	}
}
