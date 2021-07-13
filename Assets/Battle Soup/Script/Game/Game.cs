using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using BattleSoupAI;


namespace BattleSoup {
	public class Game : MonoBehaviour {




		#region --- SUB ---


		public delegate ShipData ShipDataStringHandler (string key);

		[System.Serializable] public class VoidVector3Event : UnityEvent<Vector3> { }
		[System.Serializable] public class VoidEvent : UnityEvent { }
		[System.Serializable] public class VoidStringEvent : UnityEvent<string> { }



		private class GameData {

			public SoupStrategy Strategy;
			public MapData Map = null;
			public ShipData[] ShipDatas = null;
			public Ship[] Ships = null;
			public Tile[,] Tiles = null;
			public int[] Cooldowns = null;
			public bool[] ShipsAlive = null;
			public bool[] SuperRevealed = null;
			public ShipPosition[] Positions = null;
			public readonly List<SonarPosition> Sonars = new List<SonarPosition>();

			public void Init (SoupStrategy strategy, MapData map, ShipData[] ships, ShipPosition[] positions) {

				Map = map;
				ShipDatas = ships;
				Strategy = strategy;

				// Ships
				Ships = ShipData.GetShips(ships);

				// Pos
				Positions = positions;

				// Tiles
				Tiles = new Tile[map.Size, map.Size];
				for (int j = 0; j < map.Size; j++) {
					for (int i = 0; i < map.Size; i++) {
						Tiles[i, j] = Tile.GeneralWater;
					}
				}
				foreach (var stone in map.Stones) {
					if (stone.x >= 0 && stone.x < map.Size && stone.y >= 0 && stone.y < map.Size) {
						Tiles[stone.x, stone.y] = Tile.GeneralStone;
					}
				}

				// Cooldown
				Cooldowns = new int[ships.Length];
				for (int i = 0; i < ships.Length; i++) {
					Cooldowns[i] = ships[i].Ship.Ability.Cooldown - 1;
				}

				// Ships Alive
				ShipsAlive = new bool[ShipDatas.Length];
				for (int i = 0; i < ShipsAlive.Length; i++) {
					ShipsAlive[i] = true;
				}

				// Super Revealed
				SuperRevealed = new bool[ShipDatas.Length];

			}

			public void Clear () {
				Map = null;
				ShipDatas = null;
				Ships = null;
				Tiles = null;
				ShipsAlive = null;
				Cooldowns = null;
				Positions = null;
				Sonars.Clear();
			}

		}


		private class AbilityPerformData {
			public int AbilityAttackIndex;
			public bool WaitForPicking;
			public bool PickedPerformed;
			public bool DoTiedup;

			public void Clear () {
				AbilityAttackIndex = 0;
				WaitForPicking = false;
				PickedPerformed = false;
				DoTiedup = false;
			}

		}


		[System.Flags]
		private enum AttackResult {
			None = 0,
			Miss = 1 << 0,
			HitShip = 1 << 1,
			RevealShip = 1 << 2,
			SunkShip = 1 << 3,
		}


		#endregion



		#region --- VAR ---


		// Api
		public static ShipDataStringHandler GetShip { get; set; } = null;
		public Group CurrentTurn { get; private set; } = Group.A;
		public AbilityDirection AbilityDirection { get; private set; } = AbilityDirection.Up;
		public int AbilityShipIndex { get; private set; } = -1;
		public string PrevUsedAbilityA { get; private set; } = "";
		public string PrevUsedAbilityB { get; private set; } = "";
		public bool Cheated { get; set; } = false;

		// Ser
		[SerializeField] BattleSoupUI m_SoupA = null;
		[SerializeField] BattleSoupUI m_SoupB = null;
		[SerializeField] Image m_Face = null;
		[SerializeField] Toggle m_CheatToggle = null;
		[SerializeField] Sprite[] m_Faces = null;
		[SerializeField] VoidEvent m_RefreshUI = null;
		[SerializeField] VoidVector3Event m_OnShipHitted = null;
		[SerializeField] VoidVector3Event m_OnShipSunk = null;
		[SerializeField] VoidVector3Event m_OnWaterRevealed = null;
		[SerializeField] VoidVector3Event m_OnShipRevealed = null;
		[SerializeField] VoidVector3Event m_OnSonar = null;
		[SerializeField] VoidStringEvent m_ShowMessage = null;

