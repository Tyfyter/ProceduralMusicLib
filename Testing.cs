#if DEBUG
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace ProceduralMusicLib {
	public class TestPlayer : ModPlayer {
		public static ChanneledAudioTrack testTrack;
		public override void Load() {
			if (Main.audioSystem is LegacyAudioSystem audioSystem) {
				audioSystem.AudioTracks[MusicID.AltOverworldDay] = audioSystem.DefaultTrackByIndex[MusicID.AltOverworldDay] = testTrack = new JSONAudioTrack("ProceduralMusicLib/TestJSONTrack");
			}
			keybind = KeybindLoader.RegisterKeybind(Mod, "TheKeybind", Keys.NumPad1);
		}
		static ModKeybind keybind;
		public override void ProcessTriggers(TriggersSet triggersSet) {
			if (keybind is not null && keybind.JustPressed && Main.audioSystem is LegacyAudioSystem audioSystem) {
				audioSystem.AudioTracks[MusicID.AltOverworldDay] = audioSystem.DefaultTrackByIndex[MusicID.AltOverworldDay] = testTrack = new JSONAudioTrack("ProceduralMusicLib/TestJSONTrack");
			}
		}
	}
	public class ProceduralMusicLibTestSystem : ModSystem {
		internal static byte[] _bufferToSubmit;
		int offset = 0;
		/*public override void PostDrawInterface(SpriteBatch spriteBatch) {
			if (_bufferToSubmit is null) return;
			float xPosition = 0;
			int yPosition = 512;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle frame = new Rectangle(0, 0, 1, 1);
			float prog = Main.screenWidth / (float)(_bufferToSubmit.Length / 2);
			Color color = Color.Red;
			if (Main.mouseY > 0.5 * Main.screenHeight) {
				color = Color.Blue;
				TestPlayer.testTrack.Trigger(1);
			} else {
				TestPlayer.testTrack.UnTrigger(1);
			}
			for (int i = 0; i < _bufferToSubmit.Length - 2; i += 2) {
				Vector2 pos = new Vector2(xPosition, yPosition - _bufferToSubmit[i]);
				Vector2 diff = new Vector2(xPosition + prog, yPosition - _bufferToSubmit[i + 2]) - pos;
				spriteBatch.Draw(pixel, pos, frame, color, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), 1), 0, 0);
				xPosition += prog;
			}
			xPosition = 0;
			yPosition = 512 + 300;
			for (int i = 1; i < _bufferToSubmit.Length - 2; i += 2) {
				Vector2 pos = new Vector2(xPosition, yPosition - _bufferToSubmit[i]);
				Vector2 diff = new Vector2(xPosition + prog, yPosition - _bufferToSubmit[i + 2]) - pos;
				spriteBatch.Draw(pixel, pos, frame, color, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), 1), 0, 0);
				xPosition += prog;
			}
		}*/
	}
}

#endif