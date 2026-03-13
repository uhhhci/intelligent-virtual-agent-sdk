using System.Collections;
using IVH.Core.Actions;
using UnityEngine;
using UnityEngine.AI;

namespace IVH.Core.IntelligentVirtualAgent
{
    public class AgentLocomotion : MonoBehaviour
    {
        private EyeGazeController _eyeGazeController;
        private NavMeshAgent _navMeshAgent;
        private Animator _animator;

        public bool isMoving = false;

        [HideInInspector]
        public Vector3 cameraPos;

        [Header("Movement Tuning")]
        public float rotationSpeed = 120f;
        public float arrivalThreshold = 0.15f;
        public float speedDampTime = 0.15f;
        public float turnInPlaceAngleThreshold = 60f;
        public float turnInPlaceDuration = 0.4f;

        [Header("Speed Mapping")]
        [Tooltip("NavMeshAgent speed that maps to 1.0 in the blend tree")]
        public float referenceSpeed = 1.0f;

        [Header("Rotation Behavior")]
        [Tooltip("Max angle (degrees) between forward and movement direction before agent stops rotating and uses strafe/backward anims instead")]
        public float maxRotationAngle = 70f;

        // Triggers to enter/exit locomotion
        private static readonly int WalkingHash = Animator.StringToHash("Walking");
        private static readonly int StopWalkingHash = Animator.StringToHash("stopWalking");

        // 2D blend tree parameters (local velocity)
        private static readonly int VelXHash = Animator.StringToHash("VelX");
        private static readonly int VelZHash = Animator.StringToHash("VelZ");

        // Turn in place
        private static readonly int TurnInPlaceHash = Animator.StringToHash("TurnInPlace");
        private static readonly int TurnDirectionHash = Animator.StringToHash("TurnDirection");

        private Coroutine _movementCoroutine;

        // Store the agent's facing direction at the start of movement
        // so it maintains orientation during backward/strafe movement
        private bool _shouldRotateTowardsMovement = true;

        private void Awake()
        {
            _eyeGazeController = GetComponent<EyeGazeController>();
            _navMeshAgent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_navMeshAgent != null)
            {
                _navMeshAgent.updateRotation = false;
                _navMeshAgent.updatePosition = true;
            }
        }

        private void Update()
        {
            if (_navMeshAgent == null) return;

            UpdateBlendTreeVelocity();

            if (!isMoving) return;

            if (_shouldRotateTowardsMovement)
                SmoothRotateTowardsVelocity();
        }

        private void UpdateBlendTreeVelocity()
        {
            if (_animator == null) return;

            Vector3 worldVelocity = _navMeshAgent.velocity;
            Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

            float velX = localVelocity.x / referenceSpeed;
            float velZ = localVelocity.z / referenceSpeed;

            if (HasAnimatorParameter(VelXHash))
                _animator.SetFloat(VelXHash, velX, speedDampTime, Time.deltaTime);

            if (HasAnimatorParameter(VelZHash))
                _animator.SetFloat(VelZHash, velZ, speedDampTime, Time.deltaTime);
        }

        private void SmoothRotateTowardsVelocity()
        {
            Vector3 desiredVelocity = _navMeshAgent.desiredVelocity;
            if (desiredVelocity.sqrMagnitude < 0.01f) return;

            Vector3 flatDirection = Vector3.ProjectOnPlane(desiredVelocity, Vector3.up).normalized;
            if (flatDirection == Vector3.zero) return;

            // Check the angle between the agent's forward and the movement direction
            float angle = Vector3.Angle(transform.forward, flatDirection);

            // Only rotate if the movement is roughly forward
            // If the angle is too large, the agent should strafe or walk backward instead
            if (angle > maxRotationAngle) return;

            Quaternion targetRotation = Quaternion.LookRotation(flatDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        public void MoveToPoint(Vector3 targetPosition, float speed, bool faceMovementDirection = true)
        {
            if (_navMeshAgent == null) return;

            if (_movementCoroutine != null)
                StopCoroutine(_movementCoroutine);

            isMoving = true;
            _navMeshAgent.speed = Mathf.Max(speed, 0.3f);
            _navMeshAgent.SetDestination(targetPosition);

            _movementCoroutine = StartCoroutine(MovementSequence(targetPosition, faceMovementDirection));
        }

        private IEnumerator MovementSequence(Vector3 targetPosition, bool faceMovementDirection)
        {
            // --- Wait for path ---
            while (_navMeshAgent.pathPending)
                yield return null;

            if (_navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning("AgentLocomotion: Invalid path!");
                isMoving = false;
                yield break;
            }

            // --- Determine rotation behavior ---
            Vector3 dirToTarget = (targetPosition - transform.position);
            dirToTarget.y = 0;

            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                float angleToTarget = Vector3.SignedAngle(transform.forward, dirToTarget.normalized, Vector3.up);

                if (faceMovementDirection)
                {
                    // Turn to face movement direction, then walk forward
                    _shouldRotateTowardsMovement = true;

                    if (Mathf.Abs(angleToTarget) > turnInPlaceAngleThreshold)
                        yield return StartCoroutine(TurnInPlace(angleToTarget));
                }
                else
                {
                    // Keep current facing, use strafe/backward animations
                    _shouldRotateTowardsMovement = false;
                }
            }

            // --- Enter locomotion via trigger ---
            if (_animator != null && HasAnimatorParameter(WalkingHash))
                _animator.SetTrigger(WalkingHash);

            _eyeGazeController?.SetGazeModeIdle();

            // --- Walk until arrived ---
            while (_navMeshAgent.remainingDistance > arrivalThreshold)
            {
                if (_navMeshAgent.velocity.sqrMagnitude < 0.001f && !_navMeshAgent.pathPending)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (_navMeshAgent.velocity.sqrMagnitude < 0.001f && _navMeshAgent.remainingDistance > arrivalThreshold)
                    {
                        Debug.LogWarning("AgentLocomotion: Agent appears stuck, aborting.");
                        break;
                    }
                }
                yield return null;
            }

            // --- Decelerate blend tree ---
            yield return StartCoroutine(DecelerateBlendTree());

            // --- Stop NavMeshAgent ---
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();
            _navMeshAgent.isStopped = false;

            // --- Exit locomotion via trigger ---
            if (_animator != null && HasAnimatorParameter(StopWalkingHash))
                _animator.SetTrigger(StopWalkingHash);

            // --- Turn to face player ---
            if (cameraPos != Vector3.zero)
                yield return StartCoroutine(SmoothRotateTowards(cameraPos));

            _eyeGazeController?.SetGazeModeLookAtPlayer();
            _shouldRotateTowardsMovement = true;
            isMoving = false;
        }

