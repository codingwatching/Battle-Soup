using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AngeliaFramework;


namespace BattleSoup {
	public class sExpose : sSoupStep {



		private static readonly int FRAME_CODE = "Expose Frame".AngeHash();


		// MSG
		public sExpose (int x, int y, eField field, Ship ship, bool fast = false) : base(x, y, field, ship, fast) { }


		public override StepResult FrameUpdate (Game game) {
			base.FrameUpdate(game);
			if (X < 0 || Y < 0 || X >= Field.MapSize || Y >= Field.MapSize) return StepResult.Over;
			int DURATION = Fast ? 6 : 24;
			var (_x, _y) = Field.Local_to_Global(X, Y, 1);
			if (LocalFrame == 0) {
				var result = Field.Expose(X, Y);
				eTag.SpawnTag(_x, _y, result);
			}
			if (UseAnimation && LocalFrame < DURATION) {
				if (LocalFrame % 4 < 2) {
					float t01 = (float)LocalFrame / DURATION;
					var (x, y) = Field.Local_to_Global(X, Y, 0);
					CellRenderer.Draw(
						FRAME_CODE,
						x, y + (int)(t01 * SoupConst.ISO_SIZE / 2),
						SoupConst.ISO_SIZE, SoupConst.ISO_SIZE,
						new Color32(255, 255, 255, (byte)Util.Remap(0f, 1f, 255, 0, t01))
					);
				}
				return StepResult.Continue;
			}
			return StepResult.Over;
		}


	}
}