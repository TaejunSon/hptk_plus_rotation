using System;
using HandPhysicsToolkit.Physics;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EvaluationSceneManager_HPTK_2 : MonoBehaviour
{
    [SerializeField]
    private int _participantNum;
    [SerializeField]
    private bool _isLeftHanded = false;
    [SerializeField]
    private int _maxTrialNum = 20;
    [SerializeField]
    private GameObject _diePrefab, _targetPrefab;
    [SerializeField]
    private GameObject _centerEyeAnchor;
    [SerializeField]
    protected TextMeshProUGUI _text;

    private GameObject _die, _target;
    private Pheasy _currentPheasy;
    private Outline _outline;

    private const float CUBE_SCALE = 0.04f;
    private const float INIT_ROTATION_DEG = 135f;
    private readonly Vector3 _initPosition = new Vector3(0.1f, 1.1f, 0.3f);
    private Vector3 _targetOffsetPosition;
    private Quaternion _targetOffsetRotation;
    private const float POSITION_THRESHOLD = 0.1f, ROTATION_THRESHOLD_DEG = 20f;

    private bool _isOnTarget = false, _isTimeout = false, _isInTrial = false;

    private const float DWELL_THRESHOLD = 1f, TIMEOUT_THRESHOLD = 30f;
    private float _dwellDuration, _trialDuration;

    private int _trialNum = 1;

    public event Action OnTrialEnd, OnTrialStart, OnTrialReset, OnSceneLoad, OnTarget, OffTarget, OnTimeout;
    public event Action<string> OnEvent;

    void Awake()
    {
        GenerateDie();
        UpdateUIText();
    }

    void Start()
    {
        OnSceneLoad += LoadNewScene;
        OnSceneLoad += () => { OnEvent?.Invoke("Scene Loaded"); };

        OnTrialStart += StartTrial;
        OnTrialStart += () => { OnEvent?.Invoke("Trial Start"); };

        OnTrialEnd += EndTrial;
        OnTrialEnd += () => { OnEvent?.Invoke("Trial End"); };

        OnTrialReset += ResetTrial;
        OnTrialReset += () => { OnEvent?.Invoke("Trial Reset"); };

        OnTarget += () => { if (_outline != null) _outline.OutlineColor = Color.green; };
        OffTarget += () => { if (_outline != null) _outline.OutlineColor = Color.blue; };
        OnTrialEnd += () => { if (_outline != null) _outline.OutlineColor = Color.blue; };
        OnTrialReset += () => { if (_outline != null) _outline.OutlineColor = Color.blue; };

        OnTimeout += Timeout;
        OnTimeout += () => { OnEvent?.Invoke("Timed Out"); };

        OnSceneLoad?.Invoke();
    }

    void Update()
    {
        if (IsResetPressedThisFrame())
        {
            OnTrialReset?.Invoke();
            return;
        }

        if (_die != null && _target != null)
        {
            CalculateError(out Vector3 debugDeltaPos, out Quaternion debugDeltaRot);
            debugDeltaRot.ToAngleAxis(out float debugAngleError, out Vector3 debugAxis);
            Debug.Log($"[Diff] dPos:{debugDeltaPos} | pErr:{debugDeltaPos.magnitude:F4} | dRot:{debugDeltaRot} | rErr:{debugAngleError:F2} | axis:{debugAxis}");
        }

        _trialDuration += Time.deltaTime;

        if (_isInTrial && _trialDuration > TIMEOUT_THRESHOLD)
        {
            OnTimeout?.Invoke();
            if (_trialNum <= _maxTrialNum) OnSceneLoad?.Invoke();
            return;
        }

        if (!_isInTrial) return;

        bool isErrorSmall = CalculateError(out _targetOffsetPosition, out _targetOffsetRotation);

        if (isErrorSmall && !_isOnTarget)
        {
            _dwellDuration = 0f;
            _isOnTarget = true;
            OnTarget?.Invoke();
        }
        else if (!isErrorSmall && _isOnTarget)
        {
            _isOnTarget = false;
            OffTarget?.Invoke();
        }

        if (_isOnTarget)
        {
            _dwellDuration += Time.deltaTime;
            if (_dwellDuration > DWELL_THRESHOLD)
            {
                OnTrialEnd?.Invoke();
                if (_trialNum <= _maxTrialNum) OnSceneLoad?.Invoke();
            }
        }
    }

    private static bool IsResetPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return);
#else
        return false;
