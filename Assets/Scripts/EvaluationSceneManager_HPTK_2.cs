using System;
using HandPhysicsToolkit.Physics;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class EvaluationSceneManager_HPTK_2 : MonoBehaviour
{
    private const string EVENT_TRIAL_LOAD = "Trial Load";
    private const string EVENT_TRIAL_START = "Trial Start";
    private const string EVENT_TRIAL_END = "Trial End";
    private const string EVENT_TRIAL_RESET = "Trial Reset";
    private const string EVENT_GRAB = "Grab";
    private const string EVENT_RELEASE = "Release";
    private const string EVENT_GRAB_LEFT = "Grab_left";
    private const string EVENT_RELEASE_LEFT = "Release_left";
    private const string EVENT_ON_TARGET = "On Target";
    private const string EVENT_OFF_TARGET = "Off Target";
    private const string EVENT_TIMEOUT = "Timed Out";

    [SerializeField]
    private int _participantNum;
    [SerializeField]
    private bool _isLeftHanded = false;
    [SerializeField]
    private bool _isPracticeMode = false;
    [SerializeField]
    private int _maxTrialNum = 20;
    [SerializeField]
    private GameObject _diePrefab, _targetPrefab;
    [SerializeField]
    private GameObject _centerEyeAnchor;
    [SerializeField]
    protected TextMeshProUGUI _text;
    [SerializeField]
    private ExperimentLogManager _logManager;
    [SerializeField]
    private AudioSource _audioSource;
    [SerializeField]
    private AudioClip _successSound;
    [SerializeField]
    private AudioClip _timeoutSound;

    private GameObject _die, _target;
    private Pheasy _currentPheasy;
    private Respawnable _currentRespawnable;
    private Outline _outline;

    //private GrabbableObject _grabbedObject;
    private const float CUBE_SCALE = 0.04f;
    private const float INIT_ROTATION_DEG = 135f;
    private readonly Vector3 _initPosition = new Vector3(0.1f, 1.1f, 0.3f);
    private Vector3 _targetOffsetPosition;
    private Quaternion _targetOffsetRotation;
    private const float POSITION_THRESHOLD = 0.02f, ROTATION_THRESHOLD_DEG = 10f;

    private bool _isOnTarget = false, _isTimeout = false, _isInTrial = false;

    private const float DWELL_THRESHOLD = 1f, TIMEOUT_THRESHOLD = 30f;
    private float _dwellDuration, _trialDuration;

    public Transform DieTransform => _die != null ? _die.transform : null;

    //From Hand Interacotr
    public Pose _wristWorld, _thumbTipWorld, _indexTipWorld, _middleTipWorld;
    public Pose _thumbTip, _indexTip, _middleTip;
    private Pose _prevThumbTip, _prevIndexTip, _prevMiddleTip;
     private Pose _thumbTipEuro, _indexTipEuro, _middleTipEuro;
    
    private Pose _triangle, _prevTriangle;
    private float _fingerMaxSpeed;
    
    public Pose _wristWorld_left, _thumbTipWorld_left, _indexTipWorld_left, _middleTipWorld_left;
    public Pose _thumbTip_left, _indexTip_left, _middleTip_left;
    private Pose _prevThumbTip_left, _prevIndexTip_left, _prevMiddleTip_left;
     private Pose _thumbTipEuro_left, _indexTipEuro_left, _middleTipEuro_left;
    
    private Pose _triangle_left, _prevTriangle_left;
    private float _fingerMaxSpeed_left;
    

    public bool IsGrabbed;
    public bool IsGrabbed_left;
    private bool _isGrabbed_right;
    private Pose _prevObject, _object, _objectWorld, _objectLocal;
    private Pose _objectLocal_left;
    private Pose _grabOffset, _grabOffsetTriangle;
    private OneEuroFilter<Vector3>[] _oneEuroFiltersVector3;
    private OneEuroFilter<Vector3>[] _oneEuroFiltersVector3_left;

    private int _trialNum = 1;

    public event Action OnTrialEnd, OnTrialStart, OnTrialReset, OnSceneLoad, OnTarget, OffTarget, OnTimeout;
    public event Action<string> OnEvent;

    public Transform wristWorld, thumbTipWorld, indexTipWorld, middleTipWorld;

    public Transform wristWorld_left, thumbTipWorld_left, indexTipWorld_left, middleTipWorld_left;


    void Awake()
    {
        _oneEuroFiltersVector3 = new OneEuroFilter<Vector3>[3];
        for (int i = 0; i < _oneEuroFiltersVector3.Length; i++)
        {
            _oneEuroFiltersVector3[i] = new OneEuroFilter<Vector3>();
        }

        _oneEuroFiltersVector3_left = new OneEuroFilter<Vector3>[3];
        for (int i = 0; i < _oneEuroFiltersVector3_left.Length; i++)
        {
            _oneEuroFiltersVector3_left[i] = new OneEuroFilter<Vector3>();
        }

        GenerateDie();
        UpdateUIText();
    }

    void Start()
    {
        OnSceneLoad += LoadNewTrial;
        OnSceneLoad += () => { OnEvent?.Invoke(EVENT_TRIAL_LOAD); };

        OnTrialStart += StartTrial;
        OnTrialStart += () => { OnEvent?.Invoke(EVENT_TRIAL_START); };

        // Log Trial End before scene state is reset/destroyed.
        OnTrialEnd += () => { OnEvent?.Invoke(EVENT_TRIAL_END); };
        OnTrialEnd += () => { if (!_isTimeout) PlaySound(_successSound); };
        OnTrialEnd += EndTrial;

        OnTrialReset += ResetTrial;
        OnTrialReset += () => { OnEvent?.Invoke(EVENT_TRIAL_RESET); };

        OnTarget += () => { if (_outline != null) _outline.OutlineColor = Color.green; };
        OnTarget += () => { OnEvent?.Invoke(EVENT_ON_TARGET); };

        OffTarget += () => { if (_outline != null) _outline.OutlineColor = Color.blue; };
        OffTarget += () => { OnEvent?.Invoke(EVENT_OFF_TARGET); };

        OnTrialEnd += () => { if (_outline != null) _outline.OutlineColor = Color.blue; };
        OnTrialReset += () => { if (_outline != null) _outline.OutlineColor = Color.blue; };

        OnTimeout += Timeout;
        OnTimeout += () => { PlaySound(_timeoutSound); };
        OnTimeout += () => { OnEvent?.Invoke(EVENT_TIMEOUT); };

        if (!_isPracticeMode && _logManager != null)
        {
            _logManager.Initialize(this);
            OnEvent += HandleLogEvent;
        }
        else if (!_isPracticeMode)
        {
            Debug.LogWarning("ExperimentLogManager is not assigned.");
        }

        OnSceneLoad?.Invoke();

        IsGrabbed = false;
        IsGrabbed_left = false;
        _isGrabbed_right = false;
        
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

        if (!_isPracticeMode && _isInTrial && _trialDuration > TIMEOUT_THRESHOLD)
        {
            OnTimeout?.Invoke();
            if (_trialNum <= _maxTrialNum) OnSceneLoad?.Invoke();
            return;
        }

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

        if (!_isInTrial) return;

        if (!_isPracticeMode)
        {
            _logManager?.WriteStreamRow();
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
    //Handinteractor part
        _wristWorld.position = wristWorld.position;
        _wristWorld.rotation = wristWorld.rotation;

        _thumbTipWorld.position = thumbTipWorld.position;
        _indexTipWorld.position = indexTipWorld.position;
        _middleTipWorld.position = middleTipWorld.position;

        _thumbTip.position = wristWorld.InverseTransformPoint(thumbTipWorld.position);
        _indexTip.position = wristWorld.InverseTransformPoint(indexTipWorld.position);
        _middleTip.position = wristWorld.InverseTransformPoint(middleTipWorld.position);

        _thumbTipEuro.position = _oneEuroFiltersVector3[0].Filter(_thumbTip.position, Time.deltaTime);
        _indexTipEuro.position = _oneEuroFiltersVector3[1].Filter(_indexTip.position, Time.deltaTime);
        _middleTipEuro.position = _oneEuroFiltersVector3[2].Filter(_middleTip.position, Time.deltaTime);

        Vector3 thumb, index, middle;
        thumb = _thumbTipEuro.position;
        index = _indexTipEuro.position;
        middle = _middleTipEuro.position;

        _triangle.position = GetWeightedTriangleCentroid(thumb, index, middle);

        bool isAngleValid, isTriangleValid, isAreaValid;
        
        isAngleValid = CalculateAngleAtVertex(thumb, index, middle, out float triangleP1Angle);
        isTriangleValid = CalculateTriangleOrientationWithOffset(thumb, index, middle, out _triangle.rotation);
        isAreaValid = CalculateTriangleArea(thumb, index, middle, out float triangleArea);
        _fingerMaxSpeed = GetMaxFingerSpeed(thumb, index, middle);


        _prevThumbTip.position = thumb;
        _prevIndexTip.position = index;
        _prevMiddleTip.position = middle;

        float deltaAngle = 0f;
        Vector3 deltaAxis = Vector3.one;

        if (isAngleValid && isTriangleValid && isAreaValid)
        {
            Quaternion deltaRotation = _triangle.rotation * Quaternion.Inverse(_prevTriangle.rotation);
            deltaRotation.ToAngleAxis(out deltaAngle, out deltaAxis);
            _prevTriangle.rotation = _triangle.rotation;
        }
        
        // if (_grabbedObject == null) OnGrab();
        //else CheckRelease();
        // 이 부분? 맞나
        _objectWorld.position = _die.transform.position;
        _objectWorld.rotation = _die.transform.rotation;

        _objectLocal.position = Quaternion.Inverse(_wristWorld.rotation) * (_objectWorld.position - _wristWorld.position);
        _objectLocal.rotation = Quaternion.Inverse(_wristWorld.rotation) * _objectWorld.rotation;

        if (wristWorld_left != null && thumbTipWorld_left != null && indexTipWorld_left != null && middleTipWorld_left != null)
        {
            _wristWorld_left.position = wristWorld_left.position;
            _wristWorld_left.rotation = wristWorld_left.rotation;

            _thumbTipWorld_left.position = thumbTipWorld_left.position;
            _indexTipWorld_left.position = indexTipWorld_left.position;
            _middleTipWorld_left.position = middleTipWorld_left.position;

            _thumbTip_left.position = wristWorld_left.InverseTransformPoint(thumbTipWorld_left.position);
            _indexTip_left.position = wristWorld_left.InverseTransformPoint(indexTipWorld_left.position);
            _middleTip_left.position = wristWorld_left.InverseTransformPoint(middleTipWorld_left.position);

            _thumbTipEuro_left.position = _oneEuroFiltersVector3_left[0].Filter(_thumbTip_left.position, Time.deltaTime);
            _indexTipEuro_left.position = _oneEuroFiltersVector3_left[1].Filter(_indexTip_left.position, Time.deltaTime);
            _middleTipEuro_left.position = _oneEuroFiltersVector3_left[2].Filter(_middleTip_left.position, Time.deltaTime);

            Vector3 thumb_left = _thumbTipEuro_left.position;
            Vector3 index_left = _indexTipEuro_left.position;
            Vector3 middle_left = _middleTipEuro_left.position;

            _triangle_left.position = GetWeightedTriangleCentroid(thumb_left, index_left, middle_left);

            bool isAngleValid_left, isTriangleValid_left, isAreaValid_left;
            isAngleValid_left = CalculateAngleAtVertex(thumb_left, index_left, middle_left, out float triangleP1Angle_left);
            isTriangleValid_left = CalculateTriangleOrientationWithOffset(thumb_left, index_left, middle_left, out _triangle_left.rotation);
            isAreaValid_left = CalculateTriangleArea(thumb_left, index_left, middle_left, out float triangleArea_left);
            _fingerMaxSpeed_left = GetMaxFingerSpeed_left(thumb_left, index_left, middle_left);

            _prevThumbTip_left.position = thumb_left;
            _prevIndexTip_left.position = index_left;
            _prevMiddleTip_left.position = middle_left;

            float deltaAngle_left = 0f;
            Vector3 deltaAxis_left = Vector3.one;

            if (isAngleValid_left && isTriangleValid_left && isAreaValid_left)
            {
                Quaternion deltaRotation_left = _triangle_left.rotation * Quaternion.Inverse(_prevTriangle_left.rotation);
                deltaRotation_left.ToAngleAxis(out deltaAngle_left, out deltaAxis_left);
                _prevTriangle_left.rotation = _triangle_left.rotation;
            }

            _objectLocal_left.position = Quaternion.Inverse(_wristWorld_left.rotation) * (_objectWorld.position - _wristWorld_left.position);
            _objectLocal_left.rotation = Quaternion.Inverse(_wristWorld_left.rotation) * _objectWorld.rotation;
        }

    }

        public Vector3 GetWeightedTriangleCentroid(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        if (CalculateAngleAtVertex(p1, p2, p3, out float angle))
        {
            float wT = Angle(p1, p2, p3);
            float wI = Angle(p2, p3, p1);
            float wM = Angle(p3, p1, p2);
            return (p1 / wT + p2 / wI + p3 / wM) / (1 / wT + 1 / wI + 1 / wM);
        }
        else return (p1 + p2 + p3) / 3f;
    }
    float Angle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = (b - a).normalized;
        Vector3 ac = (c - a).normalized;
        return Vector3.Angle(ab, ac);
    }

     private bool CalculateAngleAtVertex(Vector3 vertex, Vector3 other1, Vector3 other2, out float angle)
    {
        Vector3 vec1 = other1 - vertex;
        Vector3 vec2 = other2 - vertex;

        if (vec1.sqrMagnitude < 0.00001f || vec2.sqrMagnitude < 0.00001f)
        {
            angle = float.NaN;
            return false;
        }

        angle = Vector3.Angle(vec1, vec2);
        return true;
    }

        private bool CalculateTriangleOrientationWithOffset(Vector3 p1, Vector3 p2, Vector3 p3, out Quaternion orientation)
    {
        // 1. 기본 축 계산
        Vector3 forward = (p2 - p1).normalized;
        Vector3 toP3 = (p3 - p1).normalized;
        Vector3 normal = Vector3.Cross(forward, toP3).normalized;

        // 안전 장치
        if (forward.sqrMagnitude < 0.001f || normal.sqrMagnitude < 0.001f)
        {
            orientation = Quaternion.identity;
            return false;
        }

        // 2. 기본 회전 (Z축: P2 방향, Y축: 평면 수직)
        Quaternion baseRotation = Quaternion.LookRotation(forward, normal);

        // 3. [핵심] P1->P2와 P1->P3 사이의 평면상 각도 계산
        // forward를 기준으로 toP3가 평면 위에서 몇 도 돌아가 있는지 구합니다.
        // Vector3.SignedAngle을 사용하면 법선(normal)을 기준으로 시계/반시계 방향을 구분합니다.
        float angleOffset = Vector3.SignedAngle(forward, toP3, normal);

        // 4. 결과값: 기본 회전에 Y축(normal축) 기준 오프셋 적용
        // angleOffset을 그대로 쓰거나, 특정 기준 각도를 빼서 '0도' 지점을 설정할 수 있습니다.
        orientation = baseRotation * Quaternion.Euler(0, angleOffset, 0);

        return true;
    }

       public bool CalculateTriangleArea(Vector3 p1, Vector3 p2, Vector3 p3, out float area)
    {
        Vector3 vectorAB = (p2 - p1) * 100f;
        Vector3 vectorAC = (p3 - p1) * 100f;
        Vector3 vectorBC = (p3 - p2) * 100f;

        if (vectorAB.sqrMagnitude < 0.001f || vectorAC.sqrMagnitude < 0.001f
            || vectorBC.sqrMagnitude < 0.001f)
        {
            area = float.NaN;
            return false;
        }
        // _textbox.text = $"{vectorAB.magnitude}\n {vectorAC.magnitude}\n {vectorBC.magnitude}";

        Vector3 crossProduct = Vector3.Cross(vectorAB, vectorAC);

        area = crossProduct.magnitude / 2f;

        // _textbox.text = $"{area}";

        if (area < 0.001f)
        {
            area = float.NaN;
            return false;
        }

        return true;
    }
        private float GetMaxFingerSpeed(Vector3 thumb, Vector3 index, Vector3 middle)
    {
        // in m
        float deltaThumb = (thumb - _prevThumbTip.position).magnitude;
        float deltaIndex = (index - _prevIndexTip.position).magnitude;
        float deltaMiddle = (middle - _prevMiddleTip.position).magnitude;

        // in m/s
        float speedThumb = deltaThumb / Time.deltaTime;
        float speedIndex = deltaIndex / Time.deltaTime;
        float speedMiddle = deltaMiddle / Time.deltaTime;

        // DateTimeOffset dt = new DateTimeOffset(DateTime.Now);
        // _timeStamp = dt.ToUnixTimeMilliseconds();
        // _streamWriter.WriteLine($"{_timeStamp}, {speedThumb:F2}, {speedIndex:F2}, {speedMiddle:F2}");


        return Math.Max(speedThumb, Math.Max(speedIndex, speedMiddle));
    }

    private float GetMaxFingerSpeed_left(Vector3 thumb, Vector3 index, Vector3 middle)
    {
        // in m
        float deltaThumb = (thumb - _prevThumbTip_left.position).magnitude;
        float deltaIndex = (index - _prevIndexTip_left.position).magnitude;
        float deltaMiddle = (middle - _prevMiddleTip_left.position).magnitude;

        // in m/s
        float speedThumb = deltaThumb / Time.deltaTime;
        float speedIndex = deltaIndex / Time.deltaTime;
        float speedMiddle = deltaMiddle / Time.deltaTime;

        return Math.Max(speedThumb, Math.Max(speedIndex, speedMiddle));
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
        if (!_isPracticeMode && _logManager != null)
        {
            OnEvent -= HandleLogEvent;
            _logManager.CloseAll();
        }

        if (_currentPheasy != null)
        {
            _currentPheasy.OnGrabEvent -= HandleGrabEvent;
            _currentPheasy.OnReleaseEvent -= HandleReleaseEvent;
            _currentPheasy.OnGrabEvent_left -= HandleGrabEvent_left;
            _currentPheasy.OnReleaseEvent_left -= HandleReleaseEvent_left;
            _currentPheasy = null;
        }
        if (_currentRespawnable != null)
        {
            _currentRespawnable.OnRespawnEvent -= HandleRespawnEvent;
            _currentRespawnable = null;
        }
        DestroyTarget();
        DestroyDie();
    }

    private void LoadNewTrial()
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
        OnEvent?.Invoke(EVENT_GRAB);
    }

    private void HandleReleaseEvent()
    {
        OnRelease();
        OnEvent?.Invoke(EVENT_RELEASE);
    }

    private void HandleGrabEvent_left()
    {
        OnGrab_left();
        OnEvent?.Invoke(EVENT_GRAB_LEFT);
    }

    private void HandleReleaseEvent_left()
    {
        OnRelease_left();
        OnEvent?.Invoke(EVENT_RELEASE_LEFT);
    }

    private void HandleRespawnEvent()
    {
        if (_isInTrial)
        {
            OnTrialReset?.Invoke();
        }
    }

    private void HandleLogEvent(string eventName)
    {
        _logManager?.OnEvent(eventName);
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void OnGrab()
    {
        _isGrabbed_right = true;
        UpdateGrabbedState();
    }

    private void OnGrab_left()
    {
        IsGrabbed_left = true;
        UpdateGrabbedState();
    }

    private void OnRelease()
    {
        _isGrabbed_right = false;
        UpdateGrabbedState();
    }

    private void OnRelease_left()
    {
        IsGrabbed_left = false;
        UpdateGrabbedState();
    }

    private void UpdateGrabbedState()
    {
        bool wasGrabbed = IsGrabbed;
        IsGrabbed = _isGrabbed_right || IsGrabbed_left;

        if (!wasGrabbed && IsGrabbed)
        {
            if (!_isInTrial)
            {
                OnTrialStart?.Invoke();
            }
            return;
        }

        if (wasGrabbed && !IsGrabbed)
        {
            _dwellDuration = 0f;
        }
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
            _outline.enabled = true;
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
        _currentPheasy.OnGrabEvent_left -= HandleGrabEvent_left;
        _currentPheasy.OnReleaseEvent_left -= HandleReleaseEvent_left;
        _currentPheasy.OnGrabEvent += HandleGrabEvent;
        _currentPheasy.OnReleaseEvent += HandleReleaseEvent;
        _currentPheasy.OnGrabEvent_left += HandleGrabEvent_left;
        _currentPheasy.OnReleaseEvent_left += HandleReleaseEvent_left;

        _currentRespawnable = _die.GetComponentInChildren<Respawnable>();
        if (_currentRespawnable != null)
        {
            _currentRespawnable.OnRespawnEvent -= HandleRespawnEvent;
            _currentRespawnable.OnRespawnEvent += HandleRespawnEvent;
        }
    }

    private void DestroyDie()
    {
        if (_die != null) Destroy(_die);
        _die = null;
    }

    private void ResetDie()
    {
        if (_die == null) return;
        _die.transform.position = _isLeftHanded
            ? _initPosition
            : new Vector3(-_initPosition.x, _initPosition.y, _initPosition.z);
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
        _target.transform.position = !_isLeftHanded
            ? _initPosition
            : new Vector3(-_initPosition.x, _initPosition.y, _initPosition.z);
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

    public Vector3 HeadPosition => _centerEyeAnchor.transform.position;
    public Quaternion HeadRotation => _centerEyeAnchor.transform.rotation;
    private bool CalculateError(out Pose delta)
    {
        delta.position = _target.transform.position - _die.transform.position; //
        delta.rotation = _target.transform.rotation * Quaternion.Inverse(_die.transform.rotation);
        float pError = delta.position.magnitude;
        delta.rotation.ToAngleAxis(out float rError, out Vector3 axis);
        return (pError < POSITION_THRESHOLD) && ((rError < ROTATION_THRESHOLD_DEG) || (rError > 360f - ROTATION_THRESHOLD_DEG));
    }
    public Pose TargetOffset
    {
        get
        {
            if (_die == null || _target == null) return new Pose(Vector3.zero, Quaternion.identity);
            CalculateError(out Pose delta);
            return delta;
        }
    }

    public bool IsTimeout => _isTimeout;
    public bool IsGrabbed_right => _isGrabbed_right;
    public Vector3 WristWorldPosition => _wristWorld.position;
    public Quaternion WristWorldRotation => _wristWorld.rotation;
    public Vector3 WristWorldPosition_left => _wristWorld_left.position;
    public Quaternion WristWorldRotation_left => _wristWorld_left.rotation;
    public Vector3 ThumbTipWorldPosition => _thumbTipWorld.position;
    public Vector3 IndexTipWorldPosition => _indexTipWorld.position;
    public Vector3 MiddleTipWorldPosition => _middleTipWorld.position;
    public Vector3 ThumbTipWorldPosition_left => _thumbTipWorld_left.position;
    public Vector3 IndexTipWorldPosition_left => _indexTipWorld_left.position;
    public Vector3 MiddleTipWorldPosition_left => _middleTipWorld_left.position;
    public Vector3 ThumbTipLocalPosition => _thumbTip.position;
    public Vector3 IndexTipLocalPosition => _indexTip.position;
    public Vector3 MiddleTipLocalPosition => _middleTip.position;
    public Vector3 ThumbTipLocalPosition_left => _thumbTip_left.position;
    public Vector3 IndexTipLocalPosition_left => _indexTip_left.position;
    public Vector3 MiddleTipLocalPosition_left => _middleTip_left.position;
    public Vector3 TriangleCentroidPosition => _triangle.position;
    public Quaternion TriangleRotation => _triangle.rotation;
    public Vector3 TriangleCentroidPosition_left => _triangle_left.position;
    public Quaternion TriangleRotation_left => _triangle_left.rotation;
    public Pose ObjectWorldPose => _objectWorld;
    public Pose ObjectLocalPose => _objectLocal;
    public Pose ObjectLocalPose_left => _objectLocal_left;
    //public float AngleScaleFactor => _angleScaleFactor;
    //public bool IsRotating => _isRotating;
    //public bool IsGrabbed => _grabbedObject != null;
    //public int GainCondition => _gainCondition;
    public Vector3 ThumbTipEuroPosition => _thumbTipEuro.position;
    public Vector3 IndexTipEuroPosition => _indexTipEuro.position;
    public Vector3 MiddleTipEuroPosition => _middleTipEuro.position;
    public Vector3 ThumbTipEuroPosition_left => _thumbTipEuro_left.position;
    public Vector3 IndexTipEuroPosition_left => _indexTipEuro_left.position;
    public Vector3 MiddleTipEuroPosition_left => _middleTipEuro_left.position;
    //public bool IsClutching => _isClutching;

    public bool IsOnTarget => _isOnTarget;

}
