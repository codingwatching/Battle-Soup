using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using AngeliaFramework;


// Start Remake at 2022/6/23
namespace BattleSoup {
	public class BattleSoup : Game {




		#region --- SUB ---



		public enum GameState {
			Title = 0,
			Prepare = 1,
			Playing = 2,
			CardGame = 3,
			ShipEditor = 4,
		}



		public enum GameMode {
			PvA = 0,
			AvA = 1,
		}


		public enum Turn {
			A = 0,
			B = 1,
		}


		[System.Serializable]
		private class GameAsset {
			public RectTransform CornerSoup = null;
			public RectTransform PanelRoot = null;
			public RectTransform PreparePanel = null;
			public RectTransform PlacePanel = null;
			public Button MapShipSelectorNextButton = null;
			[Header("Dialog")]
			public RectTransform DialogRoot = null;
			public RectTransform QuitBattleDialog = null;
			public RectTransform NoShipAlert = null;
			public RectTransform NoMapAlert = null;
			public RectTransform FailPlacingShipsDialog = null;
			public RectTransform RobotFailedToPlaceShipsDialog = null;
			public RectTransform Dialog_Win = null;
			public RectTransform Dialog_WinCheat = null;
			public RectTransform Dialog_Lose = null;
			public RectTransform Dialog_LoseCheat = null;
			[Header("Map")]
			public RectTransform MapSelectorContentA = null;
			public Text MapSelectorLabelA = null;
			public RectTransform MapSelectorContentB = null;
			public Text MapSelectorLabelB = null;
			public Grabber MapSelectorItem = null;
			[Header("Fleet")]
			public RectTransform FleetSelectorPlayer = null;
			public RectTransform FleetSelectorPlayerContent = null;
			public RectTransform FleetSelectorRobotA = null;
			public Grabber FleetSelectorShipItem = null;
			public Text FleetSelectorLabelA = null;
			public Text FleetSelectorLabelB = null;
			public RectTransform FleetRendererA = null;
			public RectTransform FleetRendererB = null;
			public Grabber FleetRendererItem = null;
			public Dropdown RobotAiA = null;
			public Dropdown RobotAiB = null;
			[Header("Setting")]
			public Toggle SoundTG = null;
			public Toggle AutoPlayAvATG = null;
			public Toggle UseAnimationTG = null;
			[Header("Playing")]
			public Toggle CheatTG = null;
			public Grabber ShipAbilityItem = null;
			public RectTransform AbilityContainerA = null;
			public RectTransform AbilityContainerB = null;
			[Header("Asset")]
			public Sprite DefaultShipIcon = null;
			public Sprite PlusSprite = null;
		}



		#endregion




		#region --- VAR ---


		// Api
		public GameState State { get; private set; } = GameState.Title;
		public GameMode Mode { get; private set; } = GameMode.PvA;
		public string ShipRoot => Util.CombinePaths(Util.GetRuntimeBuiltRootPath(), "Ships");
		public string MapRoot => Util.CombinePaths(Util.GetRuntimeBuiltRootPath(), "Maps");
		public eField FieldA { get; private set; } = null;
		public eField FieldB { get; private set; } = null;
		public bool Cheating { get; set; } = false;
		public bool Cheated { get; private set; } = false;
		public Turn CurrentTurn { get; private set; } = Turn.A;

		// Setting
		public bool UseSound { get => s_UseSound.Value; set => s_UseSound.Value = value; }
		public bool AutoPlayForAvA { get => s_AutoPlayForAvA.Value; set => s_AutoPlayForAvA.Value = value; }
		public bool UseAnimation { get => s_UseAnimation.Value; set => s_UseAnimation.Value = value; }

		// Ser
		[SerializeField] GameAsset m_Assets = null;

		// Data
		private readonly Dictionary<int, Ship> ShipPool = new();
		private readonly Dictionary<int, Ability> AbilityPool = new();
		private readonly List<SoupAI> AllAi = new();
		private readonly List<Map> AllMaps = new();
		private bool GameOver = false;