        private IEnumerator DecelerateBlendTree()
        {
            if (_animator == null) yield break;

            float startX = HasAnimatorParameter(VelXHash) ? _animator.GetFloat(VelXHash) : 0f;
            float startZ = HasAnimatorParameter(VelZHash) ? _animator.GetFloat(VelZHash) : 0f;
            float decelDuration = 0.3f;
            float elapsed = 0f;

            while (elapsed < decelDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / decelDuration;

                if (HasAnimatorParameter(VelXHash))
                    _animator.SetFloat(VelXHash, Mathf.Lerp(startX, 0f, t));
                if (HasAnimatorParameter(VelZHash))
                    _animator.SetFloat(VelZHash, Mathf.Lerp(startZ, 0f, t));

                yield return null;
            }

            if (HasAnimatorParameter(VelXHash))
                _animator.SetFloat(VelXHash, 0f);
            if (HasAnimatorParameter(VelZHash))
                _animator.SetFloat(VelZHash, 0f);
        }

        private IEnumerator TurnInPlace(float angle)
        {
            float turnDirection = Mathf.Sign(angle);

            if (_animator != null)
            {
                if (HasAnimatorParameter(TurnInPlaceHash))
                {
                    _animator.SetTrigger(TurnInPlaceHash);
                    if (HasAnimatorParameter(TurnDirectionHash))
                        _animator.SetFloat(TurnDirectionHash, turnDirection);
                }
            }

            _navMeshAgent.isStopped = true;

            Quaternion startRot = transform.rotation;
            Vector3 targetDir = Quaternion.Euler(0, angle, 0) * transform.forward;
            Quaternion endRot = Quaternion.LookRotation(targetDir, Vector3.up);

            float elapsed = 0f;
            while (elapsed < turnInPlaceDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / turnInPlaceDuration);
                transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            transform.rotation = endRot;
            _navMeshAgent.isStopped = false;
        }

        private IEnumerator SmoothRotateTowards(Vector3 targetPosition)
        {
            Vector3 directionToTarget = targetPosition - transform.position;
            Vector3 flatDirection = Vector3.ProjectOnPlane(directionToTarget, Vector3.up).normalized;
            if (flatDirection == Vector3.zero) yield break;

            Quaternion targetRotation = Quaternion.LookRotation(flatDirection);
            float turnDuration = 0.5f;
            float elapsed = 0f;
            Quaternion startRotation = transform.rotation;

            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / turnDuration);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            transform.rotation = targetRotation;
        }

        public void StopMovement()
        {
            if (_movementCoroutine != null)
                StopCoroutine(_movementCoroutine);

            if (_navMeshAgent != null)
            {
                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
                _navMeshAgent.isStopped = false;
            }

            if (_animator != null)
            {
                if (HasAnimatorParameter(VelXHash))
                    _animator.SetFloat(VelXHash, 0f);
                if (HasAnimatorParameter(VelZHash))
                    _animator.SetFloat(VelZHash, 0f);
                if (HasAnimatorParameter(StopWalkingHash))
                    _animator.SetTrigger(StopWalkingHash);
            }

            _shouldRotateTowardsMovement = true;
            isMoving = false;
        }

        private bool HasAnimatorParameter(int paramHash)
        {
            if (_animator == null) return false;
            foreach (var param in _animator.parameters)
            {
                if (param.nameHash == paramHash) return true;
            }
            return false;
        }
    }
}