		// Data
		private readonly GameData DataA = new GameData();
		private readonly GameData DataB = new GameData();
		private readonly Vector3[] WorldCornerCaches = new Vector3[4];
		private BattleMode CurrentBattleMode = BattleMode.PvA;
		private readonly AbilityPerformData AbilityData = new AbilityPerformData();
		private float AllowUpdateTime = 0f;
		private readonly Attack DEFAULT_ATTACK = new Attack() {
			X = 0,
			Y = 0,
			AvailableTarget = Tile.GeneralWater | Tile.RevealedShip,
			Trigger = AttackTrigger.Picked,
			Type = AttackType.HitTile,
		};


		#endregion




		#region --- MSG ---


		private void OnEnable () {
			if (AllShipsSunk(Group.A) || AllShipsSunk(Group.B)) {
				gameObject.SetActive(false);
			}
		}


		private void Update () {
			Update_Aim();
			Update_Turn();
		}


		private void Update_Turn () {

			if (Time.time < AllowUpdateTime) { return; }

			if (CurrentTurn == Group.A) {
				// A Turn
				if (CurrentBattleMode == BattleMode.PvA) {
					// Player 
					if (Input.GetMouseButtonDown(0)) {
						// Mouse Left
						if (m_SoupB.GetMapPositionInside(Input.mousePosition, out var pos)) {
							if (AbilityData.WaitForPicking) {
								// Ability Attack
								AbilityData.PickedPerformed = false;
								AbilityData.WaitForPicking = false;
								if (PerformAbility(pos.x, pos.y, out _)) {
									DelayUpdate(0.1f);
									SwitchTurn();
								} else {
									AbilityData.WaitForPicking = true;
									m_RefreshUI.Invoke();
								}
							} else if (AttackTile(DEFAULT_ATTACK, pos.x, pos.y, Group.B) != AttackResult.None) {
								// Normal Attack
								PerformPassiveAttack();
								DelayUpdate(0.1f);
								SwitchTurn();
							}
						} else if (AbilityData.AbilityAttackIndex == 0) {
							OnAbilityCancel();
						}
					} else if (Input.GetMouseButtonDown(1)) {
						if (AbilityShipIndex >= 0) {
							AbilityDirection = (AbilityDirection)(((int)AbilityDirection + 1) % 4);
						}
					}
				} else {
					// Robot A
					PerformRobotTurn(Group.A);
				}
			} else {
				// B Turn
				PerformRobotTurn(Group.B);
			}
			// Func
			void PerformRobotTurn (Group group) {
				var ownData = group == Group.A ? DataA : DataB;
				var oppData = group == Group.A ? DataB : DataA;
				var oppGroup = group == Group.A ? Group.B : Group.A;
				var result = ownData.Strategy.Analyse(
					new BattleInfo() {
						Ships = ownData.Ships,
						Tiles = ownData.Tiles,
						Cooldowns = ownData.Cooldowns,
					},
					new BattleInfo() {
						Ships = oppData.Ships,
						Tiles = oppData.Tiles,
						Cooldowns = oppData.Cooldowns,
					},
					ownData.Positions,
					AbilityShipIndex
				);
				if (result.Success) {
					if (result.AbilityIndex < 0) {
						// Normal Attack
						AttackTile(DEFAULT_ATTACK, result.TargetPosition.x, result.TargetPosition.y, oppGroup);
						PerformPassiveAttack();
						DelayUpdate(0.1f);
						SwitchTurn();
					} else {
						bool combo = AbilityShipIndex >= 0;
						if (AbilityShipIndex < 0) {
							// First Trigger
							if (!AbilityFirstTrigger(result.AbilityIndex)) {
								combo = true;
							}
						}
						if (combo) {
							// Combo
							AbilityDirection = result.AbilityDirection;
							if (AbilityData.WaitForPicking) {
								// Ability Attack
								AbilityData.PickedPerformed = false;
								AbilityData.WaitForPicking = false;
								if (PerformAbility(result.TargetPosition.x, result.TargetPosition.y, out bool error) || error) {
									DelayUpdate(0.1f);
									SwitchTurn();
								} else {
									AbilityData.WaitForPicking = true;
									m_RefreshUI.Invoke();
								}
							}
						}
					}
				}
			}
		}


