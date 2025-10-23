using Coherence.Toolkit;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Y200.ProjectMultiplayer
{
    public class CharacterSelectWindow : MonoBehaviour
    {
        [SerializeField] private Guid _receivedGuid;

        [SerializeField] private Button _character1Button;   
        [SerializeField] private Button _character2Button;   
        [SerializeField] private Button _character3Button;

        private void Awake()
        {
            GlobalEvent.OnWorldConnected += OnWorldJoined;
            GlobalEvent.OnWorldDisconnected += OnWorldDisconnected;

            _character1Button.onClick.AddListener(() => SelectCharacter(0));
            _character2Button.onClick.AddListener(() => SelectCharacter(1));
            _character3Button.onClick.AddListener(() => SelectCharacter(2));

            this.gameObject.SetActive(false);
        }

        private void OnWorldJoined(Guid id)
        {
            _receivedGuid = id;

            this.gameObject.SetActive(true);
        }

        private void OnWorldDisconnected(Guid id)
        {
            _receivedGuid = id;

            this.gameObject.SetActive(false);
        }

        public void SelectCharacter(byte type)
        {
            GlobalEvent.OnCharacterSelected?.Invoke(_receivedGuid, type);
            this.gameObject.SetActive(false);
        }
    }
}