		// Saving
		private readonly SavingBool s_UseSound = new("BattleSoup.UseSound", true);
		private readonly SavingBool s_AutoPlayForAvA = new("BattleSoup.AutoPlayForAvA", false);
		private readonly SavingBool s_UseAnimation = new("BattleSoup.UseAnimation", true);
		private readonly SavingString s_PlayerFleet = new("BattleSoup.PlayerFleet", "Sailboat,SeaMonster,Longboat,MiniSub");
		private readonly SavingInt s_SelectingAiA = new("BattleSoup.SelectingAiA", 0);
		private readonly SavingInt s_SelectingAiB = new("BattleSoup.SelectingAiB", 0);
		private readonly SavingInt s_MapIndexA = new("BattleSoup.MapIndexA", 0);
		private readonly SavingInt s_MapIndexB = new("BattleSoup.MapIndexB", 0);


		#endregion




		#region --- MSG ---


		// Init
		protected override void Initialize () {

			base.Initialize();

			Init_AI();

			ReloadShipDataFromDisk();
			ReloadMapDataFromDisk();

			AddEntity(typeof(eBackgroundAnimation).AngeHash(), 0, 0);
			FieldA = AddEntity(typeof(eField).AngeHash(), 0, 0) as eField;
			FieldB = AddEntity(typeof(eField).AngeHash(), 0, 0) as eField;

			OnMapChanged();
			OnFleetChanged();

			SwitchState(GameState.Title);
			SwitchMode(GameMode.PvA);
			RefreshCameraView(true);
		}


		private void Init_AI () {
			AllAi.Clear();
			foreach (var type in typeof(SoupAI).AllChildClass()) {
				if (System.Activator.CreateInstance(type) is SoupAI ai) {
					AllAi.Add(ai);
				}
			}
			s_SelectingAiA.Value = s_SelectingAiA.Value.Clamp(0, AllAi.Count - 1);
			s_SelectingAiB.Value = s_SelectingAiB.Value.Clamp(0, AllAi.Count - 1);

			// UI
			m_Assets.RobotAiA.ClearOptions();
			m_Assets.RobotAiB.ClearOptions();
			var options = new List<string>();
			foreach (var ai in AllAi) {
				options.Add(ai.DisplayName);
			}
			m_Assets.RobotAiA.AddOptions(options);
			m_Assets.RobotAiB.AddOptions(options);
			m_Assets.RobotAiA.SetValueWithoutNotify(s_SelectingAiA.Value);
			m_Assets.RobotAiB.SetValueWithoutNotify(s_SelectingAiB.Value);
		}


		// Update
		protected override void FrameUpdate () {
			base.FrameUpdate();
			Update_StateRedirect();
			RefreshCameraView();
			switch (State) {
				case GameState.Title:
					Update_Title();
					break;
				case GameState.Prepare:
					Update_Prepare();
					break;
				case GameState.Playing:
					Update_Playing();
					break;
				case GameState.CardGame:

					break;
				case GameState.ShipEditor:

					break;
			}
		}


		private void Update_StateRedirect () {
			switch (State) {
				case GameState.Prepare:
				case GameState.Playing:
					if (ShipPool.Count == 0) {
						SwitchState(GameState.Title);
						m_Assets.NoShipAlert.gameObject.SetActive(true);
					}
					if (AllMaps.Count == 0) {
						SwitchState(GameState.Title);
						m_Assets.NoMapAlert.gameObject.SetActive(true);
					}
					break;
				case GameState.CardGame:

					break;
				case GameState.ShipEditor:
					if (AllMaps.Count == 0) {
						SwitchState(GameState.Title);
						m_Assets.NoMapAlert.gameObject.SetActive(true);
					}
					break;
			}
		}


		private void Update_Title () {
			FieldA.Enable = false;
			FieldB.Enable = false;
		}


		private void Update_Prepare () {

			bool preparing = m_Assets.PreparePanel.gameObject.activeSelf;
			m_Assets.PlacePanel.gameObject.SetActive(!preparing);

			// Map
			s_MapIndexA.Value = Mathf.Clamp(s_MapIndexA.Value, 0, AllMaps.Count);
			s_MapIndexB.Value = Mathf.Clamp(s_MapIndexB.Value, 0, AllMaps.Count);

			// Renderer
			FieldA.Enable = !preparing;
			FieldA.ShowShips = !preparing;
			FieldA.DragToMoveShips = !preparing;

			// UI
			m_Assets.MapShipSelectorNextButton.interactable = FieldA.Ships.Length > 0;

			// Switch State for AvA
			if (Mode == GameMode.AvA && !preparing) {
				SwitchState(GameState.Playing);
			}

		}


