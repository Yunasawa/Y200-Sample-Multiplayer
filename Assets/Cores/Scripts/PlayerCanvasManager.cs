using Coherence.Toolkit;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Y200.ProjectMultiplayer
{
    public class PlayerCanvasManager : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private RectTransform _canvas;
        [SerializeField] private Text _nameBadge;

        [SerializeField] private InputField _nameInput;

        private void Awake()
        {
            GlobalEvent.OnWorldConnected += SetName;
        }

        private void Update()
        {
            _canvas.transform.LookAt(_camera.transform);
        }

        public void SetName(Guid _)
        {
            _nameBadge.text = _nameInput.text;
        }
    }
}