#endif
    }

    void OnDestroy()
    {
        if (_currentPheasy != null)
        {
            _currentPheasy.OnGrabEvent -= HandleGrabEvent;
            _currentPheasy.OnReleaseEvent -= HandleReleaseEvent;
            _currentPheasy = null;
        }
        DestroyTarget();
        DestroyDie();
    }

    private void LoadNewScene()
    {
        GenerateTarget();
        UpdateUIText();
    }

    private void StartTrial()
    {
        _isTimeout = false;
        _isInTrial = true;
        _trialDuration = 0f;
    }

    private void EndTrial()
    {
        ResetDie();
        DestroyTarget();
        _isOnTarget = false;
        _isInTrial = false;

        if (_trialNum == _maxTrialNum)
        {
            if (_die != null) _die.SetActive(false);
        }
        _trialNum++;
    }

    private void ResetTrial()
    {
        ResetDie();
        _isOnTarget = false;
        _trialDuration = 0f;
    }

    private void Timeout()
    {
        _isTimeout = true;
        OnTrialEnd?.Invoke();
    }

    private void HandleGrabEvent()
    {
        OnGrab();
        OnEvent?.Invoke("Grab");
    }

    private void HandleReleaseEvent()
    {
        OnRelease();
        OnEvent?.Invoke("Release");
    }

    private void OnGrab()
    {
        if (_outline != null) _outline.enabled = true;
        if (!_isInTrial)
        {
            OnTrialStart?.Invoke();
        }
    }

    private void OnRelease()
    {
        if (_outline != null) _outline.enabled = false;
    }

    public bool CalculateError(out Vector3 deltaPos, out Quaternion deltaRot)
    {
        if (_target == null || _die == null)
        {
            deltaPos = Vector3.zero;
            deltaRot = Quaternion.identity;
            return false;
        }

        deltaPos = _target.transform.position - _die.transform.position;
        deltaRot = _target.transform.rotation * Quaternion.Inverse(_die.transform.rotation);
        float pError = deltaPos.magnitude;
        deltaRot.ToAngleAxis(out float rError, out Vector3 _);
        return (pError < POSITION_THRESHOLD) && ((rError < ROTATION_THRESHOLD_DEG) || (rError > 360f - ROTATION_THRESHOLD_DEG));
    }

    private void GenerateDie()
    {
        if (_diePrefab == null)
        {
            Debug.LogError("Die prefab is not assigned.");
            return;
        }

        _die = Instantiate(_diePrefab);
        ResetDie();

        _outline = _die.GetComponent<Outline>();
        if (_outline == null) _outline = _die.GetComponentInChildren<Outline>();
        if (_outline != null)
        {
            _outline.enabled = false;
            _outline.OutlineColor = Color.blue;
        }

        _currentPheasy = _die.GetComponentInChildren<Pheasy>();
        if (_currentPheasy == null)
        {
            Debug.LogError("Pheasy component is missing on die prefab.");
            return;
        }

        _currentPheasy.OnGrabEvent -= HandleGrabEvent;
        _currentPheasy.OnReleaseEvent -= HandleReleaseEvent;
        _currentPheasy.OnGrabEvent += HandleGrabEvent;
        _currentPheasy.OnReleaseEvent += HandleReleaseEvent;
    }

    private void DestroyDie()
    {
        if (_die != null) Destroy(_die);
        _die = null;
    }

    private void ResetDie()
    {
        if (_die == null) return;
        _die.transform.position = new Vector3(-_initPosition.x, _initPosition.y, _initPosition.z);
        _die.transform.localScale = Vector3.one * CUBE_SCALE;
        _die.transform.rotation = Quaternion.identity;

        Rigidbody rb = _die.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void GenerateTarget()
    {
        if (_targetPrefab == null)
        {
            Debug.LogError("Target prefab is not assigned.");
            return;
        }

        _target = Instantiate(_targetPrefab);
        Vector3 axis = UnityEngine.Random.onUnitSphere;
        _target.transform.Rotate(axis.normalized, INIT_ROTATION_DEG);
        _target.transform.position = _initPosition;
        _target.transform.localScale = Vector3.one * CUBE_SCALE;
    }

    private void DestroyTarget()
    {
        if (_target != null) Destroy(_target);
        _target = null;
    }

    private void UpdateUIText()
    {
        if (_text != null)
        {
            _text.text = $"Trial {_trialNum}/{_maxTrialNum}";
        }
    }

    public void GetHeadTransform(out Vector3 position, out Quaternion rotation)
    {
        if (_centerEyeAnchor != null)
        {
            position = _centerEyeAnchor.transform.position;
            rotation = _centerEyeAnchor.transform.rotation;
        }
        else
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }

    public void GetDieTransform(out Vector3 position, out Quaternion rotation)
    {
        if (_die != null)
        {
            position = _die.transform.position;
            rotation = _die.transform.rotation;
        }
        else
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }
    }

    public void GetTargetOffset(out Vector3 position, out Quaternion rotation)
    {
        position = _targetOffsetPosition;
        rotation = _targetOffsetRotation;
    }

    public bool IsInTrial => _isInTrial;
    public int TrialNum => _trialNum;
    public float TrialDuration => _trialDuration;
}