		private void Update_Aim () {
			if (
				CurrentTurn == Group.A &&
				CurrentBattleMode == BattleMode.PvA &&
				AbilityShipIndex >= 0
			) {
				m_SoupB.RefreshAimRenderer();
			} else {
				m_SoupB.ClearAimRenderer();
			}
			m_SoupA.ClearAimRenderer();
		}


		#endregion




		#region --- API ---


		public void Init (BattleMode battleMode, SoupStrategy strategyA, SoupStrategy strategyB, MapData mapA, MapData mapB, ShipData[] shipsA, ShipData[] shipsB, ShipPosition[] positionsA, ShipPosition[] positionsB) {
			CurrentBattleMode = battleMode;
			CurrentTurn = Random.value > 0.5f ? Group.A : Group.B;
			AbilityData.Clear();
			DataA.Clear();
			DataB.Clear();
			DataA.Init(strategyA, mapA, shipsA, positionsA);
			DataB.Init(strategyB, mapB, shipsB, positionsB);
			m_CheatToggle.isOn = false;
			PrevUsedAbilityA = "";
			PrevUsedAbilityB = "";
			Cheated = false;
			m_CheatToggle.gameObject.SetActive(true);
		}


		public void SetupDelegate () {
			m_SoupA.GetTile = (x, y) => DataA.Tiles[x, y];
			m_SoupB.GetTile = (x, y) => DataB.Tiles[x, y];
			m_SoupA.GetMap = () => DataA.Map;
			m_SoupB.GetMap = () => DataB.Map;
			m_SoupA.GetShips = () => DataA.ShipDatas;
			m_SoupB.GetShips = () => DataB.ShipDatas;
			m_SoupA.GetPositions = () => DataA.Positions;
			m_SoupB.GetPositions = () => DataB.Positions;
			m_SoupA.CheckShipAlive = (index) => CheckShipAlive(index, Group.A);
			m_SoupB.CheckShipAlive = (index) => CheckShipAlive(index, Group.B);
			m_SoupA.GetSonars = () => DataA.Sonars;
			m_SoupB.GetSonars = () => DataB.Sonars;
			m_SoupA.GetCurrentAbility = m_SoupB.GetCurrentAbility = () => {
				if (AbilityShipIndex >= 0) {
					return (CurrentTurn == Group.A ? DataA : DataB).Ships[AbilityShipIndex].Ability;
				}
				return null;
			};
			m_SoupA.GetCurrentAbilityDirection = m_SoupB.GetCurrentAbilityDirection = () => AbilityDirection;
			m_SoupA.GetCheating = m_SoupB.GetCheating = () => m_CheatToggle.isOn;
			m_SoupA.CheckShipSuperRevealed = (index) => DataA.SuperRevealed[index];
			m_SoupB.CheckShipSuperRevealed = (index) => DataB.SuperRevealed[index];
			m_SoupA.GetOpponentPrevUseShip = () => GetShip(PrevUsedAbilityB);
			m_SoupB.GetOpponentPrevUseShip = () => GetShip(PrevUsedAbilityA);
		}


		public void UI_Clear () {
			DataA.Clear();
			DataB.Clear();
		}


		// Ship
		public bool CheckShipAlive (int index, Group group) => (group == Group.A ? DataA : DataB).ShipsAlive[index];


		public ShipData GetShipData (Group group, int index) {
			var ships = group == Group.A ? DataA.ShipDatas : DataB.ShipDatas;
			return ships[Mathf.Clamp(index, 0, ships.Length - 1)];
		}


		// Ability
		public int GetCooldown (Group group, int index) {
			var cooldowns = group == Group.A ? DataA.Cooldowns : DataB.Cooldowns;
			return cooldowns[Mathf.Clamp(index, 0, cooldowns.Length - 1)];
		}


