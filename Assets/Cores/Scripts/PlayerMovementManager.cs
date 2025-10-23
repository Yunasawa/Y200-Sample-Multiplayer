using UnityEngine;

namespace Y200.ProjectMultiplayer
{
    [AddComponentMenu("Player: Movement Manager")]
    public class PlayerMovementManager : MonoBehaviour
    {
        [Header("References")]
        public Transform CameraSpot;

        private Vector3 _playerStartRotation;
        private float _cameraRotation;

        private Transform _playerTransform;
        private CharacterController _controller;
        [SerializeField] private Animator _animator;

        [Header("Movement Values")]
        [SerializeField] private bool _enableRotating = true;
        [SerializeField] private bool _enableMoving = true;
        [SerializeField] private bool _isCurrentlyRunSpeed = true;
        [SerializeField] private float _currentMovingSpeed = 0.12f;
        [SerializeField] private float _currentRotatingSpeed = 0.5f;
        public float WalkingSpeed = 0.1f;
        public float RunningSpeed = 0.2f;

        private Vector2 _axisDirection;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _playerTransform = transform;
        }

        private void Start()
        {
            _playerStartRotation = transform.rotation.eulerAngles;
        }

        private void Update()
        {
            VirtualUpdate();
        }

        public void VirtualUpdate()
        {
            SetCurrentSpeed(_isCurrentlyRunSpeed);

            if (_enableRotating)
                RotationInputControl(0, true, _currentRotatingSpeed, true);

            if (_enableMoving)
                MovementInputControl();
        }

        public void MovementInputControl()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _axisDirection = new Vector2(h, v);

            if (_axisDirection.magnitude < 0.1f)
                return;

            Vector3 moveDir = _playerTransform.forward;
            moveDir.y = 0;
            moveDir.Normalize();

            _controller.Move(moveDir * _currentMovingSpeed);
        }

        public void RotationInputControl(float turning, bool useLocal, float speed, bool useInput)
        {
            _cameraRotation = CameraSpot != null ? CameraSpot.localEulerAngles.y : _playerTransform.eulerAngles.y;

            if (useInput)
                _axisDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (_axisDirection.sqrMagnitude < 0.01f)
            {
                UpdateAnimation(false);
                return;
            }

            UpdateAnimation(true);

            float targetAngle = useLocal
                ? _cameraRotation + Mathf.Atan2(_axisDirection.x, _axisDirection.y) * Mathf.Rad2Deg
                : turning + Mathf.Atan2(_axisDirection.x, _axisDirection.y) * Mathf.Rad2Deg;

            PlayerRotationEntry(targetAngle, speed);
        }

        public void UpdateAnimation(bool isRunning)
        {
            _animator.SetBool("ToRun", isRunning);// ? "Run" : "Idle");
        }

        public void PlayerRotationEntry(float degree, float speed)
        {
            Quaternion targetRot = Quaternion.Euler(0, _playerStartRotation.y + degree, 0);
            _playerTransform.rotation = Quaternion.Lerp(_playerTransform.rotation, targetRot, speed);
        }

        public void SetCurrentSpeed(bool isCurrentlyRunSpeed)
        {
            _currentMovingSpeed = isCurrentlyRunSpeed ? RunningSpeed : WalkingSpeed;
        }

        public void SetCurrentWalkSpeed() => _isCurrentlyRunSpeed = false;
        public void SetCurrentRunSpeed() => _isCurrentlyRunSpeed = true;

        public void EnableMovement(bool rotating, bool moving)
        {
            _enableRotating = rotating;
            _enableMoving = moving;
        }
    }
}