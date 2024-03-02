using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static ProceduralMusicLib.AudioBuilder;

namespace ProceduralMusicLib {
	public class ProceduralMusicLib : Mod {
		public static ChanneledAudioTrack testTrack;
		public override void Load() {
			if (Main.audioSystem is LegacyAudioSystem audioSystem) {
				AudioChannel channel0 = new AudioChannel(true,
					AudioChannel.Keyframe.Audio(BuildSquareWave(200, 6000)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(300, 6000)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(200, 3000)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(500, 12000)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(700, 12000)),
					default
				);
				AudioChannel channel1 = new AudioChannel(true,
					AudioChannel.Keyframe.Audio(BuildSquareWave(200, 6000, temperment: 0.1f)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(300, 6000, temperment: 0.1f)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(200, 3000, temperment: 0.1f)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(500, 12000, temperment: 0.1f)),
					AudioChannel.Keyframe.Audio(BuildSquareWave(700, 12000, temperment: 0.1f)),
					AudioChannel.Keyframe.Audio(ExtractWav("Origins/Sounds/Custom/ShrapnelFest.wav")),
					default
				);
				channel0.keyframes[^1] = AudioChannel.Keyframe.Switch(1, channel1);
				channel1.keyframes[^1] = AudioChannel.Keyframe.Switch(-1, channel0);
				testTrack = new ChanneledAudioTrack(channel0);
				audioSystem.AudioTracks[MusicID.Crimson] = audioSystem.DefaultTrackByIndex[MusicID.Crimson] = testTrack;
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
	public static class AudioBuilder {
		public static byte[] BuildRest(int duration) => new byte[duration * 2];
		public static byte[] BuildSquareWave(double frequency, int duration, int volume = 64, double temperment = 0.5, int leftVolume = -1) {
			if (leftVolume == -1) leftVolume = volume;
			byte[] buffer = new byte[duration * 2];
			for (int i = 0; i < duration; i++) {
				buffer[i * 2] = (byte)(((i / (24000.0 / frequency)) % 1) > temperment ? 0 : leftVolume);
				buffer[i * 2 + 1] = (byte)(((i / (24000.0 / frequency)) % 1) > temperment ? 0 : volume);
			}
			return buffer;
		}
		public static byte[] ExtractWav(string name) {
			byte[] bytes =  ModContent.GetFileBytes(name);
			int pos = 12;   // First Subchunk ID from 12 to 16

			// Keep iterating until we find the data chunk (i.e. 64 61 74 61 ...... (i.e. 100 97 116 97 in decimal))
			while (!(bytes[pos] == 100 && bytes[pos + 1] == 97 && bytes[pos + 2] == 116 && bytes[pos + 3] == 97)) {
				pos += 4;
				int chunkSize = bytes[pos] + bytes[pos + 1] * 256 + bytes[pos + 2] * 65536 + bytes[pos + 3] * 16777216;
				pos += 4 + chunkSize;
			}
			pos += 8;
			int samples = (bytes.Length - pos) / 2;
			byte[] buffer = new byte[samples];
			int i = 0;
			while (pos < bytes.Length) {
				buffer[i++] = bytes[pos + 1];
				pos += 2;
			}
			return buffer;
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
			if (Main.mouseY > 0.5 * Main.screenHeight) {
				ProceduralMusicLib.testTrack.Trigger(1);
			} else {
				ProceduralMusicLib.testTrack.UnTrigger(1);
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
	public struct AudioChannel {
		public Keyframe[] keyframes;
		public bool loops;
		public AudioChannel(params Keyframe[] keyframes) : this(false, keyframes) {}
		public AudioChannel(bool loop, params Keyframe[] keyframes) {
			loops = loop;
			this.keyframes = keyframes;
		}
		public struct Keyframe {
			public KeyframeType type;
			public int trigger;
			public byte[] audio;
			public AudioChannel[] channels;
			Keyframe(KeyframeType type, int trigger, byte[] audio, AudioChannel[] channels) {
				this.type = type;
				this.trigger = trigger;
				this.audio = audio;
				this.channels = channels;
			}
			public static Keyframe Stop(int trigger = 0) => new(KeyframeType.STOP, trigger, Array.Empty<byte>(), Array.Empty<AudioChannel>());
			public static Keyframe AddChannels(int trigger = 0, params AudioChannel[] channels) => new(KeyframeType.ADD_CHANNEL, trigger, Array.Empty<byte>(), channels);
			public static Keyframe Switch(int trigger = 0, params AudioChannel[] channels) => new(KeyframeType.SWITCH, trigger, Array.Empty<byte>(), channels);
			public static Keyframe Audio(byte[] audio, int trigger = 0) => new(KeyframeType.NOTE, trigger, audio, Array.Empty<AudioChannel>());
		}
		public enum KeyframeType {
			NOTE        = 0b0001,
			ADD_CHANNEL = 0b0010,
			STOP        = 0b0100,
			SWITCH      = 0b0110,
		}
		public AudioChannel Clone() {
			return this with { keyframes = keyframes.ToArray() };
		}
		public int Update(ref int progress, int position, HashSet<int> triggers, [Out] List<AudioChannel?> switches) {
			int tries = 0;
			while (++tries < 100) {
				if (progress >= keyframes.Length) {
					if (loops) {
						progress %= keyframes.Length;
					} else {
						switches.Add(null);
						return 0;
					}
				}
				Keyframe keyframe = keyframes[progress];
				if (position == 0 && keyframe.trigger != 0 && (triggers.Contains(Math.Abs(keyframe.trigger)) ^ (keyframe.trigger > 0))) {
					progress++;
					continue;
				}
				if ((keyframe.type & KeyframeType.STOP) != 0) {
					switches.Add(null);
				}
				if ((keyframe.type & KeyframeType.ADD_CHANNEL) != 0) {
					for (int i = 0; i < keyframe.channels.Length; i++) {
						switches.Add(keyframe.channels[i]);
					}
				}
				if ((keyframe.type & KeyframeType.NOTE) != 0) {
					if (position >= keyframe.audio.Length) {
						position = 0;
						progress++;
						continue;
					}
					return keyframe.audio[position];
				}
				progress++;
			}
			return 0;
		}
	}
	public class ActiveAudioChannel {
		readonly AudioChannel audioChannel;
		int progress;
		int frameStart;
		public ActiveAudioChannel(AudioChannel audioChannel, int position) {
			this.audioChannel = audioChannel;
			progress = 0;
			frameStart = position;
		}
		public static byte UpdateChannels(List<ActiveAudioChannel> activeChannels, int position, HashSet<int> triggers) {
			int value = 0;
			List<AudioChannel> switches = new();
			for (int c = 0; c < activeChannels.Count; c++) {
				ActiveAudioChannel activeChannel = activeChannels[c];
				List<AudioChannel?> newSwitches = new();
				int oldProgress = activeChannel.progress;
				value += activeChannel.audioChannel.Update(ref activeChannel.progress, position - activeChannel.frameStart, triggers, newSwitches);

				if (oldProgress != activeChannel.progress) activeChannel.frameStart = position;
				bool removed = false;
				for (int i = 0; i < newSwitches.Count; i++) {
					if (newSwitches[i].HasValue) {
						switches.Add(newSwitches[i].Value);
					} else {
						if (removed) {
							///TODO: logging
						} else {
							activeChannels.RemoveAt(c--);
							removed = true;
						}
					}
				}
			}
			for (int i = 0; i < switches.Count; i++) {
				activeChannels.Add(new (switches[i], position));
			}
			return (byte)value;
		}
	}
	public enum ChannelSwitches {
		ADD,
		REMOVE
	}
	public class ChanneledAudioTrack : ASoundEffectBasedAudioTrack {
		int position = 0;
		public List<ActiveAudioChannel> activeChannels;
		public AudioChannel[] defaultChannels;
		public HashSet<int> triggers;
		public event Action OnTrackEnd;
		public ChanneledAudioTrack(params AudioChannel[] defaultChannels) {
			CreateSoundEffect(12000, AudioChannels.Stereo);
			this.defaultChannels = defaultChannels;
			triggers = new();
			activeChannels = new();
		}
		public override void Dispose() {

		}

		public override void Reuse() {
			position = 0;
			triggers.Clear();
			activeChannels.Clear();
			for (int i = 0; i < defaultChannels.Length; i++) {
				activeChannels.Add(new ActiveAudioChannel(defaultChannels[i], 0));
			}
		}
		public void Trigger(int trigger) {
			if (IsPlaying) {
				triggers.Add(trigger);
			}
		}
		public void UnTrigger(int trigger) {
			if (IsPlaying) {
				triggers.Remove(trigger);
			}
		}
		protected override void ReadAheadPutAChunkIntoTheBuffer() {
			ProceduralMusicLibSystem._bufferToSubmit = _bufferToSubmit;
			for (int i = 0; i < _bufferToSubmit.Length; i++) {
				position++;
				_bufferToSubmit[i] = ActiveAudioChannel.UpdateChannels(activeChannels, position, triggers);
			}
			if (false) {
				Stop(AudioStopOptions.Immediate);
			} else {
				_soundEffectInstance.SubmitBuffer(_bufferToSubmit);
			}
			if (!activeChannels.Any()) {
				OnTrackEnd();
				Stop(AudioStopOptions.Immediate);
			}
		}
	}
}