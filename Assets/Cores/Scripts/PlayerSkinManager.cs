using Coherence.Toolkit;
using System;
using UnityEngine;

namespace Y200.ProjectMultiplayer
{
    public class PlayerSkinManager : MonoBehaviour
    {
        public Guid SkinID;
        public byte CharacterType;

        [SerializeField] private CoherenceBridge _bridge;

        [SerializeField] private SkinnedMeshRenderer _jointRenderer;
        [SerializeField] private SkinnedMeshRenderer _surfaceRenderer;

        [SerializeField] private Material _c1Joint;
        [SerializeField] private Material _c1Surface;
        [SerializeField] private Material _c2Joint;
        [SerializeField] private Material _c2Surface;        
        [SerializeField] private Material _c3Joint;
        [SerializeField] private Material _c3Surface;

        private void Awake()
        {
            GlobalEvent.OnCharacterSelected += ChangeSkin;
        }

        private void OnDestroy()
        {
            GlobalEvent.OnCharacterSelected -= ChangeSkin;
        }

        public void ChangeSkin(Guid id, byte type = 0)
        {
            if (SkinID != id) return;

            CharacterType = type;

            switch (type)
            {
                case 0:
                    _jointRenderer.SetMaterials(new() { _c1Joint });
                    _surfaceRenderer.SetMaterials(new() { _c1Surface });
                    break;

                case 1:
                    _jointRenderer.SetMaterials(new() { _c2Joint });
                    _surfaceRenderer.SetMaterials(new() { _c2Surface });
                    break;

                case 2:
                    _jointRenderer.SetMaterials(new() { _c3Joint });
                    _surfaceRenderer.SetMaterials(new() { _c3Surface });
                    break;
            }
        }
    }
}