		public Ability GetAbility (Group group, int index) {
			var ships = group == Group.A ? DataA.ShipDatas : DataB.ShipDatas;
			return ships[Mathf.Clamp(index, 0, ships.Length - 1)].Ship.Ability;
		}


		public void OnAbilityClick (int shipIndex) {
			if (
				!gameObject.activeSelf ||
				CurrentBattleMode != BattleMode.PvA ||
				CurrentTurn != Group.A ||
				AbilityShipIndex >= 0
			) { return; }
			AbilityFirstTrigger(shipIndex);
		}


		#endregion




		#region --- LGC ---


		private void SwitchTurn () {

			AbilityData.Clear();
			AbilityShipIndex = -1;

			if (!gameObject.activeSelf) { return; }

			// Check Win
			if (AllShipsSunk(Group.A)) {
				if (CurrentBattleMode == BattleMode.PvA) {
					m_Face.gameObject.SetActive(true);
					m_Face.sprite = m_Faces[Cheated ? 3 : 1];
					m_ShowMessage.Invoke(Cheated ? "You cheated but still lose.\nThat sucks..." : "You Lose");
				} else {
					m_Face.gameObject.SetActive(false);
					m_Face.sprite = null;
					m_ShowMessage.Invoke("Robot B Win");
				}
				gameObject.SetActive(false);
				m_CheatToggle.gameObject.SetActive(false);
				m_RefreshUI.Invoke();
				RefreshAllSoupRenderers();
				return;
			} else if (AllShipsSunk(Group.B)) {
				if (CurrentBattleMode == BattleMode.PvA) {
					m_Face.gameObject.SetActive(true);
					m_Face.sprite = m_Faces[Cheated ? 2 : 0];
					m_ShowMessage.Invoke(Cheated ? "You didn't win.\nBecause you cheated." : "You Win");
				} else {
					m_Face.gameObject.SetActive(false);
					m_Face.sprite = null;
					m_ShowMessage.Invoke("Robot A Win");
				}
				gameObject.SetActive(false);
				m_CheatToggle.gameObject.SetActive(false);
				m_RefreshUI.Invoke();
				RefreshAllSoupRenderers();
				return;
			}

			// Cooldown
			var cooldowns = CurrentTurn == Group.A ? DataA.Cooldowns : DataB.Cooldowns;
			for (int i = 0; i < cooldowns.Length; i++) {
				cooldowns[i] = Mathf.Max(cooldowns[i] - 1, 0);
			}

			// Refresh
			RefreshAllSoupRenderers();

			// Turn Change
			CurrentTurn = CurrentTurn == Group.A ? Group.B : Group.A;
			m_RefreshUI.Invoke();

		}


		private bool AllShipsSunk (Group group) {
			int count = (group == Group.A ? DataA.ShipDatas.Length : DataB.ShipDatas.Length);
			for (int i = 0; i < count; i++) {
				if (CheckShipAlive(i, group)) {
					return false;
				}
			}
			return true;
		}


		private void RefreshShipsAlive (int index, Group group) {
			var data = group == Group.A ? DataA : DataB;
			if (index >= 0) {
				data.ShipsAlive[index] = CheckShipAlive(index);
			} else {
				for (int i = 0; i < data.ShipsAlive.Length; i++) {
					data.ShipsAlive[i] = CheckShipAlive(i);
				}
			}
			// Func
			bool CheckShipAlive (int _index) {
				var ships = data.Ships;
				var positions = data.Positions;
				var map = data.Map;
				var tiles = data.Tiles;
				_index = Mathf.Clamp(_index, 0, ships.Length - 1);
				var ship = ships[_index];
				var body = ship.Body;
				var sPos = positions[_index];
				int aliveTile = 0;
				foreach (var v in body) {
					var pos = new Vector2Int(
						sPos.Pivot.x + (sPos.Flip ? v.y : v.x),
						sPos.Pivot.y + (sPos.Flip ? v.x : v.y)
					);
					if (pos.x >= 0 && pos.x < map.Size && pos.y >= 0 && pos.y < map.Size) {
						if (tiles[pos.x, pos.y] != Tile.HittedShip) {
							aliveTile++;
							if (aliveTile > ship.TerminateHP) {
								return true;
							}
						}
					}
				}
				return false;
			}
		}


