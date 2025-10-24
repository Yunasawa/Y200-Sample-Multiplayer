using Coherence.Toolkit;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Y200.ProjectMultiplayer
{
    public class PlayerSceneManager : MonoBehaviour
    {
        private static Action<byte> OnCommandReceived;

        public byte SceneType;

        [SerializeField] private CoherenceSync _sync;

        [SerializeField] private Button _scene1LoadButton;
        [SerializeField] private Button _scene2LoadButton;

        private void Awake()
        {
            if (_scene1LoadButton) _scene1LoadButton.onClick.AddListener(() => ChangeScene(1));
            if (_scene2LoadButton) _scene2LoadButton.onClick.AddListener(() => ChangeScene(2));

            OnCommandReceived += OnCommandReceivedOnLocal;
        }

        private void OnDestroy()
        {
            OnCommandReceived -= OnCommandReceivedOnLocal;
        }

        public void ChangeScene(byte scene)
        {
            if (SceneType != scene && SceneType != 0)
            {
                SceneManager.UnloadSceneAsync(SceneType);
            }

            SceneType = scene;

            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);

            OnCommandReceived?.Invoke(scene);

            _sync.SendCommand<PlayerSceneManager>(nameof(OnSceneChanged), Coherence.MessageTarget.Other, scene);
        }

        [Command]
        public void OnSceneChanged(byte scene)
        {

            if (SceneType != scene && SceneType != 0)
            {
                SceneManager.UnloadSceneAsync(SceneType);
            }

            SceneType = scene;

            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);

            OnCommandReceived?.Invoke(scene);
        }

        private void OnCommandReceivedOnLocal(byte scene)
        {
            SceneType = scene;
        }
    }
}