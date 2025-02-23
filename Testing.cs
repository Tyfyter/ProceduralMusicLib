/*
using Microsoft.Xna.Framework.Input;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;

namespace ProceduralMusicLib {
	public class TestPlayer : ModPlayer {
		public override void Load() {
			Mod.Call(ProceduralMusicLib.CallType.ReplaceMusic, "ProceduralMusicLib/TestJSONTrack", MusicID.Crimson);
			//keybind = KeybindLoader.RegisterKeybind(Mod, "TheKeybind", Keys.NumPad1);
		}
		static ModKeybind keybind;
		public override void ProcessTriggers(TriggersSet triggersSet) {
			if (keybind is not null && keybind.JustPressed && Main.audioSystem is LegacyAudioSystem audioSystem) {
				//audioSystem.AudioTracks[MusicID.Crimson] = audioSystem.DefaultTrackByIndex[MusicID.Crimson] = testTrack = new JSONAudioTrack("ProceduralMusicLib/TestJSONTrack");
			}
			if (Main.mouseY > 0.5 * Main.screenHeight) {
				Mod.Call(ProceduralMusicLib.CallType.SetTrigger, "ProceduralMusicLib/TestJSONTrack", 1);
			} else {
				Mod.Call(ProceduralMusicLib.CallType.SetTrigger, "ProceduralMusicLib/TestJSONTrack", 1, false);
			}
		}
	}
}
//*/