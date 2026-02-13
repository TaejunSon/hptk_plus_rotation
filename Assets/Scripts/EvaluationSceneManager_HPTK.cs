using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class EvaluationSceneManager_HPTK : MonoBehaviour
{
    [SerializeField]
    private int _participantNum;
    [SerializeField]
    private bool _isLeftHanded = false;
    [SerializeField]
    private GameObject _diePrefab, _targetPrefab;
    [SerializeField]
    private GameObject _centerEyeAnchor;
    [SerializeField]
    protected TextMeshProUGUI _text;
    //[SerializeField]
    //private EvaluationLogManager _logManager;

    private GameObject _die, _target;
    private const float CUBE_SCALE = 0.04f;
    private const float INIT_ROTATION_DEG = 135f;
    private Vector3 _initPosition = new Vector3(0.1f, 1.1f, 0.3f);
    private Vector3 _targetOffsetPosition;
    private Quaternion _targetOffsetRotation;
    private const float POSITION_THRESHOLD = 0.01f, ROTATION_THRESHOLD_DEG = 5f;

    private bool _isOnTarget = false, _isTimeout = false, _isInTrial = false;

    private const float DWELL_THRESHOLD = 1f, TIMEOUT_THRESHOLD = 30f;
    private float _dwellDuration, _trialDuration;

    private const int MAX_TRIAL_NUM = 20;
    private int _trialNum = 1;

    public event Action OnTrialEnd, OnTrialStart, OnTrialReset, OnSceneLoad, OnTarget, OffTarget, OnTimeout;
    public event Action<string> OnEvent;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        GenerateDie();
        //_rotationInteractor.SetCube(_die);

        //if (_isLeftHanded) _rotationInteractor.SetOVRSkeleton(_ovrLeftSkeleton);
        //else _rotationInteractor.SetOVRSkeleton(_ovrRightSkeleton);

        //_rotationInteractor.SetTransferFunction(_expCondition);

        _text.text = $"Trial {_trialNum}/{MAX_TRIAL_NUM}";

        //_logManager.SetExpConditions(_participantNum, _expCondition);
    }
    void Start()
    {
        OnSceneLoad += LoadNewScene;
        OnSceneLoad += () => { OnEvent?.Invoke("Scene Loaded"); };

        OnSceneLoad?.Invoke();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private void GenerateDie()
    {
        _die = Instantiate(_diePrefab);
        _die.transform.position = new Vector3(-_initPosition.x, _initPosition.y, _initPosition.z);
        _die.transform.localScale = new Vector3(CUBE_SCALE, CUBE_SCALE, CUBE_SCALE);
        _die.transform.rotation = Quaternion.identity;
    }
    private void LoadNewScene()
    {
        GenerateTarget();
        _text.text = $"Trial {_trialNum}/{MAX_TRIAL_NUM}";
    }
    private void GenerateTarget()
    {
        _target = Instantiate(_targetPrefab);
        Vector3 axis = UnityEngine.Random.onUnitSphere;
        _target.transform.Rotate(axis.normalized, INIT_ROTATION_DEG);
        _target.transform.position = _initPosition;
        _target.transform.localScale = new Vector3(CUBE_SCALE, CUBE_SCALE, CUBE_SCALE);
    }



}
