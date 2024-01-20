using System;
using System.Collections.Generic;

using Dalamud.Plugin.Services;

using Ktisis.Core.Attributes;
using Ktisis.Structs.Characters;

namespace Ktisis.Services;

// Path resolvers copied from my original implementation for Xande:
// https://github.com/xivdev/Xande/pull/3

[Singleton]
public class CustomizeDiscoveryService {
	private readonly IDataManager _data;
	
	public CustomizeDiscoveryService(
		IDataManager data
	) {
		this._data = data;
	}
	
	// Data ID bullshit

	public ushort CalcDataIdFor(Tribe tribe, Gender gender) {
		var isMasc = gender == Gender.Masculine;
		var value = tribe switch {
			Tribe.Midlander => isMasc ? 101 : 201,
			Tribe.Highlander => isMasc ? 301 : 401,
			_ => (Race)Math.Floor(((decimal)tribe + 1) / 2) switch {
				Race.Elezen => isMasc ? 501 : 601,
				Race.Miqote => isMasc ? 701 : 801,
				Race.Roegadyn => isMasc ? 901 : 1001,
				Race.Lalafell => isMasc ? 1101 : 1201,
				var race => 1301 + ((int)race - 6) * 200 + (isMasc ? 0 : 100)
			}
		};
		return (ushort)value;
	}
	
	// Discovery methods

	public bool IsFaceIdValidFor(ushort dataId, int faceId) => this._data.FileExists(ResolveFacePath(dataId, faceId));

	public IEnumerable<byte> GetFaceTypes(ushort dataId) {
		for (var i = 0; i <= byte.MaxValue; i++) {
			if (this.IsFaceIdValidFor(dataId, i))
				yield return (byte)i;
		}
	}

	public byte FindBestFaceTypeFor(ushort dataId, byte current) {
		var isRangeValid = false;
		for (var i = 0; i < byte.MaxValue; i++) {
			var valid = this.IsFaceIdValidFor(dataId, i);
			switch (valid, isRangeValid) {
				case (true, false):
					isRangeValid = true;
					if (i > current)
						return (byte)i;
					continue;
				case (false, true):
					return (byte)(i - 1);
				default:
					continue;
			}
		}
		return current;
	}
	
	// Path resolution

	private static string ResolveFacePath(ushort dataId, int faceId) => string.Format(
		"chara/human/c{0:D4}/obj/face/f{1:D4}/model/c{0:D4}f{1:D4}_fac.mdl",
		dataId,
		faceId
	);
}