		private Vector3 GetWorldPosition (int x, int y, Group group) {
			var rt = (group == Group.A ? m_SoupA.transform : m_SoupB.transform) as RectTransform;
			var map = group == Group.A ? DataA.Map : DataB.Map;
			rt.GetWorldCorners(WorldCornerCaches);
			var min = WorldCornerCaches[0];
			var max = WorldCornerCaches[2];
			return new Vector3(
				Mathf.LerpUnclamped(min.x, max.x, (x + 0.5f) / map.Size),
				Mathf.LerpUnclamped(min.y, max.y, (y + 0.5f) / map.Size),
				rt.position.z
			);
		}


		private void DelayUpdate (float second) => AllowUpdateTime = Time.time + second;


		private void RefreshAllSoupRenderers () {
			m_SoupA.RefreshHitRenderer();
			m_SoupB.RefreshHitRenderer();
			m_SoupA.RefreshShipRenderer();
			m_SoupB.RefreshShipRenderer();
			m_SoupA.RefreshSonarRenderer();
			m_SoupB.RefreshSonarRenderer();
		}


		// Passive
		private void PerformPassiveAttack () {
			var data = CurrentTurn == Group.A ? DataA : DataB;
			var opGroup = CurrentTurn == Group.A ? Group.B : Group.A;
			var ships = data.Ships;
			for (int shipIndex = 0; shipIndex < ships.Length; shipIndex++) {
				var ship = ships[shipIndex];
				if (ship.Ability.HasPassive) {
					for (int i = 0; i < ship.Ability.Attacks.Count; i++) {
						var attack = ship.Ability.Attacks[i];
						if (attack.Trigger == AttackTrigger.PassiveRandom) {
							AttackTile(attack, 0, 0, opGroup, shipIndex);
						}
					}
				}
			}
		}


		// Ability
		private bool PerformAbility (int x, int y, out bool error) {

			error = false;
			var aData = AbilityData;
			if (AbilityShipIndex < 0) { error = true; return false; }
			if (!CheckShipAlive(AbilityShipIndex, CurrentTurn)) { error = true; return false; }

			var opponentGroup = CurrentTurn == Group.A ? Group.B : Group.A;
			var data = CurrentTurn == Group.A ? DataA : DataB;
			var opData = CurrentTurn == Group.A ? DataB : DataA;
			var selfAbility = data.Ships[AbilityShipIndex].Ability;
			var performAbility = selfAbility;
			string performID = data.ShipDatas[AbilityShipIndex].GlobalID;
			if (selfAbility.CopyOpponentLastUsed) {
				string oppPrevUseAbilityKey = opponentGroup == Group.A ? PrevUsedAbilityA : PrevUsedAbilityB;
				var aShip = GetShip(oppPrevUseAbilityKey);
				if (aShip != null) {
					performAbility = aShip.Ship.Ability;
					performID = aShip.GlobalID;
				}
			}

			if (performAbility.Attacks == null || performAbility.Attacks.Count == 0) { error = true; return false; }

			// Perform Attack
			for (
				aData.AbilityAttackIndex = Mathf.Clamp(aData.AbilityAttackIndex, 0, performAbility.Attacks.Count - 1);
				aData.AbilityAttackIndex < performAbility.Attacks.Count;
				aData.AbilityAttackIndex++
			) {
				var attack = performAbility.Attacks[aData.AbilityAttackIndex];
				AttackResult result = AttackResult.None;
				bool isHit = attack.Type == AttackType.HitTile || attack.Type == AttackType.HitWholeShip;
				switch (attack.Trigger) {


					case AttackTrigger.Picked:
						if (!aData.PickedPerformed) {
							// Check Target
							if (!attack.AvailableTarget.HasFlag(opData.Tiles[x, y])) {
								error = true;
								return false;
							}
							result = AttackTile(
								attack, x, y,
								opponentGroup,
								AbilityShipIndex,
								AbilityDirection
							);
							aData.PickedPerformed = true;
							if (result != AttackResult.None) {
								aData.DoTiedup = true;
							}
							break;
						} else {
							aData.WaitForPicking = true;
							return false;
						}


					case AttackTrigger.TiedUp:
						if (!aData.DoTiedup) { break; }
						result = AttackTile(
							attack, x, y,
							opponentGroup,
							AbilityShipIndex,
								AbilityDirection
						);
						break;


					case AttackTrigger.Random:
						result = AttackTile(
							attack, x, y,
							opponentGroup,
							AbilityShipIndex,
								AbilityDirection
						);
						break;


				}

				// Break Check
				if (
					(performAbility.BreakOnMiss && isHit && result.HasFlag(AttackResult.Miss)) ||
					(performAbility.BreakOnSunk && result.HasFlag(AttackResult.SunkShip))
				) { break; }

			}

			// Prev Use
			if (performAbility.HasActive) {
				if (CurrentTurn == Group.A) {
					PrevUsedAbilityA = performID;
				} else {
					PrevUsedAbilityB = performID;
				}
			}

			// Final
			data.Cooldowns[AbilityShipIndex] = selfAbility.Cooldown;
			aData.Clear();
			return true;
		}


