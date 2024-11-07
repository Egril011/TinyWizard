using FMOD.Studio;
using FMODUnity;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Quinn.DungeonGeneration
{
	public class DungeonGenerator : MonoBehaviour
	{
		enum DoorCriteria
		{
			None,
			Required,
			Banned
		}

		record RoomCriteria
		{
			public DoorCriteria North, South, East, West;

			public bool IsMatch(Room room)
			{
				// Require.
				if (North is DoorCriteria.Required && !room.HasNorthDoor) return false;
				if (South is DoorCriteria.Required && !room.HasSouthDoor) return false;
				if (East is DoorCriteria.Required && !room.HasEastDoor) return false;
				if (West is DoorCriteria.Required && !room.HasWestDoor) return false;

				// Ban.
				if (North is DoorCriteria.Banned && room.HasNorthDoor) return false;
				if (South is DoorCriteria.Banned && room.HasSouthDoor) return false;
				if (East is DoorCriteria.Banned && room.HasEastDoor) return false;
				if (West is DoorCriteria.Banned && room.HasWestDoor) return false;

				return true;
			}

			public override string ToString()
			{
				return $"N {North}, S {South}, E {East}, W {West}";
			}
		}

		[SerializeField]
		private int MaxRoomSize = 48;
		[SerializeField, RequiredListLength(MinLength = 1)]
		private FloorSO[] Floors;

		public static DungeonGenerator Instance { get; private set; }

		public FloorSO ActiveFloor { get; private set; }

		private readonly Dictionary<Vector2Int, GameObject> _generatedRooms = new();
		private EventInstance _ambience, _music;

		private void Awake()
		{
			Debug.Assert(Instance == null);
			Instance = this;
		}

		private async void Start()
		{
			var floor = Floors[Random.Range(0, Floors.Length)];
			await StartFloorAsync(floor);
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public async void GenerateRoomAt(int x, int y)
		{
			if (_generatedRooms.ContainsKey(new Vector2Int(x, y)))
				return;

			Debug.Assert(ActiveFloor != null, "Failed to generate room. There is no active floor!");

			// Rules to be used for filtering rooms while deciding which room to generate.
			var criteria = new RoomCriteria();

			if (GetRoomAt(x, y + 1, out Room n))
				criteria.North = n.HasSouthDoor ? DoorCriteria.Required : DoorCriteria.Banned;
			if (GetRoomAt(x, y - 1, out Room s))
				criteria.South = s.HasNorthDoor ? DoorCriteria.Required : DoorCriteria.Banned;
			if (GetRoomAt(x - 1, y, out Room e))
				criteria.East = e.HasWestDoor ? DoorCriteria.Required : DoorCriteria.Banned;
			if (GetRoomAt(x + 1, y, out Room w))
				criteria.West = w.HasEastDoor ? DoorCriteria.Required : DoorCriteria.Banned;

			// Filter for rooms that support required doors.
			var validRooms = ActiveFloor.Generatable.Where(roomToGenerate => criteria.IsMatch(roomToGenerate.Prefab));
			// Get random (by weight) room from filtered collection.
			Room prefab = validRooms.GetWeightedRandom(x => x.Weight).Prefab;

			Debug.Assert(prefab != null, $"Failed to generate room. No valid option found! Criteria: {criteria}.");

			// Generate actual room.
			await GenerateRoomAsync(prefab, x, y);
		}

		private bool GetRoomAt(int x, int y, out Room room)
		{
			if (_generatedRooms.TryGetValue(new(x, y), out GameObject value))
			{
				room = value.GetComponent<Room>();
				return true;
			}

			room = null;
			return false;
		}

		private async Awaitable StartFloorAsync(FloorSO floor)
		{
			foreach (var room in _generatedRooms)
			{
				Destroy(room.Value);
			}

			ActiveFloor = floor;

			// Ambience.
			if (_ambience.isValid())
			{
				_ambience.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			}

			if (!floor.Ambience.IsNull)
			{
				_ambience = RuntimeManager.CreateInstance(floor.Ambience);
				_ambience.start();
			}

			// Music.
			if (_music.isValid())
			{
				_music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			}

			if (!floor.Music.IsNull)
			{
				_music = RuntimeManager.CreateInstance(floor.Music);
				_music.start();
			}

			var prefab = floor.Generatable.GetWeightedRandom(x => x.Weight).Prefab;
			await GenerateRoomAsync(floor.StartingRoom, 0, 0);
		}

		private async Awaitable<Room> GenerateRoomAsync(Room prefab, int x, int y)
		{
			Vector2 pos = RoomGridToWorld(x, y) - Vector2.one;
			var instance = await prefab.gameObject.CloneAsync(pos, Quaternion.identity, transform);

			if (instance == null)
			{
				throw new System.NullReferenceException("Failed to generate room!");
			}

			_generatedRooms.Add(new(x, y), instance);

			var room = instance.GetComponent<Room>();
			room.RoomGridIndex = new(x, y);

			return room;
		}

		private Vector2 RoomGridToWorld(int x, int y)
		{
			return new Vector2(x, y) * MaxRoomSize;
		}
	}
}
