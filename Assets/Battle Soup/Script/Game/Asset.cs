using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Moenen.Standard;
using BattleSoupAI;


namespace BattleSoup {
	public class Asset : MonoBehaviour {




		#region --- VAR ---


		// Api
		public Dictionary<string, ShipData> ShipMap { get; } = new Dictionary<string, ShipData>();
		public List<MapData> MapDatas { get; } = new List<MapData>();

		// Ser
		[SerializeField] Material m_ShipMaterial = null;
		[SerializeField] Texture2D m_ShipBlockTexture = null;


		#endregion




		#region --- MSG ---


		private void Awake () {
			Awake_Ship();
			Awake_Map();
		}


		private void Awake_Ship () {
			ShipMap.Clear();
			var iconList = new List<Texture2D>() { m_ShipBlockTexture };
			string root = Util.CombinePaths(Util.GetRuntimeBuiltRootPath(), "Ships");
			var folders = Util.GetFoldersIn(root, true);
			var shipList = new List<ShipData>();
			for (int i = 0; i < folders.Length; i++) {
				try {
					var folder = folders[i];
					string key = folder.Name;
					if (ShipMap.ContainsKey(key)) { continue; }
					string json = Util.FileToText(Util.CombinePaths(folder.FullName, "Data.json"));
					if (string.IsNullOrEmpty(json)) { continue; }
					var shipData = JsonUtility.FromJson<ShipData>(json);
					if (shipData == null) { continue; }
					var iconByte = Util.FileToByte(Util.CombinePaths(folder.FullName, "Icon.png"));
					if (iconByte == null || iconByte.Length == 0) { continue; }
					var iconTexture = new Texture2D(1, 1);
					if (!iconTexture.LoadImage(iconByte, false)) { continue; }
					shipData.GlobalID = key;
					iconList.Add(iconTexture);
					ShipMap.Add(key, shipData);
					shipList.Add(shipData);
				} catch { }
			}
			// Pack Texture
			var shipTexture = new Texture2D(1, 1);
			var uvRects = shipTexture.PackTextures(iconList.ToArray(), 1);
			float tWidth = shipTexture.width;
			float tHeight = shipTexture.height;
			var sprites = new Sprite[ShipMap.Count + 1];
			var uv0 = uvRects[0];
			sprites[0] = Sprite.Create(shipTexture, new Rect(uv0.x * tWidth, uv0.y * tHeight, uv0.width * tWidth, uv0.height * tHeight), Vector2.one * 0.5f);
			sprites[0].name = "(ship block)";
			for (int i = 0; i < shipList.Count; i++) {
				var ship = shipList[i];
				var uv = uvRects[i + 1];
				var rect = new Rect(uv.x * tWidth, uv.y * tHeight, uv.width * tWidth, uv.height * tHeight);
				ship.SpriteIndex = i;
				ship.Sprite = Sprite.Create(shipTexture, rect, Vector2.one * 0.5f);
				ship.Sprite.name = ship.DisplayName;
				sprites[i + 1] = ship.Sprite;
			}
			m_ShipMaterial.mainTexture = shipTexture;
			// Sprites for Renderers
			var sRenderers = FindObjectsOfType<ShipRenderer>(true);
			foreach (var renderer in sRenderers) {
				renderer.SetSprites(sprites);
			}
		}


		private void Awake_Map () {
			MapDatas.Clear();
			string root = Util.CombinePaths(Util.GetRuntimeBuiltRootPath(), "Maps");
			var files = Util.GetFilesIn(root, true, "*.png");
			var stones = new List<Int2>();
			for (int i = 0; i < files.Length; i++) {
				try {
					var file = files[i];
					var png = Util.FileToByte(file.FullName);
					if (png == null || png.Length == 0) { continue; }
					var texture = new Texture2D(2, 2);
					texture.LoadImage(png, false);
					int textureWidth = texture.width;
					int textureHeight = texture.height;
					stones.Clear();
					for (int x = 0; x < textureWidth; x++) {
						for (int y = 0; y < textureHeight; y++) {
							if (texture.GetPixel(x, y).r < 0.5f) {
								stones.Add(new Int2(x, y));
							}
						}
					}
					MapDatas.Add(new MapData(Mathf.Max(textureWidth, textureHeight), stones.ToArray()));
				} catch { }
			}
			Resources.UnloadUnusedAssets();
		}


		#endregion




		#region --- API ---


		public ShipData GetShipData (string globalID) {
			if (ShipMap.ContainsKey(globalID)) {
				return ShipMap[globalID];
			}
			return null;
		}


		#endregion




	}
}