		private void OnAbilityCancel () {
			AbilityShipIndex = -1;
			AbilityData.Clear();
			m_RefreshUI.Invoke();
		}


		private bool AbilityFirstTrigger (int shipIndex) {
			AbilityShipIndex = shipIndex;
			AbilityData.AbilityAttackIndex = 0;
			AbilityData.WaitForPicking = true;
			AbilityData.PickedPerformed = true;
			AbilityDirection = AbilityDirection.Up;
			if (PerformAbility(0, 0, out _)) {
				DelayUpdate(0.1f);
				SwitchTurn();
				m_RefreshUI.Invoke();
				return true;
			} else {
				m_RefreshUI.Invoke();
				return false;
			}
		}


		// Attack
		private AttackResult AttackTile (Attack attack, int x, int y, Group targetGroup, int attackFromShipIndex = -1, AbilityDirection direction = default) {

			if (!gameObject.activeSelf) { return AttackResult.None; }

			var data = targetGroup == Group.A ? DataA : DataB;
			var soup = targetGroup == Group.A ? m_SoupA : m_SoupB;
			var ownGroup = targetGroup == Group.A ? Group.B : Group.A;
			var ownData = targetGroup == Group.A ? DataB : DataA;
			var ownSoup = targetGroup == Group.A ? m_SoupB : m_SoupA;
			bool useOwn = attack.Type == AttackType.RevealOwnUnoccupiedTile;
			bool needRefreshShip = false;
			bool needRefreshHit = false;
			bool needRefreshSonar = false;

			if (attack.Type != AttackType.RevealSelf) {
				// Pos
				if (attack.Trigger == AttackTrigger.Random || attack.Trigger == AttackTrigger.PassiveRandom) {
					// Random
					if (!useOwn) {
						// Random in Target Soup
						if (!data.Map.GetRandomTile(attack.AvailableTarget, data.Tiles, out x, out y)) { return AttackResult.None; }
					} else {
						// Random in Own Soup
						if (!ownData.Map.GetRandomTile(
							attack.AvailableTarget,
							ownData.Tiles,
							out x, out y,
							(_x, _y) => !ShipData.Contains(
								_x, _y, ownData.ShipDatas, ownData.Positions, out _)
							)
						) { return AttackResult.None; }
					}
				} else {
					// Aim
					(x, y) = attack.GetPosition(x, y, direction);
				}

				// Inside Check
				if (x < 0 || y < 0 || x >= data.Map.Size || y >= data.Map.Size) { return AttackResult.None; }

				// Target Check
				if (!attack.AvailableTarget.HasFlag(
					(!useOwn ? data : ownData).Tiles[x, y]
				)) { return AttackResult.None; }
			}

			// Do Attack
			var result = AttackResult.None;
			switch (attack.Type) {
				case AttackType.HitTile:
				case AttackType.HitWholeShip: {
					HitTile(x, y, targetGroup, data, attack.Type == AttackType.HitWholeShip, out var hitShip, out var sunkShip);
					needRefreshShip = sunkShip;
					needRefreshHit = true;
					result = hitShip ? AttackResult.HitShip : AttackResult.Miss;
					if (sunkShip) {
						result |= AttackResult.SunkShip;
					}
					break;
				}
				case AttackType.RevealTile:
				case AttackType.RevealWholeShip: {
					RevealTile(x, y, targetGroup, data, attack.Type == AttackType.RevealWholeShip, true, out var revealShip);
					needRefreshHit = true;
					result = revealShip ? AttackResult.RevealShip : AttackResult.Miss;
					if (attack.Type == AttackType.RevealWholeShip) {
						needRefreshShip = true;
					}
					break;
				}
				case AttackType.Sonar: {
					SonarTile(x, y, targetGroup, data, out var hitShip, out var sunkShip);
					needRefreshHit = true;
					needRefreshSonar = true;
					result = hitShip ? AttackResult.HitShip : AttackResult.Miss;
					if (sunkShip) {
						result |= AttackResult.SunkShip;
					}
					break;
				}
				case AttackType.RevealOwnUnoccupiedTile: {
					RevealTile(x, y, ownGroup, ownData, false, true, out _);
					ownSoup.RefreshHitRenderer();
					break;
				}
				case AttackType.RevealSelf: {
					if (attackFromShipIndex < 0) { break; }
					RevealWholeShip(ownData, attackFromShipIndex, ownGroup);
					ownSoup.RefreshHitRenderer();
					break;
				}
			}

			// Refresh
			if (needRefreshHit) {
				soup.RefreshHitRenderer();
			}
			if (needRefreshShip) {
				soup.RefreshShipRenderer();
			}
			if (needRefreshSonar) {
				soup.RefreshSonarRenderer();
			}

			return result;
		}