		private void Update_Playing () {

			bool inter = true;
			foreach (Transform tf in m_Assets.DialogRoot) {
				if (tf.gameObject.activeSelf) {
					inter = false;
					break;
				}
			}
			bool PvA = Mode == GameMode.PvA;
			bool aTurn = CurrentTurn == Turn.A;
			bool waitingForPlayer = !GameOver && PvA && aTurn && !CellStep.HasStep;

			FieldA.ShowShips = true;
			FieldA.AllowHoveringOnShip = inter && PvA && waitingForPlayer;
			FieldA.AllowHoveringOnWater = inter && PvA && waitingForPlayer;
			FieldA.HideInvisibleShip = false;
			FieldA.ClickToAttack = false;
			FieldA.ClickShipToTriggerAbility = waitingForPlayer;

			FieldB.ShowShips = true;
			FieldB.AllowHoveringOnShip = false;
			FieldB.AllowHoveringOnWater = inter && PvA && waitingForPlayer;
			FieldB.HideInvisibleShip = PvA && !Cheating;
			FieldB.ClickToAttack = waitingForPlayer;
			FieldB.ClickShipToTriggerAbility = false;

			// AI
			if (aTurn) {
				if (!PvA) {
					// Robot A



				}
			} else {
				// Robot B

				/////////// TEST //////////////
				SwitchTurn();
				/////////// TEST //////////////

			}

			// Cheat
			bool showCheat = !GameOver && PvA;
			if (Cheating) Cheated = true;
			if (m_Assets.CheatTG.isOn != Cheating) m_Assets.CheatTG.SetIsOnWithoutNotify(Cheating);
			if (m_Assets.CheatTG.gameObject.activeSelf != showCheat) m_Assets.CheatTG.gameObject.SetActive(showCheat);

			// Gameover Check
			if (!GameOver) {
				bool aSunk = FieldA.AllShipsSunk();
				bool bSunk = FieldB.AllShipsSunk();
				if (aSunk || bSunk) {
					CellStep.Clear();
					GameOver = true;
					if (PvA) {
						(aSunk ?
							Cheated ? m_Assets.Dialog_LoseCheat : m_Assets.Dialog_Lose :
							Cheated ? m_Assets.Dialog_WinCheat : m_Assets.Dialog_Win
						).gameObject.SetActive(true);
					}
				}
			}

		}


		#endregion




		#region --- API ---


		public void SwitchTurn () {
			var field = CurrentTurn == Turn.A ? FieldA : FieldB;
			foreach (var ship in field.Ships) {
				ship.CurrentCooldown--;
			}
			CurrentTurn = CurrentTurn.Opponent();
			RefreshInteractableForShipAbilityUI(m_Assets.AbilityContainerA, FieldA, Mode == GameMode.PvA);
			RefreshInteractableForShipAbilityUI(m_Assets.AbilityContainerB, FieldB, false);
		}


		public bool UseAbility (Ship ship) {
			if (ship.CurrentCooldown > 0) return false;
			if (!AbilityPool.TryGetValue(ship.GlobalCode, out var ability)) return false;
			ability.Perform(
				ship.CurrentCooldown == 0 ? EntranceType.OnAbilityUsed : EntranceType.OnAbilityUsedWithOverCooldown,
				ActionResult.None
			);
			ship.CurrentCooldown = ship.MaxCooldown;
			RefreshInteractableForShipAbilityUI(m_Assets.AbilityContainerA, FieldA, Mode == GameMode.PvA);
			RefreshInteractableForShipAbilityUI(m_Assets.AbilityContainerB, FieldB, false);
			return true;
		}


		// UI
		public void UI_OpenURL (string url) => Application.OpenURL(url);


		public void UI_SwitchState (string state) {
			if (System.Enum.TryParse<GameState>(state, true, out var result)) {
				SwitchState(result);
			}
		}


		public void UI_SwitchMode (string mode) {
			if (System.Enum.TryParse<GameMode>(mode, true, out var result)) {
				SwitchMode(result);
			}
		}


