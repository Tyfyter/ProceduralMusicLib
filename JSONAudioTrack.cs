﻿using Microsoft.Xna.Framework.Audio;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using NVorbis;
using NVorbis.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Terraria.ModLoader;
using XPT.Core.Audio.MP3Sharp;

namespace ProceduralMusicLib {
	public class JSONAudioTrack : ChanneledAudioTrack {
		public JSONAudioTrack(string fileName) {
			string text = Encoding.UTF8.GetString(ModContent.GetFileBytes(fileName + ".json")).Replace("\r", "").Replace("\t", "").Replace("\n", "");
			AudioTrackDescriptor descriptor = new();
			JsonConvert.PopulateObject(text, descriptor, new JsonSerializerSettings {
				Formatting = Formatting.Indented,
				DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
				ObjectCreationHandling = ObjectCreationHandling.Replace,
				NullValueHandling = NullValueHandling.Ignore
			});
			Dictionary<string, ExtractedAudioTrack> tracks = [];
			HashSet<int> sampleRates = [];
			HashSet<AudioChannels> channelCounts = [];
			foreach (KeyValuePair<string, AudioChunkData> item in descriptor.Chunks) {
				if (!tracks.ContainsKey(item.Value.AudioPath)) {
					ExtractedAudioTrack track = JSONAudioTrackLoader.ExtractAudioTrack(item.Value.AudioPath);
					tracks.Add(item.Value.AudioPath, track);
					sampleRates.Add(track.SampleRate);
					channelCounts.Add(track.Channels);
				}
			}
			if (sampleRates.Count != 1 || channelCounts.Count != 1) throw new Exception("Use of files with differing sample rates or channel counts not yet supported");
			Dictionary<string, byte[]> chunks = [];
			foreach (KeyValuePair<string, AudioChunkData> item in descriptor.Chunks) {
				ExtractedAudioTrack track = tracks[item.Value.AudioPath];
				Index end = item.Value.End == default ? ^1 : (int)(track.SampleRate * item.Value.End.TotalSeconds);
				chunks.Add(item.Key, track.Samples[(int)(track.SampleRate * item.Value.Start.TotalSeconds)..end]);
			}
			Dictionary<string, AudioChannel> segments = [];
			foreach (KeyValuePair<string, AudioSegmentsData> item in descriptor.Segments) {
				segments.Add(item.Key, new AudioChannel(item.Value.Loop, new AudioChannel.Keyframe[item.Value.Keyframes.Length]));
			}
			foreach (KeyValuePair<string, AudioSegmentsData> item in descriptor.Segments) {
				AudioChannel.Keyframe[] keyframes = segments[item.Key].keyframes;
				for (int i = 0; i < keyframes.Length; i++) {
					keyframes[i] = item.Value.Keyframes[i].Create(chunks, segments);
				}
			}
			CreateSoundEffect(sampleRates.First(), channelCounts.First());
			this.defaultChannels = [segments[descriptor.Start]];
		}
		public class AudioTrackDescriptor {
			public Dictionary<string, AudioChunkData> Chunks;
			public Dictionary<string, AudioSegmentsData> Segments;
			[DefaultValue("Start")]
			public string Start = "Start";
		}
		public class AudioChunkData {
			public string AudioPath;
			[JsonConverter(typeof(ShortTimeSpanConverter))]
			public TimeSpan Start;
			[JsonConverter(typeof(ShortTimeSpanConverter))]
			public TimeSpan End;
			public class ShortTimeSpanConverter : JsonConverter {
				public override bool CanConvert(Type objectType) {
					throw new NotImplementedException();
				}
				public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer) {
					if (reader.Value is string text) {
						string[] inputs = text.Trim().Split(':');
						if (inputs.Length == 2 && int.TryParse(inputs[0], out int mns) && double.TryParse(inputs[1], out double scs)) {
							return TimeSpan.FromMinutes(mns) + TimeSpan.FromSeconds(scs);
						}
					}
					throw new InvalidDataException();
				}
				public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer) {
					if (value is TimeSpan time) {
						writer.WriteValue($"{time.Minutes}:{time.TotalSeconds - time.Minutes * 60}");
						return;
					}
					throw new InvalidDataException();
				}
			}
		}
		public class AudioSegmentsData {
			public AudioKeyframeData[] Keyframes;
			public bool Loop;
		}
		public class AudioKeyframeData {
			public JSONKeyframeType Type;
			public string[] Segments;
			public string Chunk;
			public int Trigger;
			public enum JSONKeyframeType {
				Audio = 0b0001,
				Add_Segment = 0b0010,
				Stop = 0b0100,
				Switch_Segment = 0b0110,
			}
			public AudioChannel.Keyframe Create(Dictionary<string, byte[]> chunks, Dictionary<string, AudioChannel> segments) {
				switch (Type) {
					case JSONKeyframeType.Audio:
					return AudioChannel.Keyframe.Audio(chunks[Chunk], Trigger);

					case JSONKeyframeType.Add_Segment:
					return AudioChannel.Keyframe.AddChannels(Trigger, Segments.Select(segments.GetValueOrDefault).ToArray());

					case JSONKeyframeType.Stop:
					return AudioChannel.Keyframe.Stop(Trigger);

					case JSONKeyframeType.Switch_Segment:
					return AudioChannel.Keyframe.Switch(Trigger, Segments.Select(segments.GetValueOrDefault).ToArray());

					default:
					throw new InvalidEnumArgumentException(nameof(Type), (int)Type, typeof(JSONKeyframeType));
				}
			}
		}
	}
	public class JSONAudioTrackLoader : ILoadable {
		public delegate ExtractedAudioTrack FileReader(string fileName);
		public static Dictionary<string, FileReader> Readers { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);
		public static ExtractedAudioTrack ExtractAudioTrack(string fileName) {
			if (Path.GetExtension(fileName) is string ext && ext.Length > 0 && Readers.TryGetValue(ext[1..], out FileReader _reader)) {
				return _reader($"{fileName}");
			}
			foreach (KeyValuePair<string, FileReader> reader in Readers) {
				if (ModContent.FileExists($"{fileName}.{reader.Key}")) return reader.Value($"{fileName}.{reader.Key}");
			}
			throw new FileNotFoundException($"No such file found with supported extension", fileName);
		}
		public void Load(Mod mod) {
			Readers["wav"] = (path) => {
				MemoryStream stream = new MemoryStream(ModContent.GetFileBytes(path));
				BinaryReader binaryReader = new BinaryReader(stream);
				binaryReader.ReadInt32();
				binaryReader.ReadInt32();
				binaryReader.ReadInt32();
				AudioChannels channels = AudioChannels.Mono;
				uint sampleRate = 0u;
				bool foundFormat = false;
				bool foundData = false;
				int num = 0;
				byte[] samples = [];
				while (num < 10 && !(foundFormat && foundData)) {
					string chunkName = new(BitConverter.GetBytes(binaryReader.ReadUInt32()).Select(b => (char)b).ToArray());
					int chunkSize = binaryReader.ReadInt32();
					switch (chunkName) {
						default:
						binaryReader.ReadBytes(chunkSize % 2 == 0 ? chunkSize : (chunkSize + 1));
						break;
						case "fmt ":
						binaryReader.ReadInt16();
						channels = (AudioChannels)binaryReader.ReadUInt16();
						sampleRate = binaryReader.ReadUInt32();
						binaryReader.ReadInt32();
						binaryReader.ReadInt16();
						binaryReader.ReadInt16();
						foundFormat = true;
						break;
						case "data":
						samples = new byte[Math.Min(binaryReader.BaseStream.Length - binaryReader.BaseStream.Position, chunkSize / 2)];
						binaryReader.ReadInt32();
						binaryReader.ReadInt32();
						for (int i = 0; i < samples.Length; i++) {
							binaryReader.ReadByte();// I have no idea why this works, or what data I'm throwing out
							samples[i] = binaryReader.ReadByte();
						}
						foundData = true;
						break;
					}
					num++;
				}
				return new(samples, (int)sampleRate, channels);
			};
			Readers["ogg"] = (path) => {
				using VorbisReader vorbisReader = new(new MemoryStream(ModContent.GetFileBytes(path)));
				float[] temporaryBuffer = new float[vorbisReader.TotalSamples];
				vorbisReader.ReadSamples(temporaryBuffer, 0, temporaryBuffer.Length);
				byte[] samples = new byte[temporaryBuffer.Length * 2];//new byte[temporaryBuffer.Length * 2];
				for (int i = 0; i < temporaryBuffer.Length; i++) {
					short num = (short)(temporaryBuffer[i] * 32767f);
					samples[i * 2 + 1] = (byte)num;
					samples[i * 2] = (byte)(num >> 8);
				}
				/*for (int i = 0; i < temporaryBuffer.Length - 1; i += 2) {
					//short num = (short)(temporaryBuffer[i / 2] * 32767f);
					samples[i] = (byte)(temporaryBuffer[i] * 255f);
					samples[i + 1] = (byte)(temporaryBuffer[i + 1] * 255f);
				}*/
				return new(samples, vorbisReader.SampleRate, (AudioChannels)vorbisReader.Channels);
			};
		}
		public void Unload() {
			Readers = null;
		}
	}
	public record class ExtractedAudioTrack(byte[] Samples, int SampleRate, AudioChannels Channels);
}
