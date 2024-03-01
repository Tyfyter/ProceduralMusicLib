using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ProceduralMusicLib
{
	public class ProceduralMusicLib : Mod {
		public override void Load() {
			if (Main.audioSystem is LegacyAudioSystem audioSystem) {
				audioSystem.AudioTracks[MusicID.Crimson] = audioSystem.DefaultTrackByIndex[MusicID.Crimson] = new ProceduralAudioTrack(BuildSquareSequence(
					(200, 12000),
					(300, 12000),
					(200, 6000),
					(500, 24000)
				));
				/*(position) => {
					if (position < 24000 * 16) {
						return (byte)(((position / 300f) % 1) * 64);
					}
					if (position < 24000 * 32) {
						double progress = position / (24000.0 * 16) - 1;
						return (byte)((((position / 300f) % 1) * 64) * (1 - progress) + Math.Sin(position / 30f + 1) * 64 * progress);
					}
					return (byte)((Math.Sin(position / 30f) + 1) * 64);
				}*/
			}
		}
		public static Func<int, byte> BuildSquareSequence(params (int freq, int dur)[] notes) {
			int totalLength = 0;
			List<Func<int, byte>> funcs = new();
			for (int i = 0; i < notes.Length; i++) {
				int currentLength = totalLength;
				int dur = notes[i].dur;
				int freq = notes[i].freq;
				funcs.Add((position) => {
					if (position < currentLength || position >= currentLength + dur) return 0;
					return (byte)(((position / (24000.0 / freq)) % 1) > 0.5 ? 0 : 64);
				});
				totalLength += dur;
			}
			return (position) => {
				position %= totalLength;
				int total = 0;
				for (int i = 0; i < funcs.Count; i++) {
					total += funcs[i](position);
				}
				return (byte)total;
			};
		}
	}
	public class ProceduralMusicLibSystem : ModSystem {
		internal static byte[] _bufferToSubmit;
		int offset = 0;
		public override void PostDrawInterface(SpriteBatch spriteBatch) {
			if (_bufferToSubmit is null) return;
			float xPosition = 0;
			int yPosition = 512;
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle frame = new Rectangle(0, 0, 1, 1);
			float prog = Main.screenWidth / (float)(_bufferToSubmit.Length / 2);
			for (int i = 0; i < _bufferToSubmit.Length - 2; i += 2) {
				Vector2 pos = new Vector2(xPosition, yPosition - _bufferToSubmit[i]);
				Vector2 diff = new Vector2(xPosition + prog, yPosition - _bufferToSubmit[i + 2]) - pos;
				spriteBatch.Draw(pixel, pos, frame, Color.Red, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), 1), 0, 0);
				xPosition += prog;
			}
			xPosition = 0;
			yPosition = 512 + 300;
			for (int i = 1; i < _bufferToSubmit.Length - 2; i += 2) {
				Vector2 pos = new Vector2(xPosition, yPosition - _bufferToSubmit[i]);
				Vector2 diff = new Vector2(xPosition + prog, yPosition - _bufferToSubmit[i + 2]) - pos;
				spriteBatch.Draw(pixel, pos, frame, Color.Blue, diff.ToRotation(), Vector2.Zero, new Vector2(diff.Length(), 1), 0, 0);
				xPosition += prog;
			}
		}
	}
	public class ProceduralAudioTrack : ASoundEffectBasedAudioTrack {
		int position = 0;
		readonly Func<int, byte> procedure;
		public ProceduralAudioTrack(Func<int, byte> procedure) {
			this.procedure = procedure;
			CreateSoundEffect(12000, AudioChannels.Stereo);
		}
		public override void Dispose() {

		}

		public override void Reuse() {
			position = 0;
		}
		protected override void ReadAheadPutAChunkIntoTheBuffer() {
			ProceduralMusicLibSystem._bufferToSubmit = _bufferToSubmit;
			for (int i = 0; i < _bufferToSubmit.Length; i++) {
				_bufferToSubmit[i] = procedure(++position);
			}
			if (false) {
				Stop(AudioStopOptions.Immediate);
			} else {
				_soundEffectInstance.SubmitBuffer(_bufferToSubmit);
			}
		}
	}
}