		public void UI_RefreshSettingUI () {
			m_Assets.SoundTG.SetIsOnWithoutNotify(s_UseSound.Value);
			m_Assets.AutoPlayAvATG.SetIsOnWithoutNotify(s_AutoPlayForAvA.Value);
			m_Assets.UseAnimationTG.SetIsOnWithoutNotify(s_UseAnimation.Value);
		}


		public void UI_OpenReloadDialog () {
			ReloadShipDataFromDisk();
			ReloadMapDataFromDisk();
		}


		public void UI_SelectingMapChanged () {
			s_MapIndexA.Value = GetMapIndexFromUI(m_Assets.MapSelectorContentA).Clamp(0, AllMaps.Count);
			s_MapIndexB.Value = GetMapIndexFromUI(m_Assets.MapSelectorContentB).Clamp(0, AllMaps.Count);
			OnMapChanged();
			// Func
			int GetMapIndexFromUI (RectTransform container) {
				int childCount = container.childCount;
				for (int i = 0; i < childCount; i++) {
					var tg = container.GetChild(i).GetComponent<Toggle>();
					if (tg != null && tg.isOn) return i;
				}
				return 0;
			}
		}


		public void UI_SelectingFleetChanged () {
			ReloadFleetRendererUI();
			OnFleetChanged();
		}


		public void UI_ClearPlayerFleetSelector () {
			s_PlayerFleet.Value = "";
			ReloadFleetRendererUI();
			OnFleetChanged();
		}


		public void UI_ResetPlayerShipPositions () => FieldA.RandomPlaceShips(256);


		public void UI_AiSelectorChanged () {
			s_SelectingAiA.Value = m_Assets.RobotAiA.value.Clamp(0, AllAi.Count - 1);
			s_SelectingAiB.Value = m_Assets.RobotAiB.value.Clamp(0, AllAi.Count - 1);
			OnFleetChanged();
			ReloadFleetRendererUI();
		}


		public void UI_TryQuitBattle () {
			if (State != GameState.Playing) return;
			if (GameOver) {
				SwitchState(GameState.Prepare);
			} else {
				m_Assets.QuitBattleDialog.gameObject.SetActive(true);
			}
		}


		#endregion




		#region --- LGC ---


		private void SwitchState (GameState state) {

			// Check Valid before Play
			if (state == GameState.Playing) {
				if (!FieldA.IsValidForPlay(out _)) {
					m_Assets.FailPlacingShipsDialog.gameObject.SetActive(true);
					return;
				}
			}

			State = state;
			int count = m_Assets.PanelRoot.childCount;
			for (int i = 0; i < count; i++) {
				m_Assets.PanelRoot.GetChild(i).gameObject.SetActive(i == (int)state);
			}

			CellStep.Clear();
			Cheating = false;
			Cheated = false;
			GameOver = false;
			FieldA.ClickToAttack = false;
			FieldA.ClickShipToTriggerAbility = false;
			FieldB.ClickToAttack = false;
			FieldB.ClickShipToTriggerAbility = false;

			switch (state) {
				case GameState.Title:
					break;
				case GameState.Prepare:

					m_Assets.PreparePanel.gameObject.SetActive(true);
					m_Assets.PlacePanel.gameObject.SetActive(false);

					FieldA.AllowHoveringOnShip = true;
					FieldA.AllowHoveringOnWater = false;
					FieldA.HideInvisibleShip = false;

					FieldB.Enable = false;
					FieldB.AllowHoveringOnShip = false;
					FieldB.AllowHoveringOnWater = false;
					FieldB.HideInvisibleShip = false;
					FieldB.ShowShips = false;
					FieldB.DragToMoveShips = false;

					ReloadFleetRendererUI();
					break;

				case GameState.Playing:

					if (Random.value > 0.5f) CurrentTurn = CurrentTurn.Opponent();
					s_MapIndexA.Value = s_MapIndexA.Value.Clamp(0, AllMaps.Count - 1);
					s_MapIndexB.Value = s_MapIndexB.Value.Clamp(0, AllMaps.Count - 1);
					bool PvA = Mode == GameMode.PvA;

					// A
					var shiftA = new Vector2Int(0, AllMaps[s_MapIndexB.Value].Size + 2);
					FieldA.Enable = true;
					FieldA.ShowShips = false;
					FieldA.DragToMoveShips = false;

					if (!PvA) {
						bool successA = FieldA.RandomPlaceShips(256);
						if (!successA) {
							m_Assets.RobotFailedToPlaceShipsDialog.gameObject.SetActive(true);
							SwitchState(GameState.Prepare);
						}
					}
					FieldA.LocalShift = shiftA;

					// B
					FieldB.Enable = true;
					FieldB.ShowShips = false;
					FieldB.DragToMoveShips = false;
					bool success = FieldB.RandomPlaceShips(256);
					if (!success) {
						m_Assets.RobotFailedToPlaceShipsDialog.gameObject.SetActive(true);
						SwitchState(GameState.Prepare);
					}

					OnFleetChanged();
					FieldA.Restart();
					FieldB.Restart();
					ReloadShipAbilityUI(FieldA, m_Assets.AbilityContainerA, m_Assets.ShipAbilityItem, PvA);
					ReloadShipAbilityUI(FieldB, m_Assets.AbilityContainerB, m_Assets.ShipAbilityItem, false);

					break;

				case GameState.CardGame:
					break;

				case GameState.ShipEditor:
					break;

			}
		}


