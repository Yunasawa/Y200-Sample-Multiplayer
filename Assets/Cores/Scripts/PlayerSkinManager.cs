using Coherence.Toolkit;
using System;
using UnityEngine;

namespace Y200.ProjectMultiplayer
{
    public class PlayerSkinManager : MonoBehaviour
    {
        public Guid SkinID;
        [Sync] public byte CharacterType;

        [SerializeField] private CoherenceSync _sync;

        [HideInInspector] public Animator Animator;

        [SerializeField] private Animator _character1;
        [SerializeField] private Animator _character2;
        [SerializeField] private Animator _character3;

        private void Awake()
        {
            GlobalEvent.OnCharacterSelected += OnCharacterSelected;
            GlobalEvent.OnClientJoined += OnClientJoined;
        }

        private void OnDestroy()
        {
            GlobalEvent.OnCharacterSelected -= OnCharacterSelected;
            GlobalEvent.OnClientJoined -= OnClientJoined;
        }

        private void OnCharacterSelected(Guid id, byte type)
        {
            if (SkinID == id)
            {
                CharacterType = type;
            }

            UpdateCharacter(CharacterType);
        }

        private void OnClientJoined(Guid id)
        {
            UpdateCharacter(CharacterType);
        }

        private void UpdateCharacter(byte type)
        {
            _character1.gameObject.SetActive(false);
            _character2.gameObject.SetActive(false);
            _character3.gameObject.SetActive(false);

            switch (type)
            {
                case 0:
                    Animator = _character1;
                    _character1.gameObject.SetActive(true);
                    break;
                case 1:
                    Animator = _character2;
                    _character2.gameObject.SetActive(true);
                    break;
                case 2:
                    Animator = _character3;
                    _character3.gameObject.SetActive(true);
                    break;
            }
        }
    }
}