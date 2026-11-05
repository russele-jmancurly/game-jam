using System;
using Fusion;
using GameJam.Core;
using GameJam.Gameplay;
using GameJam.Player;
using UnityEngine;

namespace GameJam.Net
{
    /// <summary>
    /// Starts a Photon Fusion Shared Mode session and spawns everything. Every player spawns their own
    /// avatar; the master client also spawns the game state, the balloon and the items.
    /// Lives on the same GameObject as the NetworkRunner.
    /// </summary>
    [RequireComponent(typeof(NetworkRunner))]
    public sealed class NetworkBootstrap : SimulationBehaviour, IPlayerJoined, IPlayerLeft
    {
        /// <summary>False online without a Voice App ID: the voice client is removed and avatars leave the microphone off.</summary>
        public static bool VoiceEnabled { get; private set; } = true;

        [Header("Session")]
        [Tooltip("Everyone using the same session name ends up together. Change it to keep playtests apart.")]
        [SerializeField] private string _sessionName = "BalloonDrop-Dev";
        [SerializeField, Range(1, 4)] private int _maxPlayers = 4;
        [Tooltip("With no Fusion App ID set, run offline in Fusion's Single mode so the loop can still be tested solo.")]
        [SerializeField] private bool _playOfflineWithoutAppId = true;

        [Header("Prefabs")]
        [SerializeField] private NetworkObject _avatarPrefab;
        [SerializeField] private NetworkObject _balloonPrefab;
        [SerializeField] private NetworkObject _gameStatePrefab;
        [SerializeField] private NetworkObject[] _itemPrefabs = Array.Empty<NetworkObject>();
        [Tooltip("The giant roaming mischief maker (optional).")]
        [SerializeField] private NetworkObject _roamerPrefab;
        [SerializeField] private Vector3 _roamerSpawn = new Vector3(0f, 0f, 60f);

        public void Configure(NetworkObject avatarPrefab, NetworkObject balloonPrefab, NetworkObject gameStatePrefab, NetworkObject[] itemPrefabs,
            NetworkObject roamerPrefab = null, Vector3? roamerSpawn = null)
        {
            _roamerPrefab = roamerPrefab;
            _roamerSpawn = roamerSpawn ?? _roamerSpawn;
            _avatarPrefab = avatarPrefab;
            _balloonPrefab = balloonPrefab;
            _gameStatePrefab = gameStatePrefab;
            _itemPrefabs = itemPrefabs;
        }

        public void PlayerJoined(PlayerRef player)
        {
            if (player != Runner.LocalPlayer)
            {
                return;
            }

            GameServices.LocalPlayerId = player.PlayerId;
            Runner.Spawn(_avatarPrefab, Vector3.zero, Quaternion.identity, player);

            if (LocalRig.Instance != null)
            {
                LocalRig.Instance.RespawnAt(CourseLayout.PlayerSpawn(GameServices.CurrentCheckpoint, player.PlayerId));
            }

            if (OwnsSharedObjects() && GameState.Instance == null)
            {
                SpawnSharedObjects();
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            // Backup for the Master Client Object flag: if the owner of a shared object left, take it over.
            if (Runner.GameMode != GameMode.Shared || !Runner.IsSharedModeMasterClient)
            {
                return;
            }

            TakeOverIfOrphaned(GameState.Instance != null ? GameState.Instance.Object : null, player);
            TakeOverIfOrphaned(NetworkBalloon.Instance != null ? NetworkBalloon.Instance.Object : null, player);
        }

        private async void Start()
        {
            NetworkRunner runner = GetComponent<NetworkRunner>();
            runner.ProvideInput = false;

            var sceneManager = GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            var objectProvider = GetComponent<NetworkObjectProviderDefault>();
            if (objectProvider == null)
            {
                objectProvider = gameObject.AddComponent<NetworkObjectProviderDefault>();
            }

            GameMode mode = GameMode.Shared;
            if (!HasFusionAppId())
            {
                if (!_playOfflineWithoutAppId)
                {
                    Debug.LogError("[Net] No Fusion App ID. Set it in Tools > Fusion > Realtime Settings.");
                    return;
                }

                mode = GameMode.Single;
                Debug.LogWarning("[Net] No Fusion App ID set: running OFFLINE in Single mode (solo test only). " +
                                 "Set the App ID in Tools > Fusion > Realtime Settings to play online.");
            }

            VoiceEnabled = mode == GameMode.Single || HasVoiceAppId();
            if (!VoiceEnabled)
            {
                // Without a Voice App ID the voice client would still try to connect and log errors on every start. Keep
                // the component (VoiceNetworkObject on each avatar needs it) but never let it connect.
                Debug.LogWarning("[Voice] No Voice App ID set: voice chat stays off. Set it in Tools > Fusion > Realtime Settings.");
                if (TryGetComponent(out Photon.Voice.Fusion.FusionVoiceClient voiceClient))
                {
                    voiceClient.AutoConnectAndJoin = false;
                }
            }

            Debug.Log($"[Net] Starting {mode} session '{_sessionName}'...");
            try
            {
                StartGameResult result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = _sessionName,
                    PlayerCount = _maxPlayers,
                    SceneManager = sceneManager,
                    ObjectProvider = objectProvider,
                });

                if (result.Ok)
                {
                    Debug.Log($"[Net] Connected to '{_sessionName}'.");
                }
                else
                {
                    Debug.LogError($"[Net] Could not start Fusion: {result.ShutdownReason} {result.ErrorMessage}. " +
                                   "Check the Fusion App ID in Tools > Fusion > Realtime Settings.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnDestroy()
        {
            GameServices.LocalPlayerId = -1;
        }

        /// <summary>
        /// Offline (Single mode) we own everything. Check the mode first: IsSharedModeMasterClient throws
        /// outside Shared Mode.
        /// </summary>
        private bool OwnsSharedObjects() => Runner.GameMode == GameMode.Single || Runner.IsSharedModeMasterClient;

        private static bool HasFusionAppId()
        {
            return Fusion.Photon.Realtime.PhotonAppSettings.TryGetGlobal(out var settings)
                   && !string.IsNullOrWhiteSpace(settings.AppSettings.AppIdFusion);
        }

        private static bool HasVoiceAppId()
        {
            return Fusion.Photon.Realtime.PhotonAppSettings.TryGetGlobal(out var settings)
                   && !string.IsNullOrWhiteSpace(settings.AppSettings.AppIdVoice);
        }

        private void SpawnSharedObjects()
        {
            Runner.Spawn(_gameStatePrefab, Vector3.zero, Quaternion.identity);
            Runner.Spawn(_balloonPrefab, CourseLayout.BalloonSpawn(0), Quaternion.identity);
            if (_roamerPrefab != null)
            {
                Runner.Spawn(_roamerPrefab, _roamerSpawn, Quaternion.identity);
            }

            if (_itemPrefabs == null || _itemPrefabs.Length == 0)
            {
                return;
            }

            foreach (ItemSpawnPoint point in FindObjectsByType<ItemSpawnPoint>(FindObjectsSortMode.None))
            {
                NetworkObject prefab = _itemPrefabs[point.ItemIndex % _itemPrefabs.Length];
                if (prefab != null)
                {
                    Runner.Spawn(prefab, point.transform.position, point.transform.rotation);
                }
            }
        }

        private static void TakeOverIfOrphaned(NetworkObject target, PlayerRef leftPlayer)
        {
            if (target == null || target.HasStateAuthority)
            {
                return;
            }

            if (target.StateAuthority == leftPlayer || target.StateAuthority == PlayerRef.None)
            {
                target.RequestStateAuthority();
            }
        }
    }
}
