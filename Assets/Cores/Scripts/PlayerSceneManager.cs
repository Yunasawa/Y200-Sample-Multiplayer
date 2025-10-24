using Coherence.Toolkit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Y200.ProjectMultiplayer
{
    public class PlayerSceneManager : MonoBehaviour
    {
        public byte SceneType;

        [SerializeField] private CoherenceSync _sync;

        [SerializeField] private Button _scene1LoadButton;
        [SerializeField] private Button _scene2LoadButton;

        private void Awake()
        {
            if (_scene1LoadButton) _scene1LoadButton.onClick.AddListener(() => ChangeScene(1));
            if (_scene2LoadButton) _scene2LoadButton.onClick.AddListener(() => ChangeScene(2));
        }

        public void ChangeScene(byte scene)
        {
            SceneType = scene;

            SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);

            _sync.SendCommand<PlayerSceneManager>(nameof(OnSceneChanged), Coherence.MessageTarget.Other, scene);
        }

        [Command]
        public void OnSceneChanged(byte scene)
        {
            SceneType = scene;

            Debug.Log($"Hello: {scene}");

            SceneManager.LoadSceneAsync(SceneType, LoadSceneMode.Additive);
        }
    }
}