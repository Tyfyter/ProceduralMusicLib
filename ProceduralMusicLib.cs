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
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using static ProceduralMusicLib.AudioBuilder;

namespace ProceduralMusicLib {
	public class ProceduralMusicLib : Mod {
		public ProceduralMusicLib() : base() {
			MusicAutoloadingEnabled = false;
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
		public static byte[] ExtractWav(string name, float speed = 1f, float volume = 1f) {
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
			float posOffset = 0;
			int GetPos() => pos + ((int)posOffset) * 2;
			while (GetPos() < bytes.Length) {
				buffer[i++] = (byte)Math.Min(bytes[GetPos() + 1] * volume, byte.MaxValue);
				posOffset += speed;
			}
			return buffer;
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
#if DEBUG
			ProceduralMusicLibTestSystem._bufferToSubmit = _bufferToSubmit;
#endif
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
	public struct AudioChannel(bool loop, params AudioChannel.Keyframe[] keyframes) {
		public Keyframe[] keyframes = keyframes;
		public bool loops = loop;
		public AudioChannel(params Keyframe[] keyframes) : this(false, keyframes) {}

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
			public static Keyframe Stop(int trigger = 0) => new(KeyframeType.STOP, trigger, [], []);
			public static Keyframe AddChannels(int trigger = 0, params AudioChannel[] channels) => new(KeyframeType.ADD_CHANNEL, trigger, [], channels);
			public static Keyframe Switch(int trigger = 0, params AudioChannel[] channels) => new(KeyframeType.SWITCH, trigger, [], channels);
			public static Keyframe Audio(byte[] audio, int trigger = 0) => new(KeyframeType.NOTE, trigger, audio, []);
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
	public class ChanneledAudioTrack : ASoundEffectBasedAudioTrack {
		int position = 0;
		public List<ActiveAudioChannel> activeChannels = [];
		public AudioChannel[] defaultChannels;
		public HashSet<int> triggers = [];
		public event Action OnTrackEnd;
		public ChanneledAudioTrack(int sampleRate, params AudioChannel[] defaultChannels) {
			CreateSoundEffect(sampleRate, AudioChannels.Stereo);
			this.defaultChannels = defaultChannels;
		}
		public ChanneledAudioTrack(params AudioChannel[] defaultChannels) : this(12000, defaultChannels) { }
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
#if DEBUG
			ProceduralMusicLibTestSystem._bufferToSubmit = _bufferToSubmit;
#endif
			for (int i = 0; i < _bufferToSubmit.Length; i++) {
				_bufferToSubmit[i] = ActiveAudioChannel.UpdateChannels(activeChannels, ++position, triggers);
			}
			if (false) {
				Stop(AudioStopOptions.Immediate);
			} else {
				_soundEffectInstance.SubmitBuffer(_bufferToSubmit);
			}
			if (!activeChannels.Any()) {
				OnTrackEnd?.Invoke();
				Stop(AudioStopOptions.Immediate);
			}
		}
	}
}