		private void SwitchMode (GameMode mode) {
			Mode = mode;
			switch (mode) {
				case GameMode.PvA:
					m_Assets.MapSelectorLabelA.text = "Your Map";
					m_Assets.MapSelectorLabelB.text = "Opponent Map";
					m_Assets.FleetSelectorLabelA.text = "Your Fleet";
					m_Assets.FleetSelectorLabelB.text = "Opponent Fleet";
					m_Assets.FleetSelectorPlayer.gameObject.SetActive(true);
					m_Assets.FleetSelectorRobotA.gameObject.SetActive(false);
					ReloadFleetRendererUI();
					OnFleetChanged();
					break;
				case GameMode.AvA:
					m_Assets.MapSelectorLabelA.text = "Robot A Map";
					m_Assets.MapSelectorLabelB.text = "Robot B Map";
					m_Assets.FleetSelectorLabelA.text = "Robot A Fleet";
					m_Assets.FleetSelectorLabelB.text = "Robot B Fleet";
					m_Assets.FleetSelectorPlayer.gameObject.SetActive(false);
					m_Assets.FleetSelectorRobotA.gameObject.SetActive(true);
					ReloadFleetRendererUI();
					OnFleetChanged();
					break;
			}
		}


		private void RefreshCameraView (bool immediately = false) {

			bool availableA = FieldA.Enable;
			bool availableB = FieldB.Enable;

			if (!availableA && !availableB) return;

			const int MIN_WIDTH = SoupConst.ISO_SIZE * 10;
			const int MIN_HEIGHT = SoupConst.ISO_SIZE * 10;

			int sizeA = availableA ? FieldA.MapSize : 0;
			int sizeB = availableB ? FieldB.MapSize : 0;

			int l0 = int.MaxValue;
			int r0 = int.MinValue;
			int d0 = int.MaxValue;
			int u0 = int.MinValue;
			if (availableA) {
				(l0, _) = FieldA.Local_to_Global(0, sizeA - 1);
				(r0, _) = FieldA.Local_to_Global(sizeA, 0);
				(_, d0) = FieldA.Local_to_Global(0, 0);
				(_, u0) = FieldA.Local_to_Global(sizeA, sizeA, 1);
			}

			int l1 = int.MaxValue;
			int r1 = int.MinValue;
			int d1 = int.MaxValue;
			int u1 = int.MinValue;
			if (availableB) {
				(l1, _) = FieldB.Local_to_Global(0, sizeB - 1);
				(r1, _) = FieldB.Local_to_Global(sizeB, 0);
				(_, d1) = FieldB.Local_to_Global(0, 0);
				(_, u1) = FieldB.Local_to_Global(sizeB, sizeB, 1);
			}

			int minX = Mathf.Min(l0, l1);
			int maxX = Mathf.Max(r0, r1) + SoupConst.ISO_SIZE / 2;
			int minY = Mathf.Min(d0, d1);
			int maxY = Mathf.Max(u0, u1);

			var rect = new RectInt(minX, minY, maxX - minX, maxY - minY);
			rect = rect.Expand(
				SoupConst.ISO_SIZE / 2, SoupConst.ISO_SIZE / 2,
				SoupConst.ISO_SIZE / 2, SoupConst.ISO_SIZE
			);
			if (rect.width < MIN_WIDTH) {
				int exp = (MIN_WIDTH - rect.width) / 2;
				rect = rect.Expand(exp, exp, 0, 0);
			}
			if (rect.height < MIN_HEIGHT) {
				int exp = (MIN_HEIGHT - rect.height) / 2;
				rect = rect.Expand(0, 0, exp, exp);
			}
			var targetCameraRect = rect.ToRect().Envelope((float)Screen.width / Screen.height);

			SetViewPositionDely(
				(int)(targetCameraRect.x + targetCameraRect.width / 2) - ViewRect.width / 2,
				(int)targetCameraRect.y, immediately ? 1000 : 220
			);
			SetViewSizeDely((int)targetCameraRect.height, immediately ? 1000 : 220);
		}


