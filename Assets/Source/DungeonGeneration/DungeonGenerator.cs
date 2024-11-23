using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
using Quinn.PlayerSystem;
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
		[SerializeField, Unit(Units.Second)]
		private float MusicDelay = 1f;

		[SerializeField, Space]
		private FloorSO FirstFloor;
		[SerializeField, RequiredListLength(MinLength = 1)]
		private FloorSO[] Floors;

		public static DungeonGenerator Instance { get; private set; }

		public FloorSO ActiveFloor { get; private set; }

		private readonly Dictionary<Vector2Int, GameObject> _generatedRooms = new();
		private EventInstance _ambience, _music;

		private GameObject _lastGeneratedRoomPrefab;
		private FloorSO _lastGeneratedFloor;

		private int _floorIndex;

		public void Awake()
		{
			Debug.Assert(Instance == null);
			Instance = this;
		}

		public async void Start()
		{
			PlayerManager.Instance.OnPlayerDeath += OnPlayerDeath;
			PlayerManager.Instance.OnPlayerDeathPreSceneLoad += OnPlayerDeathPreSceneLoad;

			await StartFloorAsync(Floors[0]);
		}

		public void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public async void StartRandomFloor()
		{
			for (int i = 0; i < 1000; i++)
			{
				//var floor = Floors[Random.Range(0, Floors.Length)];
				var floor = Floors[_floorIndex];
				
				if (floor != _lastGeneratedFloor)
				{
					await StartFloorAsync(floor);
					return;
				}
			}

			throw new System.Exception("Failed to start new floor!");
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

			// Avoid using duplicate rooms if we have more than 1 room from the generation pool
			if (validRooms.Count() > 1)
			{
				validRooms = validRooms.Where(room => room.Prefab.gameObject != _lastGeneratedRoomPrefab);
			}

			// Get random (by weight) room from filtered collection.
			var selected = validRooms.GetWeightedRandom(x => x.Weight);

			Debug.Assert(selected != null, $"Failed to generate room. No valid option found! Criteria: {criteria}.");
			Room prefab = selected.Prefab;
			_lastGeneratedRoomPrefab = prefab.gameObject;

			// Generate actual room.
			await GenerateRoomAsync(prefab, x, y);
		}

		public void IncrementFloorIndex()
		{
			_floorIndex++;

			if (_floorIndex >= Floors.Length)
			{
				Debug.Log("Game Finished!");
			}
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
			_lastGeneratedFloor = floor;

			CameraManager.Instance.Blackout();

			DestroyAllRooms();
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
				DOVirtual.DelayedCall(MusicDelay, () =>
				{
					RuntimeManager.StudioSystem.setParameterByName("enable-music", 0f, ignoreseekspeed: true);

					_music = RuntimeManager.CreateInstance(floor.Music);
					_music.start();
				});
			}

			await floor.GetVariant().CloneAsync();

			var fade = CameraManager.Instance.FadeIn();
			await PlayerManager.Instance.Player.EnterFloorAsync();

			await fade;
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

		private void DestroyAllRooms()
		{
			foreach (var room in _generatedRooms)
			{
				Destroy(room.Value);
			}

			_generatedRooms.Clear();
		}

		private void OnPlayerDeath()
		{
			_music.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
			_music.release();
		}

		private void OnPlayerDeathPreSceneLoad()
		{
			DestroyAllRooms();
		}
	}
}