		private void HitTile (int x, int y, Group targetGroup, GameData targetData, bool hitWholeShip, out bool hitShip, out bool sunkShip) {
			sunkShip = false;
			hitShip = false;
			var wPos = GetWorldPosition(x, y, targetGroup);
			if (ShipData.Contains(x, y, targetData.ShipDatas, targetData.Positions, out int _shipIndex)) {
				// Hit Ship
				if (!hitWholeShip) {
					// Just Tile
					bool prevAlive = CheckShipAlive(_shipIndex, targetGroup);
					targetData.Tiles[x, y] = Tile.HittedShip;
					RefreshShipsAlive(_shipIndex, targetGroup);
					if (targetData.ShipsAlive[_shipIndex]) {
						// No Sunk
						m_OnShipHitted.Invoke(wPos);
					} else {
						// Sunk
						sunkShip = true;
						SetTilesToHit(targetData.Ships[_shipIndex], targetData.Positions[_shipIndex]);
						if (prevAlive) {
							m_OnShipSunk.Invoke(wPos);
						} else {
							m_OnShipHitted.Invoke(wPos);
						}
					}
					if (targetData.Ships[_shipIndex].Ability.ResetCooldownOnHit) {
						// Reset Cooldown On Hit
						targetData.Cooldowns[_shipIndex] = 0;
					}
				} else {
					// Whole Ship
					var ship = targetData.ShipDatas[_shipIndex];
					var sPos = targetData.Positions[_shipIndex];
					foreach (var v in ship.Ship.Body) {
						int _x = sPos.Pivot.x + (sPos.Flip ? v.y : v.x);
						int _y = sPos.Pivot.y + (sPos.Flip ? v.x : v.y);
						if (targetData.Tiles[_x, _y] != Tile.HittedShip) {
							targetData.Tiles[_x, _y] = Tile.HittedShip;
							m_OnShipHitted.Invoke(GetWorldPosition(_x, _y, targetGroup));
						}
					}
					RefreshShipsAlive(_shipIndex, targetGroup);
					sunkShip = true;
					SetTilesToHit(targetData.Ships[_shipIndex], targetData.Positions[_shipIndex]);
					m_OnShipSunk.Invoke(wPos);
				}
				hitShip = true;
			} else if (targetData.Map.HasStone(x, y)) {
				// Hit Stone
				targetData.Tiles[x, y] = Tile.RevealedStone;
				m_OnWaterRevealed.Invoke(wPos);
				hitShip = false;
			} else {
				// Hit Water
				targetData.Tiles[x, y] = Tile.RevealedWater;
				m_OnWaterRevealed.Invoke(wPos);
				hitShip = false;
			}
			// Func
			void SetTilesToHit (Ship ship, ShipPosition position) {
				foreach (var v in ship.Body) {
					targetData.Tiles[
						position.Pivot.x + (position.Flip ? v.y : v.x),
						position.Pivot.y + (position.Flip ? v.x : v.y)
					] = Tile.HittedShip;
				}
			}
		}