		private void OnMapChanged () {
			FieldA.SetMap(AllMaps[s_MapIndexA.Value]);
			FieldB.SetMap(AllMaps[s_MapIndexB.Value]);
		}


		private void OnFleetChanged () {
			FieldA.SetShips(
				GetShipsFromFleetString(Mode == GameMode.PvA ? s_PlayerFleet.Value : GetBotFleetA())
			);
			FieldA.RandomPlaceShips(256);
			FieldB.SetShips(
				GetShipsFromFleetString(GetBotFleetB())
			);
			FieldB.RandomPlaceShips(256);
		}


		private Ship[] GetShipsFromFleetString (string fleet) {
			var ships = new List<Ship>();
			var shipNames = fleet.Split(',');
			foreach (var name in shipNames) {
				if (ShipPool.TryGetValue(name.AngeHash(), out var ship)) {
					ships.Add(ship.CreateDataCopy());
				}
			}
			return ships.ToArray();
		}


		// Refresh UI
		private void RemoveShipFromFleetString (SavingString sString, int index) {
			var ships = new List<string>(sString.Value.Split(','));
			if (index >= 0 && index < ships.Count) {
				ships.RemoveAt(index);
				sString.Value = ships.Count > 0 ? string.Join(',', ships) : "";
			}
		}


