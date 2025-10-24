using Coherence.Toolkit;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Y200.ProjectMultiplayer
{
    public class PlayerMessageManager : MonoBehaviour
    {
        private static Action<string> OnMessageReceived;

        private Dictionary<string, Text> _messageItems = new();

        [SerializeField] private CoherenceSync _sync;

        [SerializeField] private Text _textObject;
        [SerializeField] private InputField _nameInput;
        [SerializeField] private InputField _messageInput;
        [SerializeField] private RectTransform _messageContainer;

        private void Awake()
        {
            if (_messageInput) _messageInput.onSubmit.AddListener(SubmitMessage);

            OnMessageReceived += OnMessageReceivedOnLocal;
        }

        private void OnDestroy()
        {
            OnMessageReceived -= OnMessageReceivedOnLocal;
        }

        private void SubmitMessage(string message)
        {
            var completeMessage = GetMessage(_nameInput.text, message);

            CreateMessageUI(completeMessage);

            _sync.SendCommand<PlayerMessageManager>(nameof(OnMessageSent), Coherence.MessageTarget.Other, completeMessage);

            _messageInput.text = string.Empty;
        }

        [Command]
        public void OnMessageSent(string completeMessage)
        {
            OnMessageReceived?.Invoke(completeMessage);
        }

        private void OnMessageReceivedOnLocal(string message)
        {
            CreateMessageUI(message);
        }

        private void CreateMessageUI(string completeMessage)
        {
            if (_textObject == null || _messageContainer == null) return;

            var textItem = Instantiate(_textObject, _messageContainer);
            textItem.gameObject.SetActive(true);
            textItem.text = completeMessage;

            _messageItems[completeMessage] = textItem;

            Debug.Log($"{completeMessage}, {_messageContainer == null}");
        }

        private string GetMessage(string playerName, string message) => $"<b><color=#FFFB00>{playerName}</color></b>: {message}";
    }
}