		private void RevealTile (int x, int y, Group group, GameData data, bool revealWholeShip, bool useCallback, out bool revealedShip) {
			var wPos = GetWorldPosition(x, y, group);
			var tile = data.Tiles[x, y];
			if (ShipData.Contains(x, y, data.ShipDatas, data.Positions, out int _shipIndex)) {
				if (!revealWholeShip) {
					// Just Tile
					if (tile != Tile.HittedShip && tile != Tile.RevealedShip) {
						data.Tiles[x, y] = Tile.RevealedShip;
						if (useCallback) {
							m_OnShipRevealed.Invoke(wPos);
						}
					}
				} else {
					// Whole Ship
					RevealWholeShip(data, _shipIndex, group);
				}
				revealedShip = true;
			} else if (data.Map.HasStone(x, y)) {
				// Stone
				if (tile != Tile.RevealedStone) {
					data.Tiles[x, y] = Tile.RevealedStone;
					if (useCallback) {
						m_OnWaterRevealed.Invoke(wPos);
					}
				}
				revealedShip = false;
			} else {
				// Just Water
				if (tile != Tile.RevealedWater) {
					data.Tiles[x, y] = Tile.RevealedWater;
					if (useCallback) {
						m_OnWaterRevealed.Invoke(wPos);
					}
				}
				revealedShip = false;
			}
		}


		private void RevealWholeShip (GameData data, int shipIndex, Group group) {
			var ship = data.ShipDatas[shipIndex];
			var sPos = data.Positions[shipIndex];
			data.SuperRevealed[shipIndex] = true;
			foreach (var v in ship.Ship.Body) {
				int _x = sPos.Pivot.x + (sPos.Flip ? v.y : v.x);
				int _y = sPos.Pivot.y + (sPos.Flip ? v.x : v.y);
				var tile = data.Tiles[_x, _y];
				if (tile != Tile.RevealedShip && tile != Tile.HittedShip) {
					data.Tiles[_x, _y] = Tile.RevealedShip;
					m_OnShipRevealed.Invoke(GetWorldPosition(_x, _y, group));
				}
			}
		}


		private void SonarTile (int x, int y, Group group, GameData data, out bool hitShip, out bool sunkShip) {
			hitShip = false;
			sunkShip = false;
			if (ShipData.Contains(x, y, data.ShipDatas, data.Positions, out _)) {
				// Hit When Has Ship
				HitTile(x, y, group, data, false, out hitShip, out sunkShip);
			} else {
				// Sonar Reveal When No Ship
				var wPos = GetWorldPosition(x, y, group);
				int mapSize = data.Map.Size;
				int minDis = ShipData.FindNearestShipDistance(
					x, y, data.ShipDatas, data.Positions, out var _pos
				);
				if (minDis == 0) {
					if (data.Tiles[_pos.x, _pos.y] != Tile.HittedShip) {
						HitTile(x, y, group, data, false, out hitShip, out sunkShip);
					}
				} else if (minDis > 0) {
					int l = x - minDis + 1;
					int r = x + minDis - 1;
					int d = y - minDis + 1;
					int u = y + minDis - 1;
					for (int i = l; i <= r; i++) {
						for (int j = d; j <= u; j++) {
							if (i < 0 || i >= mapSize || j < 0 || j >= mapSize) { continue; }
							if (Mathf.Abs(i - x) + Mathf.Abs(j - y) < minDis) {
								RevealTile(i, j, group, data, false, false, out _);
							}
						}
					}
					data.Sonars.Add(new SonarPosition(x, y, minDis));
				}
				m_OnSonar.Invoke(wPos);
			}
		}


		#endregion




	}
}