		private void ReloadFleetRendererUI () {
			ReloadFleet(
				Mode == GameMode.PvA ? s_PlayerFleet : null,
				Mode == GameMode.PvA ? s_PlayerFleet.Value : GetBotFleetA(),
				m_Assets.FleetRendererA
			);
			ReloadFleet(
				null,
				GetBotFleetB(),
				m_Assets.FleetRendererB
			);
			// Func
			void ReloadFleet (SavingString fleet, string fleetStr, RectTransform container) {
				container.DestroyAllChirldrenImmediate();
				var ships = fleetStr.Split(',');
				var hori = container.GetComponent<HorizontalLayoutGroup>();
				float plusWidth = 24;
				float itemWidth = (container.rect.width - hori.padding.horizontal) / ships.Length - hori.spacing - plusWidth;
				itemWidth = itemWidth.Clamp(0, 64);
				for (int i = 0; i < ships.Length; i++) {
					int shipIndex = i;
					string shipName = ships[shipIndex];
					if (!ShipPool.TryGetValue(shipName.AngeHash(), out var ship) || ship.Icon == null) continue;
					// Spawn Item
					var grab = Instantiate(m_Assets.FleetRendererItem, container);
					grab.gameObject.SetActive(true);
					var rt = grab.transform as RectTransform;
					rt.SetAsLastSibling();
					rt.anchoredPosition3D = rt.anchoredPosition;
					rt.localRotation = Quaternion.identity;
					rt.localScale = Vector3.one;
					rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, itemWidth);
					var img = grab.Grab<Image>();
					var btn = grab.Grab<Button>();
					img.sprite = ship.Icon;
					btn.interactable = fleet != null;
					if (fleet != null) {
						btn.onClick.AddListener(() => {
							RemoveShipFromFleetString(fleet, shipIndex);
							ReloadFleetRendererUI();
							OnFleetChanged();
						});
					}
					// Spawn Plus
					if (shipIndex < ships.Length - 1) {
						var plusG = new GameObject("Plus", typeof(RectTransform), typeof(Image));
						var pRt = plusG.transform as RectTransform;
						pRt.SetParent(container);
						pRt.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
						pRt.localScale = Vector3.one;
						pRt.SetAsLastSibling();
						pRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, plusWidth);
						var pImg = plusG.GetComponent<Image>();
						pImg.sprite = m_Assets.PlusSprite;
						pImg.preserveAspect = true;
					}
				}
			}
		}


		private void ReloadShipAbilityUI (eField field, RectTransform container, Grabber shipItem, bool interactable) {
			container.DestroyAllChirldrenImmediate();
			for (int i = 0; i < field.Ships.Length; i++) {
				var ship = field.Ships[i];
				var grab = Instantiate(shipItem, container);
				grab.gameObject.SetActive(true);
				var rt = grab.transform as RectTransform;
				rt.SetAsLastSibling();
				rt.anchoredPosition3D = rt.anchoredPosition;
				rt.localRotation = Quaternion.identity;
				rt.localScale = Vector3.one;
				var btn = grab.Grab<Button>();
				btn.interactable = interactable;
				if (interactable) {
					btn.onClick.AddListener(() => {


					});
				}
				var icon = grab.Grab<Image>("Icon");
				icon.sprite = ship.Icon;
				var label = grab.Grab<Text>("Label");
				label.text = ship.DisplayName;
				var tooltip = grab.Grab<TooltipUI>();
				tooltip.Tooltip = ship.Discription;
				var cooldown = grab.Grab<Text>("Cooldown");
				cooldown.text = ship.DefaultCooldown.ToString();
				var shape = grab.Grab<ShipShapeUI>();
				shape.Ship = ship;
			}
			RefreshInteractableForShipAbilityUI(container, field, interactable);
		}


		private void RefreshInteractableForShipAbilityUI (RectTransform container, eField field, bool interactable) {
			int count = container.childCount;
			for (int i = 0; i < count && i < field.Ships.Length; i++) {
				var ship = field.Ships[i];
				var grab = container.GetChild(i).GetComponent<Grabber>();
				if (grab == null) continue;
				var btn = grab.Grab<Button>();
				btn.interactable = interactable && !GameOver && ship.CurrentCooldown <= 0;
				var cooldown = grab.Grab<Text>("Cooldown");
				cooldown.text = !GameOver && ship.CurrentCooldown > 0 ? ship.CurrentCooldown.ToString() : "";
			}
		}


		// Load Data
		private void ReloadShipDataFromDisk () {

			// Ship Pool
			try {
				ShipPool.Clear();
				foreach (var folder in Util.GetFoldersIn(ShipRoot, true)) {
					try {
						int globalID = 0;
						Ship ship = null;
						// Info
						string infoPath = Util.CombinePaths(folder.FullName, "Info.json");
						if (Util.FileExists(infoPath)) {
							ship = JsonUtility.FromJson<Ship>(Util.FileToText(infoPath));
							if (ship == null) continue;
							globalID = ship.GlobalCode;
							ShipPool.TryAdd(ship.GlobalCode, ship);
						} else continue;
						// Icon
						if (ship != null) {
							string iconPath = Util.CombinePaths(folder.FullName, "Icon.png");
							if (Util.FileExists(iconPath)) {
								var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false) {
									filterMode = FilterMode.Bilinear,
									anisoLevel = 0,
									wrapMode = TextureWrapMode.Clamp,
								};
								texture.LoadImage(Util.FileToByte(iconPath));
								ship.Icon = Sprite.Create(
									texture,
									new Rect(0, 0, texture.width, texture.height),
									Vector2.one * 0.5f
								);
							}
							if (ship.Icon == null) ship.Icon = m_Assets.DefaultShipIcon;
						}
						// Ability
						string abPath = Util.CombinePaths(folder.FullName, "Ability.txt");
						if (Util.FileExists(abPath)) {
							string code = Util.FileToText(abPath);
							var exe = AbilityCompiler.Compile(code, out string error);
							if (exe != null) {
								AbilityPool.TryAdd(globalID, exe);
							} else {
								Debug.LogError(folder.FullName + "\n" + error);
							}
						}
					} catch (System.Exception ex) { Debug.LogWarning(folder.Name); Debug.LogException(ex); }
				}
			} catch (System.Exception ex) { Debug.LogException(ex); }

			// Ship UI
			m_Assets.FleetSelectorPlayerContent.DestroyAllChirldrenImmediate();
			foreach (var (_, ship) in ShipPool) {
				var grab = Instantiate(m_Assets.FleetSelectorShipItem, m_Assets.FleetSelectorPlayerContent);
				var rt = grab.transform as RectTransform;
				var img = grab.Grab<Image>("Icon");
				img.sprite = ship.Icon != null ? ship.Icon : m_Assets.DefaultShipIcon;
				var label = grab.Grab<Text>("Label");
				label.text = ship.DisplayName;
				var btn = grab.Grab<Button>();
				btn.onClick.AddListener(() => {
					if (string.IsNullOrEmpty(s_PlayerFleet.Value) || s_PlayerFleet.Value.EndsWith(',')) {
						s_PlayerFleet.Value += $"{ship.GlobalName}";
					} else {
						s_PlayerFleet.Value += $",{ship.GlobalName}";
					}
					ReloadFleetRendererUI();
					OnFleetChanged();
				});
				var tooltip = grab.Grab<TooltipUI>();
				tooltip.Tooltip = ship.Discription;
				var shape = grab.Grab<ShipShapeUI>();
				shape.Ship = ship;
				var cooldown = grab.Grab<Text>("Cooldown");
				cooldown.text = ship.MaxCooldown.ToString();
			}
		}


		private void ReloadMapDataFromDisk () {

			// Load Data from Disk
			try {
				AllMaps.Clear();
				foreach (var file in Util.GetFilesIn(MapRoot, false, "*.png")) {
					try {
						var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false) {
							filterMode = FilterMode.Point,
							anisoLevel = 0,
							wrapMode = TextureWrapMode.Clamp,
						};
						texture.LoadImage(Util.FileToByte(file.FullName));
						if (texture.width != texture.height) {
							Debug.LogWarning($"Map texture \"{file.Name}\" have differect width and height.");
							continue;
						}
						if (texture.width == 0 || texture.height == 0) continue;
						var pixels = texture.GetPixels32();
						var map = new Map() {
							Size = texture.width,
							Content = new int[pixels.Length],
						};
						for (int i = 0; i < pixels.Length; i++) {
							map.Content[i] = pixels[i].r < 128 ? 1 : 0;
						}
						AllMaps.Add(map);
					} catch (System.Exception ex) { Debug.LogException(ex); }
				}
				AllMaps.Sort((a, b) => a.Size.CompareTo(b.Size));
			} catch (System.Exception ex) { Debug.LogException(ex); }

			s_MapIndexA.Value = s_MapIndexA.Value.Clamp(0, AllMaps.Count - 1);
			s_MapIndexB.Value = s_MapIndexB.Value.Clamp(0, AllMaps.Count - 1);

			// Reload UI
			ReloadMapUI(m_Assets.MapSelectorItem, m_Assets.MapSelectorContentA, s_MapIndexA.Value);
			ReloadMapUI(m_Assets.MapSelectorItem, m_Assets.MapSelectorContentB, s_MapIndexB.Value);

			// Func
			void ReloadMapUI (Grabber itemSource, RectTransform content, int selectingIndex) {
				content.DestroyAllChirldrenImmediate();
				var group = content.GetComponent<ToggleGroup>();
				foreach (var map in AllMaps) {
					var item = Instantiate(itemSource, content);
					item.gameObject.SetActive(true);
					item.transform.SetAsLastSibling();
					var mapRenderer = item.Grab<MapRendererUI>();
					var tg = item.Grab<Toggle>();
					var label = item.Grab<Text>();
					mapRenderer.Map = map;
					tg.SetIsOnWithoutNotify(item.transform.GetSiblingIndex() == selectingIndex);
					tg.group = group;
					label.text = $"{map.Size}��{map.Size}";
				}
			}

		}


		private string GetBotFleetA () => AllAi[s_SelectingAiA.Value.Clamp(0, AllAi.Count - 1)].Fleet;


		private string GetBotFleetB () => AllAi[s_SelectingAiB.Value.Clamp(0, AllAi.Count - 1)].Fleet;


		#endregion




	}
}