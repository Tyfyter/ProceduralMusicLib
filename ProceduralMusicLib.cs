using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;
using XPT.Core.Audio.MP3Sharp.Decoding;
using static ProceduralMusicLib.AudioBuilder;

namespace ProceduralMusicLib {
	public class ProceduralMusicLib : Mod {
		public ProceduralMusicLib() : base() {
			MusicAutoloadingEnabled = false;
			ReserveMusicID = typeof(MusicLoader).GetMethod(nameof(ReserveMusicID), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).CreateDelegate<Func<int>>(null);
			musicByPath = (Dictionary<string, int>)typeof(MusicLoader).GetField(nameof(musicByPath), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
			musicExtensions = (Dictionary<string, string>)typeof(MusicLoader).GetField(nameof(musicExtensions), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
			musicSkipsVolumeRemap = (Dictionary<int, bool>)typeof(MusicLoader).GetField(nameof(musicSkipsVolumeRemap), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
			MonoModHooks.Add(typeof(MusicLoader).GetMethod("LoadMusic", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), (Func<string, string, IAudioTrack> orig, string path, string extension) => {
				if (extension == "json") {
					JSONAudioTrack track = JSONAudioTrack.FromFile(path);
					AddJSONTrack(path, track);
					return track;
				}
				return orig(path, extension);
			});
		}
		readonly Func<int> ReserveMusicID;
		readonly Dictionary<string, int> musicByPath;
		readonly Dictionary<string, string> musicExtensions;
		readonly Dictionary<int, bool> musicSkipsVolumeRemap;
		readonly Dictionary<string, JSONAudioTrack> jsonMusicByPath = [];
		readonly List<FileSystemWatcher> fileSystemWatchers = [];
		public void AddJSONTrack(string musicPath, JSONAudioTrack track) {
			if (!jsonMusicByPath.ContainsKey(musicPath)) {
				FileSystemWatcher trackDescriptorFileWatcher = new();
				string[] path = musicPath.Split('/');
				trackDescriptorFileWatcher.Path = Path.Combine([Program.SavePathShared, "ModSources", ..path[..^1]]);
				trackDescriptorFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
				trackDescriptorFileWatcher.Filter = path[^1] + ".json";
				trackDescriptorFileWatcher.IncludeSubdirectories = false;

				void ReloadTrack() {
					if (!musicByPath.TryGetValue(musicPath, out int id)) return;

					if (Main.audioSystem is LegacyAudioSystem { AudioTracks: IAudioTrack[] audioTracks } && audioTracks[id] is not null) {
						IAudioTrack oldTrack = audioTracks[id];
						Main.QueueMainThreadAction(() => oldTrack.Stop(AudioStopOptions.Immediate));
						JSONAudioTrack track = JSONAudioTrack.FromJSON(File.ReadAllText(Path.Combine(trackDescriptorFileWatcher.Path, trackDescriptorFileWatcher.Filter)));
						audioTracks[id] = track;
					}
					Utils.LogAndChatAndConsoleInfoMessage($"Music file {musicPath} was changed, reloading track");
				}

				trackDescriptorFileWatcher.Changed += (a, b) => {
					ReloadTrack();
				};
				trackDescriptorFileWatcher.Renamed += (a, b) => {
					if (b.Name == trackDescriptorFileWatcher.Filter) {
						ReloadTrack();
					} else if (!b.Name.EndsWith(".TMP") || !b.Name.StartsWith(trackDescriptorFileWatcher.Filter)) {
						trackDescriptorFileWatcher.EnableRaisingEvents = false;
					}
				};

				// Begin watching.
				trackDescriptorFileWatcher.EnableRaisingEvents = true;
				fileSystemWatchers.Add(trackDescriptorFileWatcher);
			}
			jsonMusicByPath[musicPath] = track;
		}
		public enum CallType {
			/// <summary>
			/// Adds a track, use « AddMusic Mod Path » or « AddMusic Path »
			/// </summary>
			AddMusic,
			/// <summary>
			/// Replaces a track with a json track at the same path, use « ModifyMusic Mod Path » or « ModifyMusic Path »
			/// </summary>
			ModifyMusic,
			/// <summary>
			/// Replaces a track with a json track, use « ReplaceMusic Mod Path TrackID » or « ReplaceMusic Path TrackID »
			/// </summary>
			ReplaceMusic,
			/// <summary>
			/// Sets or unsets a trigger in a track, use « SetTrigger Mod Path TriggerID [false] » or « SetTrigger Path TriggerID [false] »
			/// </summary>
			SetTrigger
		}
		public override object Call(params object[] args) => Call(
			args[0] is CallType callType ? callType : Enum.Parse<CallType>((string)args[0], true),
			args[1..]
		);
		public object Call(CallType callType, params object[] args) {
			string musicPath;
			bool skipVolumeRemap = true;
			if (args[0] is Mod mod) {
				musicPath = mod.Name + "/" + (string)args[1];
				args = args[2..];
				skipVolumeRemap = mod.MusicSkipsVolumeRemap;
			} else {
				musicPath = (string)args[0];
				args = args[1..];
			}
			switch (callType) {
				case CallType.AddMusic: {
					if (musicByPath.TryGetValue(musicPath, out int id)) return id;
					id = ReserveMusicID();
					musicByPath[musicPath] = id;
					musicExtensions[musicPath] = "json";
					musicSkipsVolumeRemap[id] = skipVolumeRemap;
					return id;
				}
				case CallType.ModifyMusic: {
					if (!musicByPath.TryGetValue(musicPath, out int id)) goto case CallType.AddMusic;

					if (Main.audioSystem is LegacyAudioSystem { AudioTracks: IAudioTrack[] audioTracks } && audioTracks[id] is not null) {
						audioTracks[id].Stop(AudioStopOptions.Immediate);
						JSONAudioTrack track = JSONAudioTrack.FromFile(musicPath);
						AddJSONTrack(musicPath, track);
						audioTracks[id] = track;
					}
					musicByPath[musicPath] = id;
					musicExtensions[musicPath] = "json";
					return id;
				}
				case CallType.ReplaceMusic: {
					int id;
					if (args[0] is int @int) {
						id = @int;
					} else if (args[0] is short @short) {
						id = @short;
					} else if (args[0] is ushort @ushort) {
						id = @ushort;
					} else {
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
						throw new ArgumentException($"Invalid numeric type {args[0].GetType()}, supported types are int, short, and ushort", "TrackID");
#pragma warning restore CA2208 // Instantiate argument exceptions correctly
#pragma warning restore IDE0079 // Remove unnecessary suppression
					}

					if (Main.audioSystem is LegacyAudioSystem { AudioTracks: IAudioTrack[] audioTracks } && audioTracks[id] is not null) {
						audioTracks[id].Stop(AudioStopOptions.Immediate);
						JSONAudioTrack track = JSONAudioTrack.FromFile(musicPath);
						AddJSONTrack(musicPath, track);
						audioTracks[id] = track;
					}
					musicByPath[musicPath] = id;
					musicExtensions[musicPath] = "json";
					return id;
				}
				case CallType.SetTrigger: {
					if (jsonMusicByPath.TryGetValue(musicPath, out JSONAudioTrack track)) {
						if (args.Length > 1 && args[1] is bool value && !value) {
							track.UnTrigger((int)args[0]);
						} else {
							track.Trigger((int)args[0]);
						}
					}
					return null;
				}
			}
			return null;
		}
		public static Func<int, byte> BuildSquareSequence(params (int freq, int dur)[] notes) {
			int totalLength = 0;
			List<Func<int, byte>> funcs = [];
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
			GC.SuppressFinalize(this);
		}

		public override void Reuse() {
			position = 0;
		}
		protected override void ReadAheadPutAChunkIntoTheBuffer() {
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
		public readonly int Update(ref int progress, int position, HashSet<int> triggers, [Out] List<AudioChannel?> switches) {
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
	public class ActiveAudioChannel(AudioChannel audioChannel, int position) {
		readonly AudioChannel audioChannel = audioChannel;
		int progress = 0;
		int frameStart = position;

		public static byte UpdateChannels(List<ActiveAudioChannel> activeChannels, int position, HashSet<int> triggers) {
			int value = 0;
			List<AudioChannel> switches = [];
			for (int c = 0; c < activeChannels.Count; c++) {
				ActiveAudioChannel activeChannel = activeChannels[c];
				List<AudioChannel?> newSwitches = [];
				int oldProgress = activeChannel.progress;
				value += activeChannel.audioChannel.Update(ref activeChannel.progress, position - activeChannel.frameStart, triggers, newSwitches);

				if (oldProgress != activeChannel.progress) activeChannel.frameStart = position + 1;
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
	public class BufferAudioTrack : ASoundEffectBasedAudioTrack {
		int position = 0;
		byte[] buffer;
		public BufferAudioTrack(int sampleRate, AudioChannels channels, byte[] buffer) {
			CreateSoundEffect(sampleRate, channels);
			this.buffer = buffer;
		}

		public override void Dispose() {
			buffer = null;
			GC.SuppressFinalize(this);
		}

		public override void Reuse() {
			position = 0;
		}
		protected override void ReadAheadPutAChunkIntoTheBuffer() {
			for (int i = 0; i < _bufferToSubmit.Length; i++) {
				_bufferToSubmit[i] = buffer[position];
				position = (position + 1) % buffer.Length;
			}
			_soundEffectInstance.SubmitBuffer(_bufferToSubmit);
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
			activeChannels = null;
			defaultChannels = null;
			triggers = null;
			OnTrackEnd = null;
			GC.SuppressFinalize(this);
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
			for (int i = 0; i < _bufferToSubmit.Length; i++) {
				_bufferToSubmit[i] = ActiveAudioChannel.UpdateChannels(activeChannels, position++, triggers);
			}
			if (false) {
				Stop(AudioStopOptions.Immediate);
			} else {
				_soundEffectInstance.SubmitBuffer(_bufferToSubmit);
			}
			if (activeChannels.Count == 0) {
				OnTrackEnd?.Invoke();
				//Stop(AudioStopOptions.Immediate);
			}